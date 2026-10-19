using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace DrTinyPaws.Services;

public class PillLoggerImporter : IPillLoggerImporter
{
    private static readonly string[] ColorPalette =
    [
        "#2563EB", // Blue
        "#DC2626", // Red
        "#16A34A", // Green
        "#F59E0B", // Amber
        "#9333EA", // Purple
        "#0D9488", // Teal
        "#EC4899", // Pink
        "#0284C7", // Sky Blue
        "#E11D48", // Rose
        "#475569", // Slate
        "#CA8A04", // Yellow
        "#4F46E5"  // Indigo
    ];

    public static string MapColor(long? rawVal, int index)
    {
        if (rawVal == null || rawVal == 0)
        {
            return ColorPalette[index % ColorPalette.Length];
        }

        uint uintVal = (uint)(rawVal.Value & 0xFFFFFFFF);
        string hexStr = uintVal.ToString("x8");
        if (hexStr.StartsWith("64", StringComparison.OrdinalIgnoreCase) ||
            hexStr.StartsWith("ff", StringComparison.OrdinalIgnoreCase))
        {
            string rgb = hexStr.Substring(2).ToUpperInvariant();
            if (rgb == "FFFFFF") return "#64748B";
            if (rgb == "000000") return "#334155";
            return $"#{rgb}";
        }

        return ColorPalette[index % ColorPalette.Length];
    }

    public async Task<PillLoggerImportResult> ImportAsync(string pillLoggerDbPath, string targetDbPath, bool clearExistingData = true)
    {
        if (string.IsNullOrWhiteSpace(pillLoggerDbPath) || !File.Exists(pillLoggerDbPath))
        {
            return new PillLoggerImportResult(false, 0, 0, 0, "Selected file does not exist.");
        }

        try
        {
            var targetDir = Path.GetDirectoryName(targetDbPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            using var srcConn = new SqliteConnection($"Data Source={pillLoggerDbPath};Mode=ReadOnly");
            await srcConn.OpenAsync();

            // Validate schema
            if (!await TableExistsAsync(srcConn, "pills") || !await TableExistsAsync(srcConn, "consumptions"))
            {
                return new PillLoggerImportResult(false, 0, 0, 0, "The selected file is not a valid Pill Logger database (missing 'pills' or 'consumptions' tables).");
            }

            // Fetch units
            var unitsMap = new Dictionary<long, string>();
            if (await TableExistsAsync(srcConn, "units"))
            {
                using var unitsCmd = srcConn.CreateCommand();
                unitsCmd.CommandText = "SELECT _id, name FROM units";
                using var unitsReader = await unitsCmd.ExecuteReaderAsync();
                while (await unitsReader.ReadAsync())
                {
                    long id = unitsReader.GetInt64(0);
                    string name = unitsReader.IsDBNull(1) ? string.Empty : unitsReader.GetString(1);
                    unitsMap[id] = name;
                }
            }

            // Fetch pills
            var pillIdToMedId = new Dictionary<long, string>();
            var pillNames = new Dictionary<long, string>();
            var medRows = new List<(string Id, string Name, string? Dosage, string ColorHex, string? Schedule, string? ReminderTime, string? Notes, int IsActive, string CreatedAt)>();

            using (var pillsCmd = srcConn.CreateCommand())
            {
                pillsCmd.CommandText = "SELECT _id, name, size, colour, units_id, default_reminder FROM pills ORDER BY _id";
                using var pillsReader = await pillsCmd.ExecuteReaderAsync();
                int pillIdx = 0;
                while (await pillsReader.ReadAsync())
                {
                    long pId = pillsReader.GetInt64(0);
                    string name = pillsReader.IsDBNull(1) ? "Unknown" : pillsReader.GetString(1).Trim();
                    long? size = pillsReader.IsDBNull(2) ? null : pillsReader.GetInt64(2);
                    long? colour = pillsReader.IsDBNull(3) ? null : pillsReader.GetInt64(3);
                    long? unitsId = pillsReader.IsDBNull(4) ? null : pillsReader.GetInt64(4);
                    long? defReminder = pillsReader.IsDBNull(5) ? null : pillsReader.GetInt64(5);

                    string medId = Guid.NewGuid().ToString();
                    pillIdToMedId[pId] = medId;
                    pillNames[pId] = name;

                    string? dosage = null;
                    if (size.HasValue && size.Value > 0)
                    {
                        string unitStr = (unitsId.HasValue && unitsMap.TryGetValue(unitsId.Value, out var u) && !string.IsNullOrWhiteSpace(u))
                            ? u
                            : "mg";
                        dosage = $"{size.Value}{unitStr}";
                    }

                    string colorHex = MapColor(colour, pillIdx++);
                    string? reminderStr = (defReminder.HasValue && defReminder.Value > 0)
                        ? $"Every {defReminder.Value}h"
                        : null;

                    medRows.Add((
                        medId,
                        name,
                        dosage,
                        colorHex,
                        null,
                        reminderStr,
                        null,
                        1,
                        DateTime.UtcNow.ToString("O")
                    ));
                }
            }

            // Fetch consumptions
            var intakeRows = new List<(string Id, string MedicationId, int Quantity, string TakenAt, string? Notes)>();
            var groupItemsMap = new Dictionary<string, List<(long PillId, int Quantity)>>();

            using (var consCmd = srcConn.CreateCommand())
            {
                consCmd.CommandText = "SELECT _id, pill_id, date_time, quantity, group_id FROM consumptions ORDER BY date_time ASC";
                using var consReader = await consCmd.ExecuteReaderAsync();
                while (await consReader.ReadAsync())
                {
                    long pillId = consReader.GetInt64(1);
                    if (!pillIdToMedId.TryGetValue(pillId, out var medId))
                    {
                        continue;
                    }

                    long ts = consReader.GetInt64(2);
                    int qty = consReader.IsDBNull(3) ? 1 : Math.Max(1, (int)consReader.GetInt64(3));
                    string? groupId = consReader.IsDBNull(4) ? null : consReader.GetString(4);

                    DateTime takenAt;
                    try
                    {
                        takenAt = DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime;
                    }
                    catch
                    {
                        takenAt = DateTime.Now;
                    }

                    intakeRows.Add((
                        Guid.NewGuid().ToString(),
                        medId,
                        qty,
                        takenAt.ToString("O"),
                        null
                    ));

                    if (!string.IsNullOrWhiteSpace(groupId))
                    {
                        if (!groupItemsMap.TryGetValue(groupId, out var list))
                        {
                            list = [];
                            groupItemsMap[groupId] = list;
                        }
                        list.Add((pillId, qty));
                    }
                }
            }

            // Calculate frequent multi-pill presets (DoseGroups)
            var patternCounts = new Dictionary<string, int>();
            var patternDetails = new Dictionary<string, List<(long PillId, int Quantity)>>();

            foreach (var (_, items) in groupItemsMap)
            {
                if (items.Count <= 1) continue;

                var aggregated = items
                    .GroupBy(x => x.PillId)
                    .Select(g => (PillId: g.Key, Quantity: g.Sum(x => x.Quantity)))
                    .OrderBy(x => x.PillId)
                    .ToList();

                if (aggregated.Count <= 1) continue;

                string key = string.Join(";", aggregated.Select(x => $"{x.PillId}:{x.Quantity}"));
                patternCounts[key] = patternCounts.GetValueOrDefault(key) + 1;
                patternDetails[key] = aggregated;
            }

            var frequentPatterns = patternCounts
                .Where(kvp => kvp.Value >= 10)
                .OrderByDescending(kvp => kvp.Value)
                .Take(4)
                .ToList();

            var doseGroupRows = new List<(string Id, string Name, string? Description, string? ColorHex, string CreatedAt, List<(string MedicationId, int Quantity)> Items)>();
            int presetIdx = 1;

            foreach (var (key, count) in frequentPatterns)
            {
                var pattern = patternDetails[key];
                string gId = Guid.NewGuid().ToString();
                string gName = presetIdx == 1 ? "Primary Routine" : $"Daily Routine {presetIdx}";
                string desc = string.Join(", ", pattern.Select(p => $"{p.Quantity}x {(pillNames.TryGetValue(p.PillId, out var pName) ? pName : "Med")}"));
                string colorHex = ColorPalette[(presetIdx + 2) % ColorPalette.Length];
                presetIdx++;

                var items = pattern.Select(p => (MedicationId: pillIdToMedId[p.PillId], Quantity: p.Quantity)).ToList();
                doseGroupRows.Add((gId, gName, desc, colorHex, DateTime.UtcNow.ToString("O"), items));
            }

            // Write to destination database
            using var dstConn = new SqliteConnection($"Data Source={targetDbPath}");
            await dstConn.OpenAsync();

            using var tx = dstConn.BeginTransaction();

            // Ensure destination tables exist
            using (var initCmd = dstConn.CreateCommand())
            {
                initCmd.Transaction = tx;
                initCmd.CommandText = @"
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
                ";
                await initCmd.ExecuteNonQueryAsync();
            }

            if (clearExistingData)
            {
                using var clearCmd = dstConn.CreateCommand();
                clearCmd.Transaction = tx;
                clearCmd.CommandText = @"
                    DELETE FROM DoseGroupItems;
                    DELETE FROM DoseGroups;
                    DELETE FROM MedicationIntakes;
                    DELETE FROM Medications;
                ";
                await clearCmd.ExecuteNonQueryAsync();
            }

            // Insert Medications
            using (var insertMedCmd = dstConn.CreateCommand())
            {
                insertMedCmd.Transaction = tx;
                insertMedCmd.CommandText = @"
                    INSERT INTO Medications (Id, Name, Dosage, ColorHex, Schedule, ReminderTime, Notes, IsActive, CreatedAt)
                    VALUES (@Id, @Name, @Dosage, @ColorHex, @Schedule, @ReminderTime, @Notes, @IsActive, @CreatedAt)
                ";
                var pId = insertMedCmd.Parameters.Add("@Id", SqliteType.Text);
                var pName = insertMedCmd.Parameters.Add("@Name", SqliteType.Text);
                var pDosage = insertMedCmd.Parameters.Add("@Dosage", SqliteType.Text);
                var pColor = insertMedCmd.Parameters.Add("@ColorHex", SqliteType.Text);
                var pSchedule = insertMedCmd.Parameters.Add("@Schedule", SqliteType.Text);
                var pReminder = insertMedCmd.Parameters.Add("@ReminderTime", SqliteType.Text);
                var pNotes = insertMedCmd.Parameters.Add("@Notes", SqliteType.Text);
                var pIsActive = insertMedCmd.Parameters.Add("@IsActive", SqliteType.Integer);
                var pCreatedAt = insertMedCmd.Parameters.Add("@CreatedAt", SqliteType.Text);

                foreach (var row in medRows)
                {
                    pId.Value = row.Id;
                    pName.Value = row.Name;
                    pDosage.Value = (object?)row.Dosage ?? DBNull.Value;
                    pColor.Value = row.ColorHex;
                    pSchedule.Value = (object?)row.Schedule ?? DBNull.Value;
                    pReminder.Value = (object?)row.ReminderTime ?? DBNull.Value;
                    pNotes.Value = (object?)row.Notes ?? DBNull.Value;
                    pIsActive.Value = row.IsActive;
                    pCreatedAt.Value = row.CreatedAt;
                    await insertMedCmd.ExecuteNonQueryAsync();
                }
            }

            // Insert MedicationIntakes
            using (var insertIntakeCmd = dstConn.CreateCommand())
            {
                insertIntakeCmd.Transaction = tx;
                insertIntakeCmd.CommandText = @"
                    INSERT INTO MedicationIntakes (Id, MedicationId, Quantity, TakenAt, Notes)
                    VALUES (@Id, @MedicationId, @Quantity, @TakenAt, @Notes)
                ";
                var pId = insertIntakeCmd.Parameters.Add("@Id", SqliteType.Text);
                var pMedId = insertIntakeCmd.Parameters.Add("@MedicationId", SqliteType.Text);
                var pQty = insertIntakeCmd.Parameters.Add("@Quantity", SqliteType.Integer);
                var pTakenAt = insertIntakeCmd.Parameters.Add("@TakenAt", SqliteType.Text);
                var pNotes = insertIntakeCmd.Parameters.Add("@Notes", SqliteType.Text);

                foreach (var row in intakeRows)
                {
                    pId.Value = row.Id;
                    pMedId.Value = row.MedicationId;
                    pQty.Value = row.Quantity;
                    pTakenAt.Value = row.TakenAt;
                    pNotes.Value = (object?)row.Notes ?? DBNull.Value;
                    await insertIntakeCmd.ExecuteNonQueryAsync();
                }
            }

            // Insert DoseGroups and DoseGroupItems
            using (var insertGroupCmd = dstConn.CreateCommand())
            using (var insertItemCmd = dstConn.CreateCommand())
            {
                insertGroupCmd.Transaction = tx;
                insertGroupCmd.CommandText = @"
                    INSERT INTO DoseGroups (Id, Name, Description, ColorHex, CreatedAt)
                    VALUES (@Id, @Name, @Description, @ColorHex, @CreatedAt)
                ";
                var gId = insertGroupCmd.Parameters.Add("@Id", SqliteType.Text);
                var gName = insertGroupCmd.Parameters.Add("@Name", SqliteType.Text);
                var gDesc = insertGroupCmd.Parameters.Add("@Description", SqliteType.Text);
                var gColor = insertGroupCmd.Parameters.Add("@ColorHex", SqliteType.Text);
                var gCreated = insertGroupCmd.Parameters.Add("@CreatedAt", SqliteType.Text);

                insertItemCmd.Transaction = tx;
                insertItemCmd.CommandText = @"
                    INSERT INTO DoseGroupItems (Id, DoseGroupId, MedicationId, Quantity)
                    VALUES (@Id, @DoseGroupId, @MedicationId, @Quantity)
                ";
                var itId = insertItemCmd.Parameters.Add("@Id", SqliteType.Text);
                var itGroupId = insertItemCmd.Parameters.Add("@DoseGroupId", SqliteType.Text);
                var itMedId = insertItemCmd.Parameters.Add("@MedicationId", SqliteType.Text);
                var itQty = insertItemCmd.Parameters.Add("@Quantity", SqliteType.Integer);

                foreach (var g in doseGroupRows)
                {
                    gId.Value = g.Id;
                    gName.Value = g.Name;
                    gDesc.Value = (object?)g.Description ?? DBNull.Value;
                    gColor.Value = (object?)g.ColorHex ?? DBNull.Value;
                    gCreated.Value = g.CreatedAt;
                    await insertGroupCmd.ExecuteNonQueryAsync();

                    foreach (var it in g.Items)
                    {
                        itId.Value = Guid.NewGuid().ToString();
                        itGroupId.Value = g.Id;
                        itMedId.Value = it.MedicationId;
                        itQty.Value = it.Quantity;
                        await insertItemCmd.ExecuteNonQueryAsync();
                    }
                }
            }

            await tx.CommitAsync();

            return new PillLoggerImportResult(
                true,
                medRows.Count,
                intakeRows.Count,
                doseGroupRows.Count
            );
        }
        catch (Exception ex)
        {
            return new PillLoggerImportResult(false, 0, 0, 0, ex.Message);
        }
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@TableName";
        cmd.Parameters.AddWithValue("@TableName", tableName);
        var result = await cmd.ExecuteScalarAsync();
        return result is long count && count > 0;
    }
}