using Avalonia.ReactiveUI;
using DrTinyPaws.ViewModels;

namespace DrTinyPaws.Views;

public partial class SettingsView : ReactiveUserControl<SettingsViewModel>
{
    public SettingsView()
    {
        InitializeComponent();
    }
}
