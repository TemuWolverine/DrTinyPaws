using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using DrTinyPaws.Models;

namespace DrTinyPaws.ViewModels;

public partial class WeeklyChartViewModel : ViewModelBase
{
    [ObservableProperty]
    private DateTime _weekStartDate;

    [ObservableProperty]
    private string _weekLabel = string.Empty;

    [ObservableProperty]
    private ISeries[] _series = [];

    [ObservableProperty]
    private Axis[] _xAxes = [];

    [ObservableProperty]
    private Axis[] _yAxes = [];

    [ObservableProperty]
    private int _totalWeeklyDoses;

    public event Action? WeekChanged;

    public WeeklyChartViewModel()
    {
        // Start on Monday of current week
        var now = DateTime.Now;
        int diff = (7 + (int)now.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        _weekStartDate = now.Date.AddDays(-diff);
        UpdateWeekLabel();
        SetupAxes();
    }

    private void SetupAxes()
    {
        var dayNames = new string[7];
        for (int i = 0; i < 7; i++)
        {
            var day = WeekStartDate.AddDays(i);
            dayNames[i] = $"{day:ddd}\n{day:d MMM}";
        }

        XAxes =
        [
            new Axis
            {
                Labels = dayNames,
                TextSize = 11
            }
        ];

        YAxes =
        [
            new Axis
            {
                MinStep = 1,
                MinLimit = 0,
                TextSize = 11
            }
        ];
    }

    private void UpdateWeekLabel()
    {
        var weekEnd = WeekStartDate.AddDays(6);
        WeekLabel = $"{WeekStartDate:dd MMM} – {weekEnd:dd MMM yyyy}";
    }

    [RelayCommand]
    public void PreviousWeek()
    {
        WeekStartDate = WeekStartDate.AddDays(-7);
        UpdateWeekLabel();
        SetupAxes();
        WeekChanged?.Invoke();
    }

    [RelayCommand]
    public void NextWeek()
    {
        WeekStartDate = WeekStartDate.AddDays(7);
        UpdateWeekLabel();
        SetupAxes();
        WeekChanged?.Invoke();
    }

    [RelayCommand]
    public void CurrentWeek()
    {
        var now = DateTime.Now;
        int diff = (7 + (int)now.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        WeekStartDate = now.Date.AddDays(-diff);
        UpdateWeekLabel();
        SetupAxes();
        WeekChanged?.Invoke();
    }

    public void UpdateData(List<Medication> medications, List<MedicationIntake> weekIntakes)
    {
        UpdateWeekLabel();
        SetupAxes();

        TotalWeeklyDoses = weekIntakes.Sum(i => i.Quantity);

        // Group intakes by medication
        var intakesByMedId = weekIntakes
            .GroupBy(i => i.MedicationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var seriesList = new List<ISeries>();

        // We want a stacked column series for every medication that has data this week,
        // or all active medications so the legend is consistent.
        // Medications with non-zero intake are prioritized.
        var medsToDisplay = medications
            .Where(m => intakesByMedId.ContainsKey(m.Id) || m.IsActive)
            .OrderByDescending(m => intakesByMedId.ContainsKey(m.Id))
            .ThenBy(m => m.Name)
            .ToList();

        foreach (var med in medsToDisplay)
        {
            var dayCounts = new int[7];
            if (intakesByMedId.TryGetValue(med.Id, out var medIntakes))
            {
                foreach (var intake in medIntakes)
                {
                    int dayIndex = (intake.TakenAt.Date - WeekStartDate.Date).Days;
                    if (dayIndex >= 0 && dayIndex < 7)
                    {
                        dayCounts[dayIndex] += intake.Quantity;
                    }
                }
            }

            // Only add series if it has at least one intake this week,
            // or if no series exist at all show active meds so graph is clear.
            if (dayCounts.Any(c => c > 0) || seriesList.Count < 3)
            {
                SKColor skColor = SKColor.TryParse(med.ColorHex, out var parsed) ? parsed : SKColors.Gray;

                seriesList.Add(new StackedColumnSeries<int>
                {
                    Name = string.IsNullOrEmpty(med.Dosage) ? med.Name : $"{med.Name} ({med.Dosage})",
                    Values = dayCounts,
                    Fill = new SolidColorPaint(skColor),
                    Stroke = null,
                    MaxBarWidth = 40,
                    Rx = 3, // rounded top edges
                    Ry = 3
                });
            }
        }

        Series = seriesList.ToArray();
    }
}