using Avalonia.Controls;
using Avalonia.Interactivity;
using PostCodeSerialMonitor.ViewModels;
using System.Diagnostics;
using System;
using PostCodeSerialMonitor.Utils;

namespace PostCodeSerialMonitor.Views;

public partial class MainWindow : Window
{
    private bool _autoScroll = true;
    private ScrollViewer? _scrollViewer;
    private ItemsRepeater? _itemsRepeater;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.StorageProvider = StorageProvider;
            viewModel.OnLoaded();
        }

        // Find and initialize the ScrollViewer and ItemsRepeater
        _scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
        _itemsRepeater = this.FindControl<ItemsRepeater>("LogItemsRepeater");

        if (_scrollViewer != null && _itemsRepeater != null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
            _itemsRepeater.LayoutUpdated += OnItemsRepeaterLayoutUpdated;
        }
    }

    // Distance from the bottom (in pixels) still considered "at the bottom", to absorb layout jitter
    // from item virtualization/resizing so autoscroll doesn't flicker on/off during normal updates.
    private const double BottomThreshold = 4;

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scrollViewer == null) return;

        var atBottom = _scrollViewer.Offset.Y >= _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height - BottomThreshold;
        _autoScroll = atBottom;
        if (AutoScrollButton != null)
        {
            AutoScrollButton.IsVisible = !atBottom;
        }
    }

    private void OnItemsRepeaterLayoutUpdated(object? sender, EventArgs e)
    {
        if (_autoScroll && _scrollViewer != null)
        {
            _scrollViewer.ScrollToEnd();
        }
    }

    private void OnAutoScrollButtonClick(object? sender, RoutedEventArgs e)
    {
        _autoScroll = true;
        if (AutoScrollButton != null)
        {
            AutoScrollButton.IsVisible = false;
        }
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollToEnd();
        }
    }

    private void OnHyperlinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock textBlock && textBlock.Tag is string url)
        {
            try
            {
                GlobalActions.OpenHyperlinkAction(url);
            }
            catch (Exception ex)
            {
                // Log the error or show a message to the user
                Debug.WriteLine(string.Format(Assets.Resources.FailedOpenUrl, ex.Message));
            }
        }
    }
}