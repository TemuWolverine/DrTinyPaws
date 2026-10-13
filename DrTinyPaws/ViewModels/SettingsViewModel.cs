using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrTinyPaws.Services;

namespace DrTinyPaws.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IDataStore _dataStore;

    [ObservableProperty]
    private string _currentDatabasePath = string.Empty;

    [ObservableProperty]
    private string _customPathInput = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    public event Action? Saved;
    public event Action? Cancelled;

    public SettingsViewModel(IDataStore dataStore)
    {
        _dataStore = dataStore;
        _currentDatabasePath = dataStore.DatabasePath;
        _customPathInput = dataStore.DatabasePath;
    }

    [RelayCommand]
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

    [RelayCommand]
    public async Task ResetToDefaultPathAsync()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var defaultPath = Path.Combine(appData, "DrTinyPaws", "drtinypaws.db");
        CustomPathInput = defaultPath;
        await SavePathAsync();
    }

    [RelayCommand]
    public void Cancel()
    {
        Cancelled?.Invoke();
    }
}
