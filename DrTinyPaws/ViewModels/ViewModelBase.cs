using DrTinyPaws.Services;
using ReactiveUI;
using System.Reactive;

namespace DrTinyPaws.ViewModels;

public abstract class ViewModelBase : ReactiveObject
{
    public ReactiveCommand<Unit, Unit> NavigateBackCommand { get; }
    public INavigationService? NavigationService { get; protected set; }

    protected ViewModelBase(INavigationService? navigationService = null)
    {
        NavigationService = navigationService ?? ServiceLocator.NavigationService;
        NavigateBackCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (NavigationService != null)
            {
                await NavigationService.GoBackAsync();
            }
        });
    }
}