using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrTinyPaws.Models;
using DrTinyPaws.Services;

namespace DrTinyPaws.ViewModels;

public partial class DoseGroupManagementViewModel : ViewModelBase
{
    private readonly IDataStore _dataStore;

    [ObservableProperty]
    private string _groupId = string.Empty;

    [ObservableProperty]
    private string _groupName = string.Empty;

    [ObservableProperty]
    private string _groupDescription = string.Empty;

    [ObservableProperty]
    private string _groupColorHex = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public ObservableCollection<DoseGroup> Groups { get; } = [];
    public ObservableCollection<DoseGroupItemEdit> MedicationSelection { get; } = [];

    public event Action? Saved;
    public event Action? Cancelled;

    public DoseGroupManagementViewModel(IDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public async Task InitializeAsync()
    {
        await LoadGroupsAsync();
    }

    public async Task LoadGroupsAsync()
    {
        var groups = await _dataStore.GetDoseGroupsAsync();
        Groups.Clear();
        foreach (var g in groups)
        {
            Groups.Add(g);
        }
    }

    [RelayCommand]
    public async Task StartNewGroupAsync()
    {
        GroupId = Guid.NewGuid().ToString();
        GroupName = string.Empty;
        GroupDescription = string.Empty;
        GroupColorHex = string.Empty;
        IsEditing = true;
        ErrorMessage = string.Empty;

        await LoadMedicationsForEditAsync(null);
    }

    [RelayCommand]
    public async Task EditGroupAsync(DoseGroup group)
    {
        GroupId = group.Id;
        GroupName = group.Name;
        GroupDescription = group.Description ?? string.Empty;
        GroupColorHex = group.ColorHex ?? string.Empty;
        IsEditing = true;
        ErrorMessage = string.Empty;

        await LoadMedicationsForEditAsync(group);
    }

    private async Task LoadMedicationsForEditAsync(DoseGroup? group)
    {
        var meds = await _dataStore.GetMedicationsAsync(includeInactive: false);
        MedicationSelection.Clear();

        foreach (var med in meds)
        {
            var existingItem = group?.Items.FirstOrDefault(i => i.MedicationId == med.Id);
            MedicationSelection.Add(new DoseGroupItemEdit(
                med,
                quantity: existingItem?.Quantity ?? 1,
                isIncluded: existingItem != null
            ));
        }
    }

    [RelayCommand]
    public async Task SaveGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(GroupName))
        {
            ErrorMessage = "Please enter a group name.";
            return;
        }

        var included = MedicationSelection.Where(m => m.IsIncluded && m.Quantity > 0).ToList();
        if (included.Count == 0)
        {
            ErrorMessage = "Select at least one medication for this group.";
            return;
        }

        var group = new DoseGroup
        {
            Id = GroupId,
            Name = GroupName.Trim(),
            Description = string.IsNullOrWhiteSpace(GroupDescription) ? null : GroupDescription.Trim(),
            ColorHex = string.IsNullOrWhiteSpace(GroupColorHex) ? null : GroupColorHex.Trim(),
            Items = included.Select(i => new DoseGroupItem
            {
                DoseGroupId = GroupId,
                MedicationId = i.Medication.Id,
                Quantity = i.Quantity
            }).ToList()
        };

        await _dataStore.SaveDoseGroupAsync(group);
        IsEditing = false;
        await LoadGroupsAsync();
        Saved?.Invoke();
    }

    [RelayCommand]
    public async Task DeleteGroupAsync(DoseGroup group)
    {
        await _dataStore.DeleteDoseGroupAsync(group.Id);
        await LoadGroupsAsync();
    }

    [RelayCommand]
    public void CancelEdit()
    {
        if (IsEditing)
        {
            IsEditing = false;
        }
        else
        {
            Cancelled?.Invoke();
        }
    }

    [RelayCommand]
    public void Close()
    {
        Cancelled?.Invoke();
    }
}
