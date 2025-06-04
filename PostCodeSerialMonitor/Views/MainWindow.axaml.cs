using Avalonia.Controls;
using Avalonia.Interactivity;
using PostCodeSerialMonitor.ViewModels;
using System.Diagnostics;
using System;

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

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scrollViewer == null) return;

        // If user scrolls up, disable autoscroll and show the button
        if (e.OffsetDelta.Y < 0)
        {
            _autoScroll = false;
            if (AutoScrollButton != null)
            {
                AutoScrollButton.IsVisible = true;
            }
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
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Log the error or show a message to the user
                Debug.WriteLine(string.Format(Assets.Resources.FailedOpenUrl, ex.Message));
            }
        }
    }
}