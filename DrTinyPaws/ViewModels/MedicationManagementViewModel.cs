using DrTinyPaws.Models;
using DrTinyPaws.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace DrTinyPaws.ViewModels;

public partial class MedicationManagementViewModel : ViewModelBase
{
    private readonly IDataStore _dataStore;
    public event Action? AddMedicationRequested;
    public event Action? Closed;
    public event Action<Medication>? EditMedicationRequested;
    public ObservableCollection<Medication> Medications { get; } = [];

    public MedicationManagementViewModel(IDataStore dataStore, DrTinyPaws.Services.INavigationService? navigationService = null) : base(navigationService)
    {
        _dataStore = dataStore;

        _dataStore.DataChanged += (s, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _ = LoadMedicationsAsync();
            });
        };
    }

    [ReactiveCommand]
    public void Close()
    {
        Closed?.Invoke();
        _ = NavigationService?.GoBackAsync();
    }

    [ReactiveCommand]
    public async Task DeleteMedicationAsync(Medication medication)
    {
        await _dataStore.DeleteMedicationAsync(medication.Id);
        await LoadMedicationsAsync();
    }

    public async Task InitializeAsync()
    {
        await LoadMedicationsAsync();
    }

    public async Task LoadMedicationsAsync()
    {
        var meds = await _dataStore.GetMedicationsAsync(includeInactive: false);
        Medications.Clear();
        foreach (var m in meds)
        {
            Medications.Add(m);
        }
    }

    [ReactiveCommand]
    public async Task RequestAddMedicationAsync()
    {
        AddMedicationRequested?.Invoke();
        if (NavigationService != null)
        {
            await NavigationService.NavigateToAsync(new MedicationEditViewModel(_dataStore, ServiceLocator.NotificationService, null, NavigationService));
        }
    }

    [ReactiveCommand]
    public async Task RequestEditMedicationAsync(Medication medication)
    {
        EditMedicationRequested?.Invoke(medication);
        if (NavigationService != null)
        {
            await NavigationService.NavigateToAsync(new MedicationEditViewModel(_dataStore, ServiceLocator.NotificationService, medication, NavigationService));
        }
    }
}