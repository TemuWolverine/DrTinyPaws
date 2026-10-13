using System;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;

namespace DrTinyPaws.ViewModels;

public abstract class ViewModelBase : ReactiveObject, IRoutableViewModel
{
    public virtual string? UrlPathSegment { get; protected set; }
    public IScreen HostScreen { get; protected set; }

    public ReactiveCommand<Unit, IRoutableViewModel?> NavigateBackCommand { get; }

    protected ViewModelBase(IScreen? hostScreen = null, string? urlPathSegment = null)
    {
        HostScreen = hostScreen!;
        UrlPathSegment = urlPathSegment ?? GetType().Name;
        NavigateBackCommand = ReactiveCommand.CreateFromObservable(() =>
            HostScreen != null ? HostScreen.Router.NavigateBack.Execute() : Observable.Return<IRoutableViewModel?>(null));
    }
}
