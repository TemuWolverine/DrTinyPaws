using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiveChartsCore.SkiaSharpView;
using DrTinyPaws.Models;
using DrTinyPaws.Services;
using DrTinyPaws.ViewModels;
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
    public async Task MainViewModel_PopupContent_ManagesStateAndCommandsCorrectly()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"drtinypaws_popup_test_{Guid.NewGuid()}.db");
        var store = new SqliteDataStore();

        try
        {
            await store.SetDatabasePathAsync(tempDbPath, seedSampleData: false);
            var notif = new DefaultNotificationService();
            var mainVm = new MainViewModel(store, notif);

            Assert.Null(mainVm.PopupContent);
            Assert.False(mainVm.IsPopupOpen);

            // 1. Open Take Medication
            await mainVm.OpenTakeMedicationAsync();
            Assert.NotNull(mainVm.PopupContent);
            Assert.IsType<TakeMedicationViewModel>(mainVm.PopupContent);
            Assert.True(mainVm.IsPopupOpen);

            // Light dismiss emulation
            mainVm.IsPopupOpen = false;
            Assert.Null(mainVm.PopupContent);

            // 2. Open Add Medication
            mainVm.OpenAddMedication();
            Assert.NotNull(mainVm.PopupContent);
            Assert.IsType<MedicationEditViewModel>(mainVm.PopupContent);
            Assert.True(mainVm.IsPopupOpen);

            mainVm.ClosePopup();
            Assert.Null(mainVm.PopupContent);
            Assert.False(mainVm.IsPopupOpen);

            // 3. Open Manage Medications
            await mainVm.OpenManageMedicationsAsync();
            Assert.NotNull(mainVm.PopupContent);
            Assert.IsType<MedicationManagementViewModel>(mainVm.PopupContent);
            Assert.True(mainVm.IsPopupOpen);

            // 4. Open Settings
            mainVm.OpenSettings();
            Assert.NotNull(mainVm.PopupContent);
            Assert.IsType<SettingsViewModel>(mainVm.PopupContent);
            Assert.True(mainVm.IsPopupOpen);

            // 5. Open Manage Groups
            await mainVm.OpenManageGroupsAsync();
            Assert.NotNull(mainVm.PopupContent);
            Assert.IsType<DoseGroupManagementViewModel>(mainVm.PopupContent);
            Assert.True(mainVm.IsPopupOpen);

            mainVm.ClosePopup();
            Assert.Null(mainVm.PopupContent);
            Assert.False(mainVm.IsPopupOpen);
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
}
