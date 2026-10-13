using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiveChartsCore.SkiaSharpView;
using DrTinyPaws.Models;
using DrTinyPaws.Services;
using DrTinyPaws.ViewModels;
using ReactiveUI;
using System.Reactive.Threading.Tasks;
using Xunit;

namespace DrTinyPaws.Tests;

public class DrTinyPawsTests
{
    [Fact]
    public void WeeklyChartViewModel_WeekStartsOnMonday()
    {
        var vm = new WeeklyChartViewModel();
        Assert.Equal(DayOfWeek.Monday, vm.WeekStartDate.DayOfWeek);
    }

    [Fact]
    public void WeeklyChartViewModel_Navigation_MovesBy7Days()
    {
        var vm = new WeeklyChartViewModel();
        var initialStart = vm.WeekStartDate;

        vm.PreviousWeek();
        Assert.Equal(initialStart.AddDays(-7), vm.WeekStartDate);
        Assert.Equal(DayOfWeek.Monday, vm.WeekStartDate.DayOfWeek);

        vm.NextWeek();
        Assert.Equal(initialStart, vm.WeekStartDate);

        vm.NextWeek();
        Assert.Equal(initialStart.AddDays(7), vm.WeekStartDate);

        vm.CurrentWeek();
        Assert.Equal(initialStart, vm.WeekStartDate);
    }

    [Fact]
    public void WeeklyChartViewModel_UpdateData_GeneratesStackedColumnsWithCorrectCounts()
    {
        var vm = new WeeklyChartViewModel();
        var monday = vm.WeekStartDate;

        var medBlue = new Medication { Id = "med-blue", Name = "Panadeine", ColorHex = "#2563EB" };
        var medRed = new Medication { Id = "med-red", Name = "Anti-histamine", ColorHex = "#DC2626" };

        var intakes = new List<MedicationIntake>
        {
            // Monday: 2 blue, 1 red
            new() { MedicationId = medBlue.Id, Quantity = 2, TakenAt = monday.AddHours(9) },
            new() { MedicationId = medRed.Id, Quantity = 1, TakenAt = monday.AddHours(21) },
            // Wednesday (Monday + 2 days): 3 blue
            new() { MedicationId = medBlue.Id, Quantity = 3, TakenAt = monday.AddDays(2).AddHours(8) }
        };

        vm.UpdateData([medBlue, medRed], intakes);

        Assert.Equal(6, vm.TotalWeeklyDoses);
        Assert.NotEmpty(vm.Series);

        var blueSeries = vm.Series.OfType<StackedColumnSeries<int>>().FirstOrDefault(s => s.Name == "Panadeine");
        var redSeries = vm.Series.OfType<StackedColumnSeries<int>>().FirstOrDefault(s => s.Name == "Anti-histamine");

        Assert.NotNull(blueSeries);
        Assert.NotNull(redSeries);

        var blueVals = blueSeries.Values!.ToArray();
        var redVals = redSeries.Values!.ToArray();

        Assert.Equal(7, blueVals.Length);
        Assert.Equal(7, redVals.Length);

        // Day 0 = Monday
        Assert.Equal(2, blueVals[0]);
        Assert.Equal(1, redVals[0]);

        // Day 1 = Tuesday
        Assert.Equal(0, blueVals[1]);

        // Day 2 = Wednesday
        Assert.Equal(3, blueVals[2]);
    }

