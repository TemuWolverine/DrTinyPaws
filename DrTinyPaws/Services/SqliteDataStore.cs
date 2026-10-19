using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DrTinyPaws.Models;

namespace DrTinyPaws.Services;

public class SqliteDataStore : IDataStore, IDisposable
{
    private string _databasePath = string.Empty;
    private FileSystemWatcher? _fileWatcher;
    private System.Threading.Timer? _debounceTimer;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public string DatabasePath => _databasePath;
    public event EventHandler? DataChanged;

    public async Task InitializeAsync(bool seedSampleData = true)
    {
        if (string.IsNullOrEmpty(_databasePath))
        {
            _databasePath = LoadDatabasePath();
        }
        var dbDir = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        await ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS Medications (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Dosage TEXT,
                ColorHex TEXT NOT NULL,
                Schedule TEXT,
                ReminderTime TEXT,
                Notes TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS MedicationIntakes (
                Id TEXT PRIMARY KEY,
                MedicationId TEXT NOT NULL,
                Quantity INTEGER NOT NULL,
                TakenAt TEXT NOT NULL,
                Notes TEXT
            );

            CREATE TABLE IF NOT EXISTS DoseGroups (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT,
                ColorHex TEXT,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS DoseGroupItems (
                Id TEXT PRIMARY KEY,
                DoseGroupId TEXT NOT NULL,
                MedicationId TEXT NOT NULL,
                Quantity INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_intakes_takenat ON MedicationIntakes(TakenAt);
            CREATE INDEX IF NOT EXISTS idx_groupitems_groupid ON DoseGroupItems(DoseGroupId);
        ");

        if (seedSampleData)
        {
            await SeedSampleDataIfEmptyAsync();
        }
        SetupFileWatcher();
    }

    private string GetConfigFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "DrTinyPaws");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return Path.Combine(dir, "config.json");
    }

    private string LoadDatabasePath()
    {
        try
        {
            var configPath = GetConfigFilePath();
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("DatabasePath", out var pathProp))
                {
                    var customPath = pathProp.GetString();
                    if (!string.IsNullOrWhiteSpace(customPath))
                    {
                        return customPath;
                    }
                }
            }
        }
        catch
        {
            // Ignore config read errors and fall back to default
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "DrTinyPaws", "drtinypaws.db");
    }

    public async Task SetDatabasePathAsync(string newPath, bool seedSampleData = true)
    {
        if (string.IsNullOrWhiteSpace(newPath) || newPath == _databasePath)
            return;

        var newDir = Path.GetDirectoryName(newPath);
        if (!string.IsNullOrEmpty(newDir) && !Directory.Exists(newDir))
        {
            Directory.CreateDirectory(newDir);
        }

        _databasePath = newPath;
        var configPath = GetConfigFilePath();
        var escapedPath = _databasePath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var json = $"{{\n  \"DatabasePath\": \"{escapedPath}\"\n}}";
        await File.WriteAllTextAsync(configPath, json);

        SetupFileWatcher();
        await InitializeAsync(seedSampleData);
        NotifyDataChanged();
    }

    private void SetupFileWatcher()
    {
        _fileWatcher?.Dispose();
        _fileWatcher = null;

        try
        {
            var dir = Path.GetDirectoryName(_databasePath);
            var fileName = Path.GetFileName(_databasePath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                _fileWatcher = new FileSystemWatcher(dir, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Changed += (s, e) => OnDatabaseFileChanged();
                _fileWatcher.Created += (s, e) => OnDatabaseFileChanged();
            }
        }
        catch
        {
            // Best effort file watcher
        }
    }

    private void OnDatabaseFileChanged()
    {
        // Debounce to avoid multiple rapid triggers while syncing
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ =>
        {
            NotifyDataChanged();
        }, null, 500, Timeout.Infinite);
    }

    private void NotifyDataChanged()
    {
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection($"Data Source={_databasePath}");
        conn.Open();
        return conn;
    }

    private async Task ExecuteNonQueryAsync(string sql)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SeedSampleDataIfEmptyAsync()
    {
        var existingMeds = await GetMedicationsAsync(includeInactive: true);
        if (existingMeds.Count > 0) return;

        // Seed default medications mentioned in user request
        var panadeine = new Medication
        {
            Name = "Panadeine",
            Dosage = "500mg / 8mg",
            ColorHex = "#2563EB", // Blue
            Schedule = "As needed, max 4x daily",
            ReminderTime = "08:00",
            Notes = "Take with water"
        };

        var antihistamine = new Medication
        {
            Name = "Anti-histamine",
            Dosage = "10mg",
            ColorHex = "#DC2626", // Red
            Schedule = "Daily at night",
            ReminderTime = "21:00",
            Notes = "May cause drowsiness"
        };

        var vitaminC = new Medication
        {
            Name = "Vitamin C",
            Dosage = "1000mg",
            ColorHex = "#F59E0B", // Amber / Yellow
            Schedule = "Morning daily",
            ReminderTime = "08:00",
            Notes = "Effervescent tablet"
        };

        var magnesium = new Medication
        {
            Name = "Magnesium",
            Dosage = "300mg",
            ColorHex = "#10B981", // Emerald
            Schedule = "Evening daily",
            ReminderTime = "20:00"
        };

        await SaveMedicationAsync(panadeine);
        await SaveMedicationAsync(antihistamine);
        await SaveMedicationAsync(vitaminC);
        await SaveMedicationAsync(magnesium);

        // Seed "Morning" dose group as described in the prompt
        var morningGroup = new DoseGroup
        {
            Name = "Morning Routine",
            Description = "3 Panadeine, 2 Vitamin C",
            Items =
            [
                new DoseGroupItem { MedicationId = panadeine.Id, Quantity = 3 },
                new DoseGroupItem { MedicationId = vitaminC.Id, Quantity = 2 }
            ]
        };
        await SaveDoseGroupAsync(morningGroup);

        // Seed sample intake data for current week so chart renders immediately
        var now = DateTime.Now;
        // Start from Monday of current week
        int diff = (7 + (int)now.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        var monday = now.Date.AddDays(-diff);

        var sampleIntakes = new List<MedicationIntake>
        {
            new() { MedicationId = panadeine.Id, Quantity = 2, TakenAt = monday.AddHours(8).AddMinutes(15) },
            new() { MedicationId = vitaminC.Id, Quantity = 1, TakenAt = monday.AddHours(8).AddMinutes(20) },
            new() { MedicationId = antihistamine.Id, Quantity = 1, TakenAt = monday.AddHours(21).AddMinutes(0) },

            new() { MedicationId = panadeine.Id, Quantity = 3, TakenAt = monday.AddDays(1).AddHours(8).AddMinutes(30) },
            new() { MedicationId = vitaminC.Id, Quantity = 2, TakenAt = monday.AddDays(1).AddHours(8).AddMinutes(30) },
            new() { MedicationId = magnesium.Id, Quantity = 1, TakenAt = monday.AddDays(1).AddHours(20).AddMinutes(45) },

            new() { MedicationId = antihistamine.Id, Quantity = 1, TakenAt = monday.AddDays(2).AddHours(9).AddMinutes(0) },
            new() { MedicationId = panadeine.Id, Quantity = 1, TakenAt = monday.AddDays(2).AddHours(13).AddMinutes(30) }
        };

        if (now >= monday.AddDays(3))
        {
            sampleIntakes.Add(new() { MedicationId = panadeine.Id, Quantity = 2, TakenAt = monday.AddDays(3).AddHours(8).AddMinutes(10) });
            sampleIntakes.Add(new() { MedicationId = vitaminC.Id, Quantity = 1, TakenAt = monday.AddDays(3).AddHours(8).AddMinutes(10) });
            sampleIntakes.Add(new() { MedicationId = antihistamine.Id, Quantity = 1, TakenAt = monday.AddDays(3).AddHours(21).AddMinutes(15) });
        }

        await LogIntakesAsync(sampleIntakes);
    }

    public async Task<List<Medication>> GetMedicationsAsync(bool includeInactive = false)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = includeInactive
                ? "SELECT Id, Name, Dosage, ColorHex, Schedule, ReminderTime, Notes, IsActive, CreatedAt FROM Medications ORDER BY Name ASC"
                : "SELECT Id, Name, Dosage, ColorHex, Schedule, ReminderTime, Notes, IsActive, CreatedAt FROM Medications WHERE IsActive = 1 ORDER BY Name ASC";

            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<Medication>();
            while (await reader.ReadAsync())
            {
                list.Add(new Medication
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    Dosage = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ColorHex = reader.GetString(3),
                    Schedule = reader.IsDBNull(4) ? null : reader.GetString(4),
                    ReminderTime = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Notes = reader.IsDBNull(6) ? null : reader.GetString(6),
                    IsActive = reader.GetInt32(7) == 1,
                    CreatedAt = DateTime.Parse(reader.GetString(8))
                });
            }
            return list;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<Medication>> GetLastAddedMedicationsAsync(int count = 4)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Dosage, ColorHex, Schedule, ReminderTime, Notes, IsActive, CreatedAt FROM Medications WHERE IsActive = 1 ORDER BY CreatedAt DESC, rowid DESC LIMIT @Limit";
            cmd.Parameters.AddWithValue("@Limit", count);

            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<Medication>();
            while (await reader.ReadAsync())
            {
                list.Add(new Medication
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    Dosage = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ColorHex = reader.GetString(3),
                    Schedule = reader.IsDBNull(4) ? null : reader.GetString(4),
                    ReminderTime = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Notes = reader.IsDBNull(6) ? null : reader.GetString(6),
                    IsActive = reader.GetInt32(7) == 1,
                    CreatedAt = DateTime.Parse(reader.GetString(8))
                });
            }
            return list;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Medication?> GetMedicationByIdAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Dosage, ColorHex, Schedule, ReminderTime, Notes, IsActive, CreatedAt FROM Medications WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Medication
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    Dosage = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ColorHex = reader.GetString(3),
                    Schedule = reader.IsDBNull(4) ? null : reader.GetString(4),
                    ReminderTime = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Notes = reader.IsDBNull(6) ? null : reader.GetString(6),
                    IsActive = reader.GetInt32(7) == 1,
                    CreatedAt = DateTime.Parse(reader.GetString(8))
                };
            }
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveMedicationAsync(Medication medication)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Medications (Id, Name, Dosage, ColorHex, Schedule, ReminderTime, Notes, IsActive, CreatedAt)
                VALUES (@Id, @Name, @Dosage, @ColorHex, @Schedule, @ReminderTime, @Notes, @IsActive, @CreatedAt)
                ON CONFLICT(Id) DO UPDATE SET
                    Name = excluded.Name,
                    Dosage = excluded.Dosage,
                    ColorHex = excluded.ColorHex,
                    Schedule = excluded.Schedule,
                    ReminderTime = excluded.ReminderTime,
                    Notes = excluded.Notes,
                    IsActive = excluded.IsActive;
            ";

            cmd.Parameters.AddWithValue("@Id", medication.Id);
            cmd.Parameters.AddWithValue("@Name", medication.Name);
            cmd.Parameters.AddWithValue("@Dosage", (object?)medication.Dosage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ColorHex", medication.ColorHex);
            cmd.Parameters.AddWithValue("@Schedule", (object?)medication.Schedule ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ReminderTime", (object?)medication.ReminderTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Notes", (object?)medication.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", medication.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@CreatedAt", medication.CreatedAt.ToString("O"));

            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
        NotifyDataChanged();
    }

    public async Task DeleteMedicationAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            // Soft delete to maintain history integrity
            cmd.CommandText = "UPDATE Medications SET IsActive = 0 WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
        NotifyDataChanged();
    }

    public async Task<List<MedicationIntake>> GetIntakesForDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT i.Id, i.MedicationId, i.Quantity, i.TakenAt, i.Notes,
                       m.Name, m.ColorHex, m.Dosage
                FROM MedicationIntakes i
                LEFT JOIN Medications m ON i.MedicationId = m.Id
                WHERE i.TakenAt >= @StartDate AND i.TakenAt <= @EndDate
                ORDER BY i.TakenAt DESC;
            ";
            cmd.Parameters.AddWithValue("@StartDate", startDate.ToString("O"));
            cmd.Parameters.AddWithValue("@EndDate", endDate.ToString("O"));

            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<MedicationIntake>();
            while (await reader.ReadAsync())
            {
                list.Add(new MedicationIntake
                {
                    Id = reader.GetString(0),
                    MedicationId = reader.GetString(1),
                    Quantity = reader.GetInt32(2),
                    TakenAt = DateTime.Parse(reader.GetString(3)),
                    Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
                    MedicationName = reader.IsDBNull(5) ? "Unknown" : reader.GetString(5),
                    MedicationColorHex = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    MedicationDosage = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }
            return list;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<MedicationIntake>> GetRecentIntakesAsync(int limit = 200)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT i.Id, i.MedicationId, i.Quantity, i.TakenAt, i.Notes,
                       m.Name, m.ColorHex, m.Dosage
                FROM MedicationIntakes i
                LEFT JOIN Medications m ON i.MedicationId = m.Id
                ORDER BY i.TakenAt DESC
                LIMIT @Limit;
            ";
            cmd.Parameters.AddWithValue("@Limit", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<MedicationIntake>();
            while (await reader.ReadAsync())
            {
                list.Add(new MedicationIntake
                {
                    Id = reader.GetString(0),
                    MedicationId = reader.GetString(1),
                    Quantity = reader.GetInt32(2),
                    TakenAt = DateTime.Parse(reader.GetString(3)),
                    Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
                    MedicationName = reader.IsDBNull(5) ? "Unknown" : reader.GetString(5),
                    MedicationColorHex = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    MedicationDosage = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }
            return list;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task LogIntakeAsync(MedicationIntake intake)
    {
        await LogIntakesAsync([intake]);
    }

    public async Task LogIntakesAsync(IEnumerable<MedicationIntake> intakes)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();
            foreach (var intake in intakes)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO MedicationIntakes (Id, MedicationId, Quantity, TakenAt, Notes)
                    VALUES (@Id, @MedicationId, @Quantity, @TakenAt, @Notes);
                ";
                cmd.Parameters.AddWithValue("@Id", string.IsNullOrEmpty(intake.Id) ? Guid.NewGuid().ToString() : intake.Id);
                cmd.Parameters.AddWithValue("@MedicationId", intake.MedicationId);
                cmd.Parameters.AddWithValue("@Quantity", intake.Quantity);
                cmd.Parameters.AddWithValue("@TakenAt", intake.TakenAt.ToString("O"));
                cmd.Parameters.AddWithValue("@Notes", (object?)intake.Notes ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
            tx.Commit();
        }
        finally
        {
            _lock.Release();
        }
        NotifyDataChanged();
    }

    public async Task DeleteIntakeAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM MedicationIntakes WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
        NotifyDataChanged();
    }

    public async Task<List<DoseGroup>> GetDoseGroupsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Description, ColorHex, CreatedAt FROM DoseGroups ORDER BY Name ASC";

            using var reader = await cmd.ExecuteReaderAsync();
            var groups = new List<DoseGroup>();
            while (await reader.ReadAsync())
            {
                groups.Add(new DoseGroup
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ColorHex = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CreatedAt = DateTime.Parse(reader.GetString(4))
                });
            }

            foreach (var g in groups)
            {
                using var itemCmd = conn.CreateCommand();
                itemCmd.CommandText = @"
                    SELECT i.Id, i.DoseGroupId, i.MedicationId, i.Quantity,
                           m.Name, m.ColorHex, m.Dosage
                    FROM DoseGroupItems i
                    LEFT JOIN Medications m ON i.MedicationId = m.Id
                    WHERE i.DoseGroupId = @GroupId;
                ";
                itemCmd.Parameters.AddWithValue("@GroupId", g.Id);
                using var itemReader = await itemCmd.ExecuteReaderAsync();
                while (await itemReader.ReadAsync())
                {
                    g.Items.Add(new DoseGroupItem
                    {
                        Id = itemReader.GetString(0),
                        DoseGroupId = itemReader.GetString(1),
                        MedicationId = itemReader.GetString(2),
                        Quantity = itemReader.GetInt32(3),
                        MedicationName = itemReader.IsDBNull(4) ? "Unknown" : itemReader.GetString(4),
                        MedicationColorHex = itemReader.IsDBNull(5) ? string.Empty : itemReader.GetString(5),
                        MedicationDosage = itemReader.IsDBNull(6) ? null : itemReader.GetString(6)
                    });
                }
            }

            return groups;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<DoseGroup?> GetDoseGroupByIdAsync(string id)
    {
        var groups = await GetDoseGroupsAsync();
        return groups.Find(g => g.Id == id);
    }

    public async Task SaveDoseGroupAsync(DoseGroup group)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO DoseGroups (Id, Name, Description, ColorHex, CreatedAt)
                VALUES (@Id, @Name, @Description, @ColorHex, @CreatedAt)
                ON CONFLICT(Id) DO UPDATE SET
                    Name = excluded.Name,
                    Description = excluded.Description,
                    ColorHex = excluded.ColorHex;
            ";
            cmd.Parameters.AddWithValue("@Id", group.Id);
            cmd.Parameters.AddWithValue("@Name", group.Name);
            cmd.Parameters.AddWithValue("@Description", (object?)group.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ColorHex", (object?)group.ColorHex ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedAt", group.CreatedAt.ToString("O"));
            await cmd.ExecuteNonQueryAsync();

            // Replace items
            using var deleteCmd = conn.CreateCommand();
            deleteCmd.Transaction = tx;
            deleteCmd.CommandText = "DELETE FROM DoseGroupItems WHERE DoseGroupId = @GroupId";
            deleteCmd.Parameters.AddWithValue("@GroupId", group.Id);
            await deleteCmd.ExecuteNonQueryAsync();

            foreach (var item in group.Items)
            {
                using var insertItemCmd = conn.CreateCommand();
                insertItemCmd.Transaction = tx;
                insertItemCmd.CommandText = @"
                    INSERT INTO DoseGroupItems (Id, DoseGroupId, MedicationId, Quantity)
                    VALUES (@Id, @DoseGroupId, @MedicationId, @Quantity);
                ";
                insertItemCmd.Parameters.AddWithValue("@Id", string.IsNullOrEmpty(item.Id) ? Guid.NewGuid().ToString() : item.Id);
                insertItemCmd.Parameters.AddWithValue("@DoseGroupId", group.Id);
                insertItemCmd.Parameters.AddWithValue("@MedicationId", item.MedicationId);
                insertItemCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                await insertItemCmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
        }
        finally
        {
            _lock.Release();
        }
        NotifyDataChanged();
    }

    public async Task DeleteDoseGroupAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();

            using var cmdItems = conn.CreateCommand();
            cmdItems.Transaction = tx;
            cmdItems.CommandText = "DELETE FROM DoseGroupItems WHERE DoseGroupId = @GroupId";
            cmdItems.Parameters.AddWithValue("@GroupId", id);
            await cmdItems.ExecuteNonQueryAsync();

            using var cmdGroup = conn.CreateCommand();
            cmdGroup.Transaction = tx;
            cmdGroup.CommandText = "DELETE FROM DoseGroups WHERE Id = @Id";
            cmdGroup.Parameters.AddWithValue("@Id", id);
            await cmdGroup.ExecuteNonQueryAsync();

            tx.Commit();
        }
        finally
        {
            _lock.Release();
        }
        NotifyDataChanged();
    }

    public async Task<PillLoggerImportResult> ImportPillLoggerDatabaseAsync(string sourceDbPath, bool clearExistingData = true)
    {
        await _lock.WaitAsync();
        try
        {
            var importer = new PillLoggerImporter();
            var result = await importer.ImportAsync(sourceDbPath, _databasePath, clearExistingData);
            if (result.Success)
            {
                NotifyDataChanged();
            }
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _fileWatcher?.Dispose();
        _lock.Dispose();
    }
}
