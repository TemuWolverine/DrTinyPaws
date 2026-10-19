using DrTinyPaws.Models;
using DrTinyPaws.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DrTinyPaws.ViewModels;

public partial class TakeMedicationViewModel : ViewModelBase
{
    private readonly IDataStore _dataStore;
    public event Action? Cancelled;
    public event Action? Saved;
    public ObservableCollection<DoseGroup> AvailableGroups { get; } = [];
    [Reactive] public partial string ErrorMessage { get; set; } = string.Empty;
    public ObservableCollection<TakeMedicationEntryItem> MedItems { get; } = [];
    [Reactive] public partial string NewGroupDescription { get; set; } = string.Empty;
    [Reactive] public partial string NewGroupName { get; set; } = string.Empty;
    [Reactive] public partial string Notes { get; set; } = string.Empty;
    [Reactive] public partial bool ShowCreateGroupPanel { get; set; }
    [Reactive] public partial DateTimeOffset TakenDate { get; set; } = DateTimeOffset.Now;
    [Reactive] public partial TimeSpan TakenTime { get; set; } = DateTime.Now.TimeOfDay;

    public TakeMedicationViewModel(IDataStore dataStore, DrTinyPaws.Services.INavigationService? navigationService = null) : base(navigationService)
    {
        _dataStore = dataStore;
    }

    [ReactiveCommand]
    public void ApplyGroup(DoseGroup group)
    {
        // First reset all items
        foreach (var item in MedItems)
        {
            item.IsSelected = false;
            item.Quantity = 1;
        }

        // Apply quantities from group
        foreach (var gItem in group.Items)
        {
            var match = MedItems.FirstOrDefault(m => m.Medication.Id == gItem.MedicationId);
            if (match != null)
            {
                match.IsSelected = true;
                match.Quantity = gItem.Quantity;
            }
        }
    }

    [ReactiveCommand]
    public void Cancel()
    {
        Cancelled?.Invoke();
        _ = NavigationService?.GoBackAsync();
    }

    public async Task InitializeAsync(string? selectedMedicationId = null)
    {
        var meds = await _dataStore.GetMedicationsAsync(includeInactive: false);
        MedItems.Clear();

        if (!string.IsNullOrEmpty(selectedMedicationId))
        {
            var selectedMed = meds.FirstOrDefault(m => m.Id == selectedMedicationId);
            if (selectedMed != null)
            {
                MedItems.Add(new TakeMedicationEntryItem(selectedMed, quantity: 1, isSelected: true));
            }

            foreach (var med in meds.Where(m => m.Id != selectedMedicationId))
            {
                MedItems.Add(new TakeMedicationEntryItem(med, quantity: 1, isSelected: false));
            }
        }
        else
        {
            foreach (var med in meds)
            {
                MedItems.Add(new TakeMedicationEntryItem(med, quantity: 1, isSelected: false));
            }
        }

        var groups = await _dataStore.GetDoseGroupsAsync();
        AvailableGroups.Clear();
        foreach (var g in groups)
        {
            AvailableGroups.Add(g);
        }
    }

    [ReactiveCommand]
    public async Task RecordDoseAsync()
    {
        var selected = MedItems.Where(i => i.IsSelected && i.Quantity > 0).ToList();
        if (selected.Count == 0)
        {
            ErrorMessage = "Please select at least one medication to record.";
            return;
        }

        ErrorMessage = string.Empty;

        var timestamp = TakenDate.Date.Add(TakenTime);

        var intakes = selected.Select(item => new MedicationIntake
        {
            MedicationId = item.Medication.Id,
            Quantity = item.Quantity,
            TakenAt = timestamp,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
        }).ToList();

        await _dataStore.LogIntakesAsync(intakes);
        Saved?.Invoke();
        if (NavigationService != null)
        {
            await NavigationService.GoBackAsync();
        }
    }

    [ReactiveCommand]
    public async Task SaveSelectionAsGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGroupName))
        {
            ErrorMessage = "Please enter a group name.";
            return;
        }

        var selectedItems = MedItems.Where(i => i.IsSelected && i.Quantity > 0).ToList();
        if (selectedItems.Count == 0)
        {
            ErrorMessage = "Select at least one medication to create a group.";
            return;
        }

        var newGroup = new DoseGroup
        {
            Name = NewGroupName.Trim(),
            Description = string.IsNullOrWhiteSpace(NewGroupDescription) ? null : NewGroupDescription.Trim(),
            Items = selectedItems.Select(s => new DoseGroupItem
            {
                MedicationId = s.Medication.Id,
                Quantity = s.Quantity
            }).ToList()
        };

        await _dataStore.SaveDoseGroupAsync(newGroup);
        AvailableGroups.Add(newGroup);
        ShowCreateGroupPanel = false;
        NewGroupName = string.Empty;
        NewGroupDescription = string.Empty;
        ErrorMessage = string.Empty;
    }

    [ReactiveCommand]
    public void ToggleCreateGroup()
    {
        ShowCreateGroupPanel = !ShowCreateGroupPanel;
        if (ShowCreateGroupPanel && string.IsNullOrWhiteSpace(NewGroupName))
        {
            NewGroupName = "Custom Preset";
        }
    }
}