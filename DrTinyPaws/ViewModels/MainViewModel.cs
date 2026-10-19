using DrTinyPaws.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Threading.Tasks;

namespace DrTinyPaws.ViewModels;

public partial class MainViewModel : ReactiveObject
{
    private readonly IDataStore _dataStore;
    private readonly INavigationService _navigationService;
    private readonly INotificationService _notificationService;
    public DashboardViewModel Dashboard { get; }
    [Reactive] public partial bool IsDrawerOpen { get; set; }

    public MainViewModel() :
        this(ServiceLocator.DataStore, ServiceLocator.NotificationService, ServiceLocator.NavigationService)
    {
    }

    public MainViewModel(IDataStore dataStore, INotificationService notificationService, INavigationService? navigationService = null)
    {
        _dataStore = dataStore;
        _notificationService = notificationService;
        _navigationService = navigationService ?? ServiceLocator.NavigationService;

        Dashboard = new DashboardViewModel(_navigationService, _dataStore, _notificationService);
    }

    [ReactiveCommand]
    public void CloseDrawer()
    {
        IsDrawerOpen = false;
    }

    [ReactiveCommand]
    public Task NavigateToDashboardAsync() => NavigateToPageAsync();

    [ReactiveCommand]
    public Task NavigateToGroupPresetsAsync()
    {
        var vm = new DoseGroupManagementViewModel(_dataStore, _navigationService);
        return NavigateToPageAsync(vm, vm.InitializeAsync);
    }

    [ReactiveCommand]
    public Task NavigateToMedicationsAsync()
    {
        var vm = new MedicationManagementViewModel(_dataStore, _navigationService);
        return NavigateToPageAsync(vm, vm.InitializeAsync);
    }

    [ReactiveCommand]
    public Task NavigateToSettingsAsync() => NavigateToPageAsync(new SettingsViewModel(_dataStore, _navigationService));

    [ReactiveCommand]
    public Task NavigateToTakeMedicationAsync()
    {
        var vm = new TakeMedicationViewModel(_dataStore, _navigationService);
        return NavigateToPageAsync(vm, () => vm.InitializeAsync());
    }

    [ReactiveCommand]
    public void ToggleDrawer()
    {
        IsDrawerOpen = !IsDrawerOpen;
    }

    private async Task NavigateToPageAsync(ViewModelBase? vm = null, Func<Task>? initialize = null)
    {
        IsDrawerOpen = false;
        if (initialize != null)
        {
            await initialize();
        }

        await _navigationService.NavigateToRootAsync();

        if (vm != null)
        {
            await _navigationService.NavigateToAsync(vm);
        }
    }
}