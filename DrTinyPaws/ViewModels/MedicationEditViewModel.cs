using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrTinyPaws.Models;
using DrTinyPaws.Services;

namespace DrTinyPaws.ViewModels;

public partial class MedicationEditViewModel : ViewModelBase
{
    private readonly IDataStore _dataStore;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _dosage = string.Empty;

    [ObservableProperty]
    private string _colorHex = string.Empty;

    [ObservableProperty]
    private string _schedule = string.Empty;

    [ObservableProperty]
    private string _reminderTime = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

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

    public event Action? Saved;
    public event Action? Cancelled;

    public MedicationEditViewModel(IDataStore dataStore, INotificationService notificationService, Medication? existing = null)
    {
        _dataStore = dataStore;
        _notificationService = notificationService;

        if (existing != null)
        {
            _isEditing = true;
            _id = existing.Id;
            _name = existing.Name;
            _dosage = existing.Dosage ?? string.Empty;
            _colorHex = existing.ColorHex;
            _schedule = existing.Schedule ?? string.Empty;
            _reminderTime = existing.ReminderTime ?? string.Empty;
            _notes = existing.Notes ?? string.Empty;
        }
    }

    [RelayCommand]
    public void SelectColor(string color)
    {
        ColorHex = color;
    }

    [RelayCommand]
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
    }

    [RelayCommand]
    public void Cancel()
    {
        Cancelled?.Invoke();
    }
}
