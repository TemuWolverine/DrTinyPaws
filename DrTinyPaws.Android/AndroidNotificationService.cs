using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using DrTinyPaws.Models;
using DrTinyPaws.Services;

namespace DrTinyPaws.Android;

public class AndroidNotificationService : DefaultNotificationService
{
    private const string ChannelId = "drtinypaws_medication_channel";
    private readonly Context _context;

    public AndroidNotificationService(Context context)
    {
        _context = context;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            CreateNotificationChannelOreo();
        }
    }

    [SupportedOSPlatform("android26.0")]
    private void CreateNotificationChannelOreo()
    {
        var channel = new NotificationChannel(ChannelId, "Medication Reminders", NotificationImportance.High)
        {
            Description = "DrTinyPaws medication intake reminders"
        };
        if (_context.GetSystemService(Context.NotificationService) is NotificationManager notificationManager)
        {
            notificationManager.CreateNotificationChannel(channel);
        }
    }

    public override Task SendNotificationAsync(string title, string message)
    {
        base.SendNotificationAsync(title, message);

        try
        {
            var builder = new NotificationCompat.Builder(_context, ChannelId);
            builder.SetContentTitle(title);
            builder.SetContentText(message);
            builder.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo);
            builder.SetPriority(NotificationCompat.PriorityHigh);
            builder.SetAutoCancel(true);

            var notificationManager = NotificationManagerCompat.From(_context);
            int notificationId = (int)(DateTime.UtcNow.Ticks % int.MaxValue);
            using var notification = builder.Build();
            if (notification != null && notificationManager != null)
            {
                notificationManager.Notify(notificationId, notification);
            }
        }
        catch (Exception ex)
        {
            global::System.Diagnostics.Debug.WriteLine($"Android notification error: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}
