using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using DrTinyPaws.ViewModels;
using DrTinyPaws.Views;

namespace DrTinyPaws.Services;

public class NavigationService : INavigationService
{
    private NavigationPage? _navigationPage;
    public bool CanGoBack => _navigationPage != null && _navigationPage.StackDepth > 1;

    public static Page? CreatePage(ViewModelBase viewModel)
    {
        Page? page = viewModel switch
        {
            DashboardViewModel => new DashboardView(),
            TakeMedicationViewModel => new TakeMedicationView(),
            MedicationManagementViewModel => new MedicationManagementView(),
            MedicationEditViewModel => new MedicationEditView(),
            DoseGroupManagementViewModel => new DoseGroupManagementView(),
            SettingsViewModel => new SettingsView(),
            _ => null
        };

        page?.DataContext = viewModel;

        return page;
    }

    public void Attach(NavigationPage navigationPage)
    {
        _navigationPage = navigationPage;
    }

    public async Task GoBackAsync()
    {
        if (_navigationPage == null) 
            return;

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_navigationPage.StackDepth > 1)
            {
                await _navigationPage.PopAsync();
            }
        });
    }

    public async Task NavigateToAsync(ViewModelBase viewModel)
    {
        if (_navigationPage == null) 
            return;

        var page = CreatePage(viewModel);
        if (page != null)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await _navigationPage.PushAsync(page);
            });
        }
    }

    public async Task NavigateToRootAsync()
    {
        if (_navigationPage == null) 
            return;

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await _navigationPage.PopToRootAsync();
        });
    }
}