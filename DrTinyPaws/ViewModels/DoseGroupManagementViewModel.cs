using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using DrTinyPaws.Models;
using DrTinyPaws.Services;

namespace DrTinyPaws.ViewModels;

public partial class DoseGroupManagementViewModel : ViewModelBase
{
    private readonly IDataStore _dataStore;

    [Reactive]
    public partial string GroupId { get; set; } = string.Empty;

    [Reactive]
    public partial string GroupName { get; set; } = string.Empty;

    [Reactive]
    public partial string GroupDescription { get; set; } = string.Empty;

    [Reactive]
    public partial string GroupColorHex { get; set; } = string.Empty;

    [Reactive]
    public partial bool IsEditing { get; set; }

    [Reactive]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public ObservableCollection<DoseGroup> Groups { get; } = [];
    public ObservableCollection<DoseGroupItemEdit> MedicationSelection { get; } = [];

    public event Action? Saved;
    public event Action? Cancelled;

    public DoseGroupManagementViewModel(IDataStore dataStore, IScreen? hostScreen = null) : base(hostScreen)
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

    [ReactiveCommand]
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

    [ReactiveCommand]
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

    [ReactiveCommand]
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

    [ReactiveCommand]
    public async Task DeleteGroupAsync(DoseGroup group)
    {
        await _dataStore.DeleteDoseGroupAsync(group.Id);
        await LoadGroupsAsync();
    }

    [ReactiveCommand]
    public void CancelEdit()
    {
        if (IsEditing)
        {
            IsEditing = false;
        }
        else
        {
            Cancelled?.Invoke();
            HostScreen?.Router.NavigateBack.Execute().Subscribe();
        }
    }

    [ReactiveCommand]
    public void Close()
    {
        Cancelled?.Invoke();
        HostScreen?.Router.NavigateBack.Execute().Subscribe();
    }
}
