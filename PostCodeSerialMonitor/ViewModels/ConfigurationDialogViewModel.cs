using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PostCodeSerialMonitor.Models;
using PostCodeSerialMonitor.Services;
using System.Threading.Tasks;
using Avalonia.Controls;
using System;

namespace PostCodeSerialMonitor.ViewModels;

public partial class ConfigurationDialogViewModel : ViewModelBase
{
    private readonly ConfigurationService _configurationService;
    private readonly AppConfiguration _originalConfiguration;

    [ObservableProperty]
    private bool checkForAppUpdates;

    [ObservableProperty]
    private bool checkForCodeUpdates;

    [ObservableProperty]
    private bool checkForFwUpdates;

    [ObservableProperty]
    private string appUpdateUrl;

    [ObservableProperty]
    private string codesMetaBaseUrl;

    [ObservableProperty]
    private string fwUpdateUrl;

    public ObservableCollection<string> Languages { get; } = new();

    [ObservableProperty]
    private string selectedLanguage;

    public ConfigurationDialogViewModel(ConfigurationService configurationService)
    {
        _configurationService = configurationService;
        _originalConfiguration = configurationService.Config;

        // Initialize properties from current configuration
        CheckForAppUpdates = _originalConfiguration.CheckForAppUpdates;
        CheckForCodeUpdates = _originalConfiguration.CheckForCodeUpdates;
        CheckForFwUpdates = _originalConfiguration.CheckForFwUpdates;
        AppUpdateUrl = _originalConfiguration.AppUpdateUrl.ToString();
        CodesMetaBaseUrl = _originalConfiguration.CodesMetaBaseUrl.ToString();
        FwUpdateUrl = _originalConfiguration.FwUpdateUrl.ToString();
        SelectedLanguage = _originalConfiguration.Language;

        //Add available languages
        Languages.Add("en-US");
        Languages.Add("pt-BR");
    }

    [RelayCommand]
    private async Task SaveAsync(Window window)
    {
        await _configurationService.UpdateConfigurationAsync(config =>
        {
            config.CheckForAppUpdates = CheckForAppUpdates;
            config.CheckForCodeUpdates = CheckForCodeUpdates;
            config.CheckForFwUpdates = CheckForFwUpdates;
            config.AppUpdateUrl = new Uri(AppUpdateUrl);
            config.CodesMetaBaseUrl = new Uri(CodesMetaBaseUrl);
            config.FwUpdateUrl = new Uri(FwUpdateUrl);
            config.Language = SelectedLanguage;
        });

        window.Close();
    }

    [RelayCommand]
    private void Cancel(Window window)
    {
        window.Close();
    }
} 