using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DrTinyPaws.Models;

namespace DrTinyPaws.Services;

public class DefaultNotificationService : INotificationService, IDisposable
{
    private readonly ConcurrentDictionary<string, string> _scheduledReminders = new();
    private System.Threading.Timer? _checkTimer;

    public event Action<string, string>? NotificationReceived;

    public Task InitializeAsync()
    {
        // Check every minute for medication reminders
        _checkTimer = new System.Threading.Timer(CheckRemindersCallback, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        return Task.CompletedTask;
    }

    private void CheckRemindersCallback(object? state)
    {
        var nowStr = DateTime.Now.ToString("HH:mm");
        foreach (var (medName, reminderTime) in _scheduledReminders)
        {
            if (reminderTime == nowStr)
            {
                SendNotificationAsync("Medication Reminder", $"Time to take your {medName}!");
            }
        }
    }

    public Task ScheduleReminderAsync(Medication medication)
    {
        if (!string.IsNullOrWhiteSpace(medication.ReminderTime))
        {
            _scheduledReminders[medication.Name] = medication.ReminderTime;
        }
        else
        {
            _scheduledReminders.TryRemove(medication.Name, out _);
        }
        return Task.CompletedTask;
    }

    public Task CancelReminderAsync(string medicationId)
    {
        _scheduledReminders.TryRemove(medicationId, out _);
        return Task.CompletedTask;
    }

    public virtual Task SendNotificationAsync(string title, string message)
    {
        Debug.WriteLine($"[NOTIFICATION] {title}: {message}");
        NotificationReceived?.Invoke(title, message);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _checkTimer?.Dispose();
    }
}
