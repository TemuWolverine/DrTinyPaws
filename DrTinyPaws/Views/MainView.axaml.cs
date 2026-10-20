using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using DrTinyPaws.Services;

namespace DrTinyPaws.Views;

public partial class MainView : DrawerPage
{
    private IInsetsManager? _insetsManager;
    private TopLevel? _topLevel;

    public MainView()
    {
        InitializeComponent();
        if (ServiceLocator.NavigationService is NavigationService navService)
        {
            navService.Attach(NavPage);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel != null)
        {
            _topLevel.ScalingChanged += OnScalingChanged;
            if (_topLevel.InsetsManager is { } insets)
            {
                _insetsManager = insets;
                insets.SafeAreaChanged += OnSafeAreaChanged;
                UpdateSafeArea(insets.SafeAreaPadding);
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _topLevel?.ScalingChanged -= OnScalingChanged;
        _topLevel = null;
        _insetsManager?.SafeAreaChanged -= OnSafeAreaChanged;
        _insetsManager = null;
    }

    private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e)
    {
        UpdateSafeArea(e.SafeAreaPadding);
    }

    private void OnScalingChanged(object? sender, System.EventArgs e)
    {
        if (_insetsManager != null)
        {
            UpdateSafeArea(_insetsManager.SafeAreaPadding);
        }
    }

    private void UpdateSafeArea(Thickness rawPadding)
    {
        var scaling = _topLevel?.RenderScaling ?? 1.0;

        // On Android, InsetsManager often returns raw physical pixels (e.g. 118px top, 63px bottom)
        // rather than device-independent pixels (DIPs). A standard Android status bar is ~24-48 DIPs.
        // If the top inset is greater than 60, it indicates physical pixels that need to be scaled down.
        if (rawPadding.Top > 60)
        {
            var scale = scaling > 1.0 ? scaling : (rawPadding.Top / 45.0);
            SafeAreaPadding = new Thickness(
                rawPadding.Left / scale,
                rawPadding.Top / scale,
                rawPadding.Right / scale,
                rawPadding.Bottom / scale);
        }
        else
        {
            SafeAreaPadding = rawPadding;
        }
    }
}