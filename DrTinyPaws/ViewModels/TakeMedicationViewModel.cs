using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrTinyPaws.Models;
using DrTinyPaws.Services;

namespace DrTinyPaws.ViewModels;

public partial class TakeMedicationViewModel : ViewModelBase
{
    private readonly IDataStore _dataStore;

    [ObservableProperty]
    private DateTimeOffset _takenDate = DateTimeOffset.Now;

    [ObservableProperty]
    private TimeSpan _takenTime = DateTime.Now.TimeOfDay;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _showCreateGroupPanel;

    [ObservableProperty]
    private string _newGroupName = string.Empty;

    [ObservableProperty]
    private string _newGroupDescription = string.Empty;

    public ObservableCollection<TakeMedicationEntryItem> MedItems { get; } = [];
    public ObservableCollection<DoseGroup> AvailableGroups { get; } = [];

    public event Action? Saved;
    public event Action? Cancelled;

    public TakeMedicationViewModel(IDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public async Task InitializeAsync()
    {
        var meds = await _dataStore.GetMedicationsAsync(includeInactive: false);
        MedItems.Clear();
        foreach (var med in meds)
        {
            MedItems.Add(new TakeMedicationEntryItem(med, quantity: 1, isSelected: false));
        }

        var groups = await _dataStore.GetDoseGroupsAsync();
        AvailableGroups.Clear();
        foreach (var g in groups)
        {
            AvailableGroups.Add(g);
        }
    }

    [RelayCommand]
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

    [RelayCommand]
    public void ToggleCreateGroup()
    {
        ShowCreateGroupPanel = !ShowCreateGroupPanel;
        if (ShowCreateGroupPanel && string.IsNullOrWhiteSpace(NewGroupName))
        {
            NewGroupName = "Custom Preset";
        }
    }

    [RelayCommand]
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

    [RelayCommand]
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
    }

    [RelayCommand]
    public void Cancel()
    {
        Cancelled?.Invoke();
    }
}
