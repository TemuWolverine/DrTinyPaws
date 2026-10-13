using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using DrTinyPaws.ViewModels;

namespace DrTinyPaws.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _viewModel;

    public MainView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        AttachViewModel();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (DataContext is MainViewModel vm)
        {
            _viewModel = vm;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateBlur(vm.IsPopupOpen);
        }
        else
        {
            _viewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsPopupOpen) && _viewModel != null)
        {
            UpdateBlur(_viewModel.IsPopupOpen);
        }
    }

    private void UpdateBlur(bool isPopupOpen)
    {
        if (MainContentGrid?.Effect is BlurEffect blur)
        {
            blur.Radius = isPopupOpen ? 6 : 0;
        }
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.ClosePopup();
        }
    }
}