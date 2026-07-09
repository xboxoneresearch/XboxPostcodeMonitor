using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PostCodeSerialMonitor.ViewModels;

public class ViewModelBase : ObservableObject
{
    internal Window GetParentWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop?.MainWindow ?? throw new Exception(Assets.Resources.FailedGetMainWindow);
        else
            throw new Exception(Assets.Resources.FailedGetApplicationLifetime);
    }
}
