using DrTinyPaws.Services;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace DrTinyPaws.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IDataStore _dataStore;
    public event Action? Cancelled;
    public event Action? Saved;
    [Reactive] public partial bool ClearExistingDataOnImport { get; set; } = true;
    [Reactive] public partial string CurrentDatabasePath { get; set; } = string.Empty;
    [Reactive] public partial string CustomPathInput { get; set; } = string.Empty;
    [Reactive] public partial string ImportStatusMessage { get; set; } = string.Empty;

    [Reactive] public partial bool ImportSuccess { get; set; }
    [Reactive] public partial bool IsImporting { get; set; }
    [Reactive] public partial bool IsSuccess { get; set; }
    public Interaction<Unit, string?> OpenDatabaseFileDialog { get; } = new();
    public Interaction<Unit, string?> OpenPillLoggerFileDialog { get; } = new();

    [Reactive]
    public partial string StatusMessage { get; set; } = string.Empty;

    public SettingsViewModel(IDataStore dataStore, DrTinyPaws.Services.INavigationService? navigationService = null) : base(navigationService)
    {
        _dataStore = dataStore;
        CurrentDatabasePath = dataStore.DatabasePath;
        CustomPathInput = dataStore.DatabasePath;
    }

    [ReactiveCommand]
    public async Task BrowseDatabasePathAsync()
    {
        try
        {
            var selectedPath = await OpenDatabaseFileDialog.Handle(Unit.Default);
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                CustomPathInput = selectedPath;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to browse path: {ex.Message}";
            IsSuccess = false;
        }
    }

    [ReactiveCommand]
    public void Cancel()
    {
        Cancelled?.Invoke();
        _ = NavigationService?.GoBackAsync();
    }

    [ReactiveCommand]
    public async Task ImportPillLoggerAsync()
    {
        if (IsImporting) return;

        try
        {
            var selectedPath = await OpenPillLoggerFileDialog.Handle(Unit.Default);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            IsImporting = true;
            ImportStatusMessage = "Importing data from Pill Logger...";
            ImportSuccess = false;

            var result = await _dataStore.ImportPillLoggerDatabaseAsync(selectedPath, ClearExistingDataOnImport);
            if (result.Success)
            {
                ImportSuccess = true;
                ImportStatusMessage = $"Successfully imported {result.MedicationsImported} medications, {result.IntakesImported:N0} logs, and {result.DoseGroupsImported} routine presets!";
            }
            else
            {
                ImportSuccess = false;
                ImportStatusMessage = $"Import failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            ImportSuccess = false;
            ImportStatusMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }

    [ReactiveCommand]
    public async Task ResetToDefaultPathAsync()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var defaultPath = Path.Combine(appData, "DrTinyPaws", "drtinypaws.db");
        CustomPathInput = defaultPath;
        await SavePathAsync();
    }

    [ReactiveCommand]
    public async Task SavePathAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomPathInput))
        {
            StatusMessage = "Path cannot be empty.";
            IsSuccess = false;
            return;
        }

        try
        {
            var trimmed = CustomPathInput.Trim();
            await _dataStore.SetDatabasePathAsync(trimmed);
            CurrentDatabasePath = _dataStore.DatabasePath;
            StatusMessage = "Database location updated successfully! External sync enabled.";
            IsSuccess = true;
            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to set path: {ex.Message}";
            IsSuccess = false;
        }
    }
}