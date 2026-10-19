using System.Threading.Tasks;
using DrTinyPaws.ViewModels;

namespace DrTinyPaws.Services;

public interface INavigationService
{
    Task NavigateToAsync(ViewModelBase viewModel);
    Task NavigateToRootAsync();
    Task GoBackAsync();
    bool CanGoBack { get; }
}
