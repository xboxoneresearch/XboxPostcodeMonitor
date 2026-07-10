using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PostCodeSerialMonitor.Models;
using PostCodeSerialMonitor.Services;
using PostCodeSerialMonitor.Utils;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

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
    private bool showTimestamps;

    [ObservableProperty]
    private string codesMetaBaseUrl;

    [ObservableProperty]
    private ObservableCollection<string> languages;

    [ObservableProperty]
    private string selectedLanguage;

    [ObservableProperty]
    private ObservableCollection<string> themes = new(["Dark", "Light", "System"]);

    [ObservableProperty]
    private string selectedTheme;

    public static ObservableCollection<string> GetAvailableLanguages()
    {
        var languages = new ObservableCollection<string>();
        var cultures = LocalizationUtils.GetAvailableCultures();
        foreach (CultureInfo culture in cultures)
            languages.Add(culture.Name);
        return languages;
    }

    public ConfigurationDialogViewModel(ConfigurationService configurationService)
    {
        _configurationService = configurationService;
        _originalConfiguration = configurationService.Config;

        // Initialize properties from current configuration
        CheckForAppUpdates = _originalConfiguration.CheckForAppUpdates;
        CheckForCodeUpdates = _originalConfiguration.CheckForCodeUpdates;
        CheckForFwUpdates = _originalConfiguration.CheckForFwUpdates;
        ShowTimestamps = _originalConfiguration.ShowTimestamps;
        CodesMetaBaseUrl = _originalConfiguration.CodesMetaBaseUrl.ToString();
        SelectedLanguage = _originalConfiguration.Language;
        SelectedTheme = _originalConfiguration.Theme;

        //Add available languages
        Languages = GetAvailableLanguages();
    }

    [RelayCommand]
    private async Task SaveAsync(Window window)
    {
        bool languageChanged = _originalConfiguration.Language != SelectedLanguage;

        await _configurationService.UpdateConfigurationAsync(config =>
        {
            config.CheckForAppUpdates = CheckForAppUpdates;
            config.CheckForCodeUpdates = CheckForCodeUpdates;
            config.CheckForFwUpdates = CheckForFwUpdates;
            config.ShowTimestamps = ShowTimestamps;
            config.CodesMetaBaseUrl = new Uri(CodesMetaBaseUrl);
            config.Language = SelectedLanguage;
            config.Theme = SelectedTheme;
        });

        Application.Current!.RequestedThemeVariant = App.ThemeVariantFromName(SelectedTheme);

        window.Close();

        if (languageChanged) {
            await MessageBoxManager
                .GetMessageBoxStandard(Assets.Resources.RestartRequired, string.Format(Assets.Resources.LanguageChangedPleaseRestart),
                    ButtonEnum.Ok)
                .ShowAsPopupAsync(GetParentWindow());
        }
    }

    [RelayCommand]
    private void Cancel(Window window)
    {
        window.Close();
    }
} 