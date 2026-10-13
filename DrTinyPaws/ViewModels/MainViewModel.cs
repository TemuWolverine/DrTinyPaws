using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrTinyPaws.Models;
using DrTinyPaws.Services;

namespace DrTinyPaws.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IDataStore _dataStore;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private WeeklyChartViewModel _chartViewModel = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveDialog))]
    private object? _popupContent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveDialog))]
    private bool _isPopupOpen;

    public bool HasActiveDialog => IsPopupOpen;

    partial void OnPopupContentChanged(object? value)
    {
        IsPopupOpen = value != null;
    }

    partial void OnIsPopupOpenChanged(bool value)
    {
        if (!value && PopupContent != null)
        {
            PopupContent = null;
        }
    }

    [ObservableProperty]
    private int _totalDosesToday;

    [ObservableProperty]
    private int _totalDosesThisWeek;

    [ObservableProperty]
    private string _notificationBanner = string.Empty;

    [ObservableProperty]
    private bool _showNotificationBanner;

    public ObservableCollection<MedicationIntake> RecentIntakes { get; } = [];
    public ObservableCollection<Medication> Medications { get; } = [];
    public ObservableCollection<DoseGroup> DoseGroups { get; } = [];

    public MainViewModel() : this(ServiceLocator.DataStore, ServiceLocator.NotificationService)
    {
    }

    public MainViewModel(IDataStore dataStore, INotificationService notificationService)
    {
        _dataStore = dataStore;
        _notificationService = notificationService;

        _chartViewModel.WeekChanged += () =>
        {
            _ = LoadChartAndIntakesAsync();
        };

        _dataStore.DataChanged += (s, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _ = RefreshDataAsync();
            });
        };

        if (_notificationService is DefaultNotificationService defaultNotif)
        {
            defaultNotif.NotificationReceived += (title, message) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    NotificationBanner = $"{title}: {message}";
                    ShowNotificationBanner = true;
                });
            };
        }

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        await _dataStore.InitializeAsync();
        await _notificationService.InitializeAsync();
        await RefreshDataAsync();
    }

    [RelayCommand]
    public void DismissNotificationBanner()
    {
        ShowNotificationBanner = false;
        NotificationBanner = string.Empty;
    }

    public async Task RefreshDataAsync()
    {
        var meds = await _dataStore.GetMedicationsAsync(includeInactive: false);
        Medications.Clear();
        foreach (var m in meds)
        {
            Medications.Add(m);
            // Schedule reminders
            if (!string.IsNullOrWhiteSpace(m.ReminderTime))
            {
                await _notificationService.ScheduleReminderAsync(m);
            }
        }

        var groups = await _dataStore.GetDoseGroupsAsync();
        DoseGroups.Clear();
        foreach (var g in groups)
        {
            DoseGroups.Add(g);
        }

        await LoadChartAndIntakesAsync();
    }

    private async Task LoadChartAndIntakesAsync()
    {
        var start = ChartViewModel.WeekStartDate.Date;
        var end = start.AddDays(7).AddTicks(-1);

        var weekIntakes = await _dataStore.GetIntakesForDateRangeAsync(start, end);
        var recent = await _dataStore.GetRecentIntakesAsync(limit: 100);

        RecentIntakes.Clear();
        foreach (var r in recent)
        {
            RecentIntakes.Add(r);
        }

        ChartViewModel.UpdateData(Medications.ToList(), weekIntakes);

        var today = DateTime.Today;
        TotalDosesToday = recent.Where(i => i.TakenAt.Date == today).Sum(i => i.Quantity);
        TotalDosesThisWeek = weekIntakes.Sum(i => i.Quantity);
    }

    [RelayCommand]
    public async Task OpenTakeMedicationAsync()
    {
        var vm = new TakeMedicationViewModel(_dataStore);
        await vm.InitializeAsync();

        vm.Saved += () =>
        {
            ClosePopup();
            _ = RefreshDataAsync();
        };

        vm.Cancelled += () =>
        {
            ClosePopup();
        };

        PopupContent = vm;
    }

    [RelayCommand]
    public async Task QuickTakeGroupAsync(DoseGroup group)
    {
        if (group.Items.Count == 0) return;

        var now = DateTime.Now;
        var intakes = group.Items.Select(item => new MedicationIntake
        {
            MedicationId = item.MedicationId,
            Quantity = item.Quantity,
            TakenAt = now,
            Notes = $"From group: {group.Name}"
        }).ToList();

        await _dataStore.LogIntakesAsync(intakes);
        await RefreshDataAsync();
    }

    [RelayCommand]
    public void OpenAddMedication()
    {
        var vm = new MedicationEditViewModel(_dataStore, _notificationService);
        vm.Saved += () =>
        {
            ClosePopup();
            _ = RefreshDataAsync();
        };
        vm.Cancelled += () =>
        {
            ClosePopup();
        };

        PopupContent = vm;
    }

    [RelayCommand]
    public void EditMedication(Medication medication)
    {
        var vm = new MedicationEditViewModel(_dataStore, _notificationService, medication);
        vm.Saved += () =>
        {
            ClosePopup();
            _ = RefreshDataAsync();
        };
        vm.Cancelled += () =>
        {
            ClosePopup();
        };

        PopupContent = vm;
    }

    [RelayCommand]
    public async Task OpenManageMedicationsAsync()
    {
        var vm = new MedicationManagementViewModel(_dataStore);
        await vm.InitializeAsync();
        vm.AddMedicationRequested += () =>
        {
            var addVm = new MedicationEditViewModel(_dataStore, _notificationService);
            addVm.Saved += () =>
            {
                _ = RefreshDataAsync();
                _ = OpenManageMedicationsAsync();
            };
            addVm.Cancelled += () =>
            {
                _ = OpenManageMedicationsAsync();
            };
            PopupContent = addVm;
        };
        vm.EditMedicationRequested += med =>
        {
            var editVm = new MedicationEditViewModel(_dataStore, _notificationService, med);
            editVm.Saved += () =>
            {
                _ = RefreshDataAsync();
                _ = OpenManageMedicationsAsync();
            };
            editVm.Cancelled += () =>
            {
                _ = OpenManageMedicationsAsync();
            };
            PopupContent = editVm;
        };
        vm.Closed += () => ClosePopup();
        PopupContent = vm;
    }

    [RelayCommand]
    public async Task OpenManageGroupsAsync()
    {
        var vm = new DoseGroupManagementViewModel(_dataStore);
        await vm.InitializeAsync();
        vm.Saved += () =>
        {
            _ = RefreshDataAsync();
        };
        vm.Cancelled += () =>
        {
            ClosePopup();
        };
        PopupContent = vm;
    }

    [RelayCommand]
    public void OpenSettings()
    {
        var vm = new SettingsViewModel(_dataStore);
        vm.Saved += () =>
        {
            _ = RefreshDataAsync();
        };
        vm.Cancelled += () =>
        {
            ClosePopup();
        };
        PopupContent = vm;
    }

    [RelayCommand]
    public void ClosePopup()
    {
        PopupContent = null;
        IsPopupOpen = false;
    }

    [RelayCommand]
    public void CloseDialog() => ClosePopup();

    [RelayCommand]
    public async Task DeleteIntakeAsync(MedicationIntake intake)
    {
        await _dataStore.DeleteIntakeAsync(intake.Id);
        await RefreshDataAsync();
    }

    [RelayCommand]
    public async Task DeleteMedicationAsync(Medication medication)
    {
        await _dataStore.DeleteMedicationAsync(medication.Id);
        await RefreshDataAsync();
    }
}
