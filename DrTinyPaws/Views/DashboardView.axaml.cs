using Avalonia.ReactiveUI;
using DrTinyPaws.ViewModels;

namespace DrTinyPaws.Views;

public partial class DashboardView : ReactiveUserControl<DashboardViewModel>
{
    public DashboardView()
    {
        InitializeComponent();
    }
}
