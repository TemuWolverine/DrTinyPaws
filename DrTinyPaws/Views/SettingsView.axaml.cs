using System;
using System.IO;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using ReactiveUI;
using DrTinyPaws.ViewModels;

namespace DrTinyPaws.Views;

public partial class SettingsView : ReactiveUserControl<SettingsViewModel>
{
    public SettingsView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            if (ViewModel == null) return;

            ViewModel.OpenPillLoggerFileDialog.RegisterHandler(async interaction =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null)
                {
                    interaction.SetOutput(null);
                    return;
                }

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Pill Logger Database File",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("SQLite Database (*.db;*.sqlite)")
                        {
                            Patterns = ["*.db", "*.sqlite", "*.sqlite3"]
                        },
                        new FilePickerFileType("All Files (*.*)")
                        {
                            Patterns = ["*.*"]
                        }
                    ]
                });

                string? path = null;
                if (files.Count > 0)
                {
                    var file = files[0];
                    path = file.TryGetLocalPath();
                    if (string.IsNullOrEmpty(path) && file.Path.IsAbsoluteUri && file.Path.IsFile && File.Exists(file.Path.LocalPath))
                    {
                        path = file.Path.LocalPath;
                    }

                    if (string.IsNullOrEmpty(path))
                    {
                        var tempFile = Path.Combine(Path.GetTempPath(), $"pilllogger_{Guid.NewGuid():N}.db");
                        await using var srcStream = await file.OpenReadAsync();
                        await using var dstStream = File.Create(tempFile);
                        await srcStream.CopyToAsync(dstStream);
                        path = tempFile;
                    }
                }

                interaction.SetOutput(path);
            }).DisposeWith(disposables);

            ViewModel.OpenDatabaseFileDialog.RegisterHandler(async interaction =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null)
                {
                    interaction.SetOutput(null);
                    return;
                }

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select NurseJoy Database File",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("SQLite Database (*.db;*.sqlite)")
                        {
                            Patterns = ["*.db", "*.sqlite", "*.sqlite3"]
                        },
                        new FilePickerFileType("All Files (*.*)")
                        {
                            Patterns = ["*.*"]
                        }
                    ]
                });

                string? path = null;
                if (files.Count > 0)
                {
                    var file = files[0];
                    path = file.TryGetLocalPath();
                    if (string.IsNullOrEmpty(path) && file.Path.IsAbsoluteUri && file.Path.IsFile && File.Exists(file.Path.LocalPath))
                    {
                        path = file.Path.LocalPath;
                    }
                }

                interaction.SetOutput(path);
            }).DisposeWith(disposables);
        });
    }
}
