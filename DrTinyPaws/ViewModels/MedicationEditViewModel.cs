using DrTinyPaws.Models;
using DrTinyPaws.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DrTinyPaws.ViewModels;

public partial class MedicationEditViewModel : ViewModelBase
{
    private readonly IDataStore _dataStore;
    private readonly INotificationService _notificationService;
    public event Action? Cancelled;
    public event Action? Saved;
    public List<string> AvailableColors { get; } =
    [
        "#2563EB", // Blue
        "#0284C7", // Sky Blue
        "#0D9488", // Teal
        "#16A34A", // Green
        "#84CC16", // Lime
        "#EAB308", // Yellow
        "#F97316", // Orange
        "#DC2626", // Red
        "#E11D48", // Rose
        "#9333EA", // Purple
        "#6366F1", // Indigo
        "#64748B"  // Slate
    ];

    [Reactive] public partial string ColorHex { get; set; } = string.Empty;

    public List<string> CommonSchedules { get; } =
    [
        "Once daily (Morning)",
        "Once daily (Night)",
        "Twice daily (Morning & Night)",
        "Three times daily",
        "Every 4 to 6 hours",
        "Weekly",
        "As needed"
    ];

    [Reactive] public partial string Dosage { get; set; } = string.Empty; 
    [Reactive] public partial string ErrorMessage { get; set; } = string.Empty;
    [Reactive] public partial string Id { get; set; } = string.Empty;
    [Reactive] public partial bool IsEditing { get; set; }
    [Reactive] public partial string Name { get; set; } = string.Empty;
    [Reactive] public partial string Notes { get; set; } = string.Empty;
    [Reactive] public partial string ReminderTime { get; set; } = string.Empty;
    [Reactive] public partial string Schedule { get; set; } = string.Empty;
    public string Title => IsEditing ? "Edit Medication" : "Add Medication";

    public MedicationEditViewModel(
        IDataStore dataStore,
        INotificationService notificationService,
        Medication? existing = null,
        DrTinyPaws.Services.INavigationService? navigationService = null) : base(navigationService)
    {
        _dataStore = dataStore;
        _notificationService = notificationService;

        if (existing != null)
        {
            IsEditing = true;
            Id = existing.Id;
            Name = existing.Name;
            Dosage = existing.Dosage ?? string.Empty;
            ColorHex = existing.ColorHex;
            Schedule = existing.Schedule ?? string.Empty;
            ReminderTime = existing.ReminderTime ?? string.Empty;
            Notes = existing.Notes ?? string.Empty;
        }
    }

    [ReactiveCommand]
    public void Cancel()
    {
        Cancelled?.Invoke();
        _ = NavigationService?.GoBackAsync();
    }

    [ReactiveCommand]
    public async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Please enter a medication name.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ColorHex))
        {
            ErrorMessage = "Please select or enter a color.";
            return;
        }

        ErrorMessage = string.Empty;

        var med = new Medication
        {
            Id = string.IsNullOrEmpty(Id) ? Guid.NewGuid().ToString() : Id,
            Name = Name.Trim(),
            Dosage = string.IsNullOrWhiteSpace(Dosage) ? null : Dosage.Trim(),
            ColorHex = ColorHex.Trim(),
            Schedule = string.IsNullOrWhiteSpace(Schedule) ? null : Schedule.Trim(),
            ReminderTime = string.IsNullOrWhiteSpace(ReminderTime) ? null : ReminderTime.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            IsActive = true
        };

        await _dataStore.SaveMedicationAsync(med);

        if (!string.IsNullOrWhiteSpace(med.ReminderTime))
        {
            await _notificationService.ScheduleReminderAsync(med);
        }
        else
        {
            await _notificationService.CancelReminderAsync(med.Id);
        }

        Saved?.Invoke();
        if (NavigationService != null)
        {
            await NavigationService.GoBackAsync();
        }
    }

    [ReactiveCommand]
    public void SelectColor(string color)
    {
        ColorHex = color;
    }
}