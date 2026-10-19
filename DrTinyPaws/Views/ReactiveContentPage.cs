using System;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;

namespace DrTinyPaws.Views;

public class ReactiveContentPage<TViewModel> : ContentPage, IViewFor<TViewModel> where TViewModel : class
{
#pragma warning disable AVP1002
    public static readonly StyledProperty<TViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<ReactiveContentPage<TViewModel>, TViewModel?>(nameof(ViewModel));
#pragma warning restore AVP1002

    public TViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = (TViewModel?)value;
    }

    public ReactiveContentPage()
    {
        this.GetObservable(DataContextProperty).Subscribe(x => ViewModel = x as TViewModel);
        this.GetObservable(ViewModelProperty).Subscribe(x => DataContext = x);
    }
}
