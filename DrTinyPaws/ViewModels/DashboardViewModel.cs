using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using DrTinyPaws.Models;
using DrTinyPaws.Services;

namespace DrTinyPaws.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IDataStore _dataStore;
    private readonly INotificationService _notificationService;

    [Reactive]
    public partial WeeklyChartViewModel ChartViewModel { get; set; }

    public ObservableCollection<DoseGroup> DoseGroups { get; } = [];

    public ObservableCollection<Medication> Medications { get; } = [];

    [Reactive]
    public partial string NotificationBanner { get; set; } = string.Empty;

    public ObservableCollection<MedicationIntake> RecentIntakes { get; } = [];

    [Reactive]
    public partial bool ShowNotificationBanner { get; set; }

    [Reactive]
    public partial int TotalDosesThisWeek { get; set; }

    [Reactive]
    public partial int TotalDosesToday { get; set; }

    public DashboardViewModel(IScreen hostScreen, IDataStore dataStore, INotificationService notificationService)
        : base(hostScreen, "Dashboard")
    {
        _dataStore = dataStore;
        _notificationService = notificationService;
        ChartViewModel = new WeeklyChartViewModel(hostScreen);

        ChartViewModel.WeekChanged += () =>
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

    [ReactiveCommand]
    public async Task DeleteIntakeAsync(MedicationIntake intake)
    {
        await _dataStore.DeleteIntakeAsync(intake.Id);
        await RefreshDataAsync();
    }

    [ReactiveCommand]
    public void DismissNotificationBanner()
    {
        ShowNotificationBanner = false;
        NotificationBanner = string.Empty;
    }

    public async Task InitializeAsync()
    {
        await _dataStore.InitializeAsync();
        await _notificationService.InitializeAsync();
        await RefreshDataAsync();
    }

    [ReactiveCommand]
    public async Task OpenManageGroupsAsync()
    {
        var vm = new DoseGroupManagementViewModel(_dataStore, HostScreen);
        await vm.InitializeAsync();
        HostScreen.Router.Navigate.Execute(vm).Subscribe();
    }

    [ReactiveCommand]
    public async Task OpenManageMedicationsAsync()
    {
        var vm = new MedicationManagementViewModel(_dataStore, HostScreen);
        await vm.InitializeAsync();
        HostScreen.Router.Navigate.Execute(vm).Subscribe();
    }

    [ReactiveCommand]
    public void OpenSettings()
    {
        var vm = new SettingsViewModel(_dataStore, HostScreen);
        HostScreen.Router.Navigate.Execute(vm).Subscribe();
    }

    [ReactiveCommand]
    public async Task OpenTakeMedicationAsync()
    {
        var vm = new TakeMedicationViewModel(_dataStore, HostScreen);
        await vm.InitializeAsync();
        HostScreen.Router.Navigate.Execute(vm).Subscribe();
    }

    [ReactiveCommand]
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

    public async Task RefreshDataAsync()
    {
        var meds = await _dataStore.GetMedicationsAsync(includeInactive: false);
        Medications.Clear();
        foreach (var m in meds)
        {
            Medications.Add(m);
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
}