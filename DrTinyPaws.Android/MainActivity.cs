using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace DrTinyPaws.Android;

[Activity(
    Label = "DrTinyPaws.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        DrTinyPaws.Services.ServiceLocator.NotificationService = new AndroidNotificationService(this);
        base.OnCreate(savedInstanceState);
    }
}
