using System;
using ReactiveUI;
using DrTinyPaws.Services;

namespace DrTinyPaws.ViewModels;

public class MainViewModel : ReactiveObject, IScreen
{
    private readonly IDataStore _dataStore;
    private readonly INotificationService _notificationService;

    public DashboardViewModel Dashboard { get; }
    public RoutingState Router { get; } = new RoutingState();

    public MainViewModel() : this(ServiceLocator.DataStore, ServiceLocator.NotificationService)
    {
    }

    public MainViewModel(IDataStore dataStore, INotificationService notificationService)
    {
        _dataStore = dataStore;
        _notificationService = notificationService;

        Dashboard = new DashboardViewModel(this, _dataStore, _notificationService);
        Router.Navigate.Execute(Dashboard).Subscribe();
    }
}