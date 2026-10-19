using DrTinyPaws.ViewModels;

namespace DrTinyPaws.Views;

public partial class DashboardView : ReactiveContentPage<DashboardViewModel>
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void OnQuickAddMenuItemClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            QuickAddFabButton.Flyout?.Hide();
        });
    }
}
