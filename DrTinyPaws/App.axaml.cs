using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using Splat;
using DrTinyPaws.ViewModels;
using DrTinyPaws.Views;

namespace DrTinyPaws;

public partial class App : Application
{
    public override void Initialize()
    {
        RegisterViews();
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    private static void RegisterViews()
    {
        var locator = new RxViewLocator();
        Locator.CurrentMutable.RegisterConstant<ReactiveUI.IViewLocator>(locator);
        Locator.CurrentMutable.Register(() => new DashboardView(), typeof(IViewFor<DashboardViewModel>));
        Locator.CurrentMutable.Register(() => new TakeMedicationView(), typeof(IViewFor<TakeMedicationViewModel>));
        Locator.CurrentMutable.Register(() => new MedicationManagementView(), typeof(IViewFor<MedicationManagementViewModel>));
        Locator.CurrentMutable.Register(() => new MedicationEditView(), typeof(IViewFor<MedicationEditViewModel>));
        Locator.CurrentMutable.Register(() => new DoseGroupManagementView(), typeof(IViewFor<DoseGroupManagementViewModel>));
        Locator.CurrentMutable.Register(() => new SettingsView(), typeof(IViewFor<SettingsViewModel>));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = new MainViewModel() };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}