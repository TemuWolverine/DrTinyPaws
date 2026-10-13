using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using DrTinyPaws.Models;
using DrTinyPaws.Services;

namespace DrTinyPaws.ViewModels;

public partial class MedicationManagementViewModel : ViewModelBase
{
    private readonly IDataStore _dataStore;

    public ObservableCollection<Medication> Medications { get; } = [];

    public event Action? AddMedicationRequested;
    public event Action<Medication>? EditMedicationRequested;
    public event Action? Closed;

    public MedicationManagementViewModel(IDataStore dataStore, IScreen? hostScreen = null) : base(hostScreen)
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
    public void RequestAddMedication()
    {
        AddMedicationRequested?.Invoke();
        if (HostScreen != null)
        {
            HostScreen.Router.Navigate.Execute(new MedicationEditViewModel(_dataStore, ServiceLocator.NotificationService, null, HostScreen)).Subscribe();
        }
    }

    [ReactiveCommand]
    public void RequestEditMedication(Medication medication)
    {
        EditMedicationRequested?.Invoke(medication);
        if (HostScreen != null)
        {
            HostScreen.Router.Navigate.Execute(new MedicationEditViewModel(_dataStore, ServiceLocator.NotificationService, medication, HostScreen)).Subscribe();
        }
    }

    [ReactiveCommand]
    public async Task DeleteMedicationAsync(Medication medication)
    {
        await _dataStore.DeleteMedicationAsync(medication.Id);
        await LoadMedicationsAsync();
    }

    [ReactiveCommand]
    public void Close()
    {
        Closed?.Invoke();
        HostScreen?.Router.NavigateBack.Execute().Subscribe();
    }
}
