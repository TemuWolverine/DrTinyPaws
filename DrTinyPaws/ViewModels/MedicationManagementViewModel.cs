using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public MedicationManagementViewModel(IDataStore dataStore)
    {
        _dataStore = dataStore;
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

    [RelayCommand]
    public void RequestAddMedication()
    {
        AddMedicationRequested?.Invoke();
    }

    [RelayCommand]
    public void RequestEditMedication(Medication medication)
    {
        EditMedicationRequested?.Invoke(medication);
    }

    [RelayCommand]
    public async Task DeleteMedicationAsync(Medication medication)
    {
        await _dataStore.DeleteMedicationAsync(medication.Id);
        await LoadMedicationsAsync();
    }

    [RelayCommand]
    public void Close()
    {
        Closed?.Invoke();
    }
}
