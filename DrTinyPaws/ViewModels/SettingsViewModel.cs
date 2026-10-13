using System;
using System.IO;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using DrTinyPaws.Services;

namespace DrTinyPaws.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IDataStore _dataStore;

    [Reactive]
    public partial string CurrentDatabasePath { get; set; } = string.Empty;

    [Reactive]
    public partial string CustomPathInput { get; set; } = string.Empty;

    [Reactive]
    public partial string StatusMessage { get; set; } = string.Empty;

    [Reactive]
    public partial bool IsSuccess { get; set; }

    public event Action? Saved;
    public event Action? Cancelled;

    public SettingsViewModel(IDataStore dataStore, IScreen? hostScreen = null) : base(hostScreen)
    {
        _dataStore = dataStore;
        CurrentDatabasePath = dataStore.DatabasePath;
        CustomPathInput = dataStore.DatabasePath;
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

    [ReactiveCommand]
    public async Task ResetToDefaultPathAsync()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var defaultPath = Path.Combine(appData, "DrTinyPaws", "drtinypaws.db");
        CustomPathInput = defaultPath;
        await SavePathAsync();
    }

    [ReactiveCommand]
    public void Cancel()
    {
        Cancelled?.Invoke();
        HostScreen?.Router.NavigateBack.Execute().Subscribe();
    }
}