    [Fact]
    public async Task SqliteDataStore_CrudOperations_Succeed()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"drtinypaws_test_{Guid.NewGuid()}.db");
        var store = new SqliteDataStore();
        
        try
        {
            await store.SetDatabasePathAsync(tempDbPath, seedSampleData: false);

            // Test Medication CRUD
            var med = new Medication
            {
                Name = "Paracetamol",
                Dosage = "500mg",
                ColorHex = "#3B82F6",
                Schedule = "Daily",
                ReminderTime = "08:00",
                Notes = "With meals"
            };

            await store.SaveMedicationAsync(med);

            var fetched = await store.GetMedicationByIdAsync(med.Id);
            Assert.NotNull(fetched);
            Assert.Equal("Paracetamol", fetched.Name);
            Assert.Equal("500mg", fetched.Dosage);
            Assert.Equal("#3B82F6", fetched.ColorHex);
            Assert.Equal("08:00", fetched.ReminderTime);

            // Test Intake Logging
            var now = DateTime.Now;
            var intake1 = new MedicationIntake
            {
                MedicationId = med.Id,
                Quantity = 2,
                TakenAt = now
            };
            var intake2 = new MedicationIntake
            {
                MedicationId = med.Id,
                Quantity = 1,
                TakenAt = now.AddMinutes(30)
            };

            await store.LogIntakesAsync([intake1, intake2]);

            var recent = await store.GetRecentIntakesAsync(limit: 10);
            Assert.True(recent.Count >= 2);
            Assert.Equal(intake2.Id, recent[0].Id); // Most recent first
            Assert.Equal("Paracetamol", recent[0].MedicationName);
            Assert.Equal("#3B82F6", recent[0].MedicationColorHex);

            // Test Dose Groups
            var group = new DoseGroup
            {
                Name = "Morning Dose",
                Description = "2 Paracetamol",
                ColorHex = "#10B981",
                Items =
                [
                    new DoseGroupItem { MedicationId = med.Id, Quantity = 2 }
                ]
            };

            await store.SaveDoseGroupAsync(group);

            var groups = await store.GetDoseGroupsAsync();
            var savedGroup = groups.FirstOrDefault(g => g.Id == group.Id);
            Assert.NotNull(savedGroup);
            Assert.Equal("Morning Dose", savedGroup.Name);
            Assert.Single(savedGroup.Items);
            Assert.Equal(2, savedGroup.Items[0].Quantity);

            // Test Delete Intake
            await store.DeleteIntakeAsync(intake1.Id);
            var afterDeleteIntakes = await store.GetRecentIntakesAsync(limit: 10);
            Assert.DoesNotContain(afterDeleteIntakes, i => i.Id == intake1.Id);

            // Test Delete Medication (soft delete)
            await store.DeleteMedicationAsync(med.Id);
            var activeMeds = await store.GetMedicationsAsync(includeInactive: false);
            Assert.DoesNotContain(activeMeds, m => m.Id == med.Id);
        }
        finally
        {
            store.Dispose();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task TakeMedicationViewModel_GroupPresetApplication_PrepopulatesQuantities()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"drtinypaws_test_{Guid.NewGuid()}.db");
        var store = new SqliteDataStore();

        try
        {
            await store.SetDatabasePathAsync(tempDbPath, seedSampleData: false);

            var med1 = new Medication { Name = "Aspirin", ColorHex = "#EF4444" };
            var med2 = new Medication { Name = "Vitamin C", ColorHex = "#F59E0B" };
            await store.SaveMedicationAsync(med1);
            await store.SaveMedicationAsync(med2);

            var group = new DoseGroup
            {
                Name = "Morning Routine",
                Items =
                [
                    new DoseGroupItem { MedicationId = med1.Id, Quantity = 3 },
                    new DoseGroupItem { MedicationId = med2.Id, Quantity = 2 }
                ]
            };
            await store.SaveDoseGroupAsync(group);

            var vm = new TakeMedicationViewModel(store);
            await vm.InitializeAsync();

            Assert.Equal(2, vm.MedItems.Count);
            Assert.All(vm.MedItems, m => Assert.False(m.IsSelected));

            // Apply Group
            vm.ApplyGroup(group);

            var item1 = vm.MedItems.First(m => m.Medication.Id == med1.Id);
            var item2 = vm.MedItems.First(m => m.Medication.Id == med2.Id);

            Assert.True(item1.IsSelected);
            Assert.Equal(3, item1.Quantity);

            Assert.True(item2.IsSelected);
            Assert.Equal(2, item2.Quantity);

            // Record Dose
            bool savedEventFired = false;
            vm.Saved += () => savedEventFired = true;

            await vm.RecordDoseAsync();
            Assert.True(savedEventFired);

            var recorded = await store.GetRecentIntakesAsync(limit: 10);
            Assert.Equal(2, recorded.Count);
            Assert.Contains(recorded, r => r.MedicationId == med1.Id && r.Quantity == 3);
            Assert.Contains(recorded, r => r.MedicationId == med2.Id && r.Quantity == 2);
        }
        finally
        {
            store.Dispose();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public void WeeklyChartViewModel_WeekStart_HandlesYearBoundary()
    {
        var vm = new WeeklyChartViewModel
        {
            WeekStartDate = new DateTime(2026, 12, 28) // Monday
        };

        Assert.Equal(DayOfWeek.Monday, vm.WeekStartDate.DayOfWeek);
        vm.NextWeek();
        Assert.Equal(new DateTime(2027, 1, 4), vm.WeekStartDate);
        Assert.Equal(DayOfWeek.Monday, vm.WeekStartDate.DayOfWeek);
    }

    [Fact]
    public async Task SqliteDataStore_UpdateDoseGroup_OverwritesItemsCorrectly()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"drtinypaws_test_{Guid.NewGuid()}.db");
        var store = new SqliteDataStore();

        try
        {
            await store.SetDatabasePathAsync(tempDbPath, seedSampleData: false);

            var med1 = new Medication { Name = "Med A", ColorHex = "#10B981" };
            var med2 = new Medication { Name = "Med B", ColorHex = "#3B82F6" };
            await store.SaveMedicationAsync(med1);
            await store.SaveMedicationAsync(med2);

            var group = new DoseGroup
            {
                Name = "Test Preset",
                Items =
                [
                    new DoseGroupItem { MedicationId = med1.Id, Quantity = 1 }
                ]
            };
            await store.SaveDoseGroupAsync(group);

            // Update group with new items
            group.Name = "Updated Preset";
            group.Items =
            [
                new DoseGroupItem { MedicationId = med2.Id, Quantity = 4 }
            ];
            await store.SaveDoseGroupAsync(group);

            var fetched = await store.GetDoseGroupByIdAsync(group.Id);
            Assert.NotNull(fetched);
            Assert.Equal("Updated Preset", fetched.Name);
            Assert.Single(fetched.Items);
            Assert.Equal(med2.Id, fetched.Items[0].MedicationId);
            Assert.Equal(4, fetched.Items[0].Quantity);
        }
        finally
        {
            store.Dispose();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task MainViewModel_Router_ManagesNavigationStackCorrectly()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"drtinypaws_nav_test_{Guid.NewGuid()}.db");
        var store = new SqliteDataStore();

        try
        {
            await store.SetDatabasePathAsync(tempDbPath, seedSampleData: false);
            var notif = new DefaultNotificationService();
            var mainVm = new MainViewModel(store, notif);

            // Initially, Router should have DashboardViewModel at the root
            Assert.Single(mainVm.Router.NavigationStack);
            Assert.IsType<DashboardViewModel>(mainVm.Router.NavigationStack[0]);

            // 1. Navigate to Take Medication via Dashboard command
            await mainVm.Dashboard.OpenTakeMedicationCommand.Execute().ToTask();
            Assert.Equal(2, mainVm.Router.NavigationStack.Count);
            Assert.IsType<TakeMedicationViewModel>(mainVm.Router.NavigationStack[1]);

            // Cancel / Go back
            var takeVm = (TakeMedicationViewModel)mainVm.Router.NavigationStack[1];
            takeVm.CancelCommand.Execute().Subscribe();
            Assert.Single(mainVm.Router.NavigationStack);
            Assert.IsType<DashboardViewModel>(mainVm.Router.NavigationStack[0]);

            // 2. Navigate to Manage Medications
            await mainVm.Dashboard.OpenManageMedicationsCommand.Execute().ToTask();
            Assert.Equal(2, mainVm.Router.NavigationStack.Count);
            Assert.IsType<MedicationManagementViewModel>(mainVm.Router.NavigationStack[1]);

            // From Manage Medications, request Add Medication
            var manageMedsVm = (MedicationManagementViewModel)mainVm.Router.NavigationStack[1];
            manageMedsVm.RequestAddMedicationCommand.Execute().Subscribe();
            Assert.Equal(3, mainVm.Router.NavigationStack.Count);
            Assert.IsType<MedicationEditViewModel>(mainVm.Router.NavigationStack[2]);

            // Cancel Add Medication returns to Manage Medications
            var addMedVm = (MedicationEditViewModel)mainVm.Router.NavigationStack[2];
            addMedVm.CancelCommand.Execute().Subscribe();
            Assert.Equal(2, mainVm.Router.NavigationStack.Count);
            Assert.IsType<MedicationManagementViewModel>(mainVm.Router.NavigationStack[1]);

            // Close Manage Medications returns to Dashboard
            manageMedsVm.CloseCommand.Execute().Subscribe();
            Assert.Single(mainVm.Router.NavigationStack);
            Assert.IsType<DashboardViewModel>(mainVm.Router.NavigationStack[0]);

            // 3. Navigate to Settings
            mainVm.Dashboard.OpenSettingsCommand.Execute().Subscribe();
            Assert.Equal(2, mainVm.Router.NavigationStack.Count);
            Assert.IsType<SettingsViewModel>(mainVm.Router.NavigationStack[1]);

            var settingsVm = (SettingsViewModel)mainVm.Router.NavigationStack[1];
            settingsVm.CancelCommand.Execute().Subscribe();
            Assert.Single(mainVm.Router.NavigationStack);
            Assert.IsType<DashboardViewModel>(mainVm.Router.NavigationStack[0]);

            // 4. Navigate to Manage Groups
            await mainVm.Dashboard.OpenManageGroupsCommand.Execute().ToTask();
            Assert.Equal(2, mainVm.Router.NavigationStack.Count);
            Assert.IsType<DoseGroupManagementViewModel>(mainVm.Router.NavigationStack[1]);

            var groupsVm = (DoseGroupManagementViewModel)mainVm.Router.NavigationStack[1];
            groupsVm.CloseCommand.Execute().Subscribe();
            Assert.Single(mainVm.Router.NavigationStack);
            Assert.IsType<DashboardViewModel>(mainVm.Router.NavigationStack[0]);
        }
        finally
        {
            store.Dispose();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task PillLoggerImporter_WithSamplePillLoggerDb_ImportsSuccessfully()
    {
        var samplePillLoggerDb = Path.Combine(Path.GetTempPath(), $"sample_pilllogger_{Guid.NewGuid():N}.db");
        var targetDb = Path.Combine(Path.GetTempPath(), $"drtinypaws_target_{Guid.NewGuid():N}.db");

        try
        {
            await CreateMockPillLoggerDbAsync(samplePillLoggerDb);

            var importer = new PillLoggerImporter();
            var result = await importer.ImportAsync(samplePillLoggerDb, targetDb, clearExistingData: true);

            Assert.True(result.Success);
            Assert.Equal(2, result.MedicationsImported);
            Assert.Equal(24, result.IntakesImported);
            Assert.Equal(1, result.DoseGroupsImported);

            // Verify content in target datastore
            using var store = new SqliteDataStore();
            await store.SetDatabasePathAsync(targetDb, seedSampleData: false);

            var meds = await store.GetMedicationsAsync(includeInactive: true);
            Assert.Equal(2, meds.Count);
            var med1 = meds.First(m => m.Name == "Paracetamol");
            Assert.Equal("500mg", med1.Dosage);
            Assert.Equal("Every 4h", med1.ReminderTime);

            var groups = await store.GetDoseGroupsAsync();
            Assert.Single(groups);
            Assert.Equal("Primary Routine", groups[0].Name);
            Assert.Equal(2, groups[0].Items.Count);

            var intakes = await store.GetRecentIntakesAsync(limit: 50);
            Assert.Equal(24, intakes.Count);
        }
        finally
        {
            if (File.Exists(samplePillLoggerDb)) try { File.Delete(samplePillLoggerDb); } catch { }
            if (File.Exists(targetDb)) try { File.Delete(targetDb); } catch { }
        }
    }

    [Fact]
    public async Task PillLoggerImporter_WithInvalidDb_FailsGracefully()
    {
        var invalidDb = Path.Combine(Path.GetTempPath(), $"invalid_db_{Guid.NewGuid():N}.db");
        var targetDb = Path.Combine(Path.GetTempPath(), $"drtinypaws_target_{Guid.NewGuid():N}.db");

        try
        {
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={invalidDb}"))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE TABLE some_random_table (id INTEGER);";
                await cmd.ExecuteNonQueryAsync();
            }

            var importer = new PillLoggerImporter();
            var result = await importer.ImportAsync(invalidDb, targetDb);

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("missing 'pills' or 'consumptions'", result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(invalidDb)) try { File.Delete(invalidDb); } catch { }
            if (File.Exists(targetDb)) try { File.Delete(targetDb); } catch { }
        }
    }

    [Fact]
    public async Task SettingsViewModel_ImportCommand_UserCancels_DoesNotChangeState()
    {
        var targetDb = Path.Combine(Path.GetTempPath(), $"drtinypaws_target_{Guid.NewGuid():N}.db");
        using var store = new SqliteDataStore();

        try
        {
            await store.SetDatabasePathAsync(targetDb, seedSampleData: false);
            var vm = new SettingsViewModel(store);

            // Register interaction handler returning null (dialog cancelled)
            using var d = vm.OpenPillLoggerFileDialog.RegisterHandler(interaction =>
            {
                interaction.SetOutput(null);
            });

            await vm.ImportPillLoggerCommand.Execute().ToTask();

            Assert.False(vm.IsImporting);
            Assert.False(vm.ImportSuccess);
            Assert.Empty(vm.ImportStatusMessage);
        }
        finally
        {
            if (File.Exists(targetDb)) try { File.Delete(targetDb); } catch { }
        }
    }

    [Fact]
    public async Task SettingsViewModel_ImportCommand_SucceedsWithMockDb()
    {
        var samplePillLoggerDb = Path.Combine(Path.GetTempPath(), $"sample_pilllogger_{Guid.NewGuid():N}.db");
        var targetDb = Path.Combine(Path.GetTempPath(), $"drtinypaws_target_{Guid.NewGuid():N}.db");
        using var store = new SqliteDataStore();

        try
        {
            await CreateMockPillLoggerDbAsync(samplePillLoggerDb);
            await store.SetDatabasePathAsync(targetDb, seedSampleData: false);

            var vm = new SettingsViewModel(store);

            // Register interaction handler returning the mock db path
            using var d = vm.OpenPillLoggerFileDialog.RegisterHandler(interaction =>
            {
                interaction.SetOutput(samplePillLoggerDb);
            });

            await vm.ImportPillLoggerCommand.Execute().ToTask();

            Assert.False(vm.IsImporting);
            Assert.True(vm.ImportSuccess);
            Assert.Contains("Successfully imported 2 medications", vm.ImportStatusMessage);
        }
        finally
        {
            if (File.Exists(samplePillLoggerDb)) try { File.Delete(samplePillLoggerDb); } catch { }
            if (File.Exists(targetDb)) try { File.Delete(targetDb); } catch { }
        }
    }

    [Fact]
    public async Task PillLoggerImporter_WithActualPillLoggerDb_IfPresent_ImportsAllRecords()
    {
        const string actualDb = @"d:\Utils\scrcpy-win64-v1.24\pilllogger\apps\uk.co.pilllogger\db\PillLogger.db";
        if (!File.Exists(actualDb)) return;

        var targetDb = Path.Combine(Path.GetTempPath(), $"drtinypaws_actual_import_{Guid.NewGuid():N}.db");
        using var store = new SqliteDataStore();

        try
        {
            await store.SetDatabasePathAsync(targetDb, seedSampleData: false);
            var result = await store.ImportPillLoggerDatabaseAsync(actualDb, clearExistingData: true);

            Assert.True(result.Success);
            Assert.Equal(19, result.MedicationsImported);
            Assert.Equal(10861, result.IntakesImported);
            Assert.Equal(4, result.DoseGroupsImported);
        }
        finally
        {
            if (File.Exists(targetDb)) try { File.Delete(targetDb); } catch { }
        }
    }

    private static async Task CreateMockPillLoggerDbAsync(string dbPath)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE units (_id INTEGER PRIMARY KEY, name TEXT);
            INSERT INTO units (_id, name) VALUES (1, 'mg'), (2, 'ml');

            CREATE TABLE pills (_id INTEGER PRIMARY KEY, name TEXT, size INTEGER, colour INTEGER, favourite INTEGER, units_id INTEGER, default_reminder INTEGER, user_id INTEGER DEFAULT 1);
            INSERT INTO pills (_id, name, size, colour, favourite, units_id, default_reminder)
            VALUES (1, 'Paracetamol', 500, 1694485504, 0, 1, 4),
                   (2, 'Ibuprofen', 200, 0, 0, 1, 0);

            CREATE TABLE consumptions (_id INTEGER PRIMARY KEY, pill_id INTEGER, date_time LONG, quantity INTEGER, group_id TEXT, reminder_date_time LONG, user_id INTEGER DEFAULT 1);
        ";
        await cmd.ExecuteNonQueryAsync();

        for (int i = 0; i < 12; i++)
        {
            var gid = Guid.NewGuid().ToString();
            var ts = DateTimeOffset.UtcNow.AddHours(-i).ToUnixTimeMilliseconds();
            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = $@"
                INSERT INTO consumptions (pill_id, date_time, quantity, group_id) VALUES (1, {ts}, 2, '{gid}');
                INSERT INTO consumptions (pill_id, date_time, quantity, group_id) VALUES (2, {ts}, 1, '{gid}');
            ";
            await insertCmd.ExecuteNonQueryAsync();
        }
    }
}

