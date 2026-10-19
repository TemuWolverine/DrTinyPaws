namespace DrTinyPaws.Services;

public static class ServiceLocator
{
    private static IDataStore? _dataStore;
    private static INavigationService? _navigationService;
    private static INotificationService? _notificationService;

    public static IDataStore DataStore
    {
        get => _dataStore ??= new SqliteDataStore();
        set => _dataStore = value;
    }

    public static INavigationService NavigationService
    {
        get => _navigationService ??= new NavigationService();
        set => _navigationService = value;
    }

    public static INotificationService NotificationService
    {
        get => _notificationService ??= new DefaultNotificationService();
        set => _notificationService = value;
    }
}