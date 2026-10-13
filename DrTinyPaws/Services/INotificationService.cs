using System.Threading.Tasks;
using DrTinyPaws.Models;

namespace DrTinyPaws.Services;

public interface INotificationService
{
    Task InitializeAsync();
    Task ScheduleReminderAsync(Medication medication);
    Task CancelReminderAsync(string medicationId);
    Task SendNotificationAsync(string title, string message);
}
