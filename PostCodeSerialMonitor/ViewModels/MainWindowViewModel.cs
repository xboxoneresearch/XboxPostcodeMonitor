using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PostCodeSerialMonitor.Views;
using PostCodeSerialMonitor.Services;
using PostCodeSerialMonitor.Models;
using PostCodeSerialMonitor.Utils;

using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Dto;
using Avalonia.Media;

namespace PostCodeSerialMonitor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SerialService _serialService;
    private readonly ConfigurationService _configurationService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private SerialLineDecoder _serialLineDecoder;
    private MetaUpdateService _metaUpdateService;
    private MetaDefinitionService _metaDefinitionService;
    private GithubUpdateService _githubUpdateService;
    private IStorageProvider? _storageProvider;

    public ObservableCollection<PortInfo> SerialPorts { get; } = new();

    public ObservableCollection<ConsoleType> ConsoleModels { get; } = new();

    public ObservableCollection<LogEntry> LogEntries { get; } = new();
    public ObservableCollection<string> RawLogEntries { get; } = new();

    private string lastConnectedPicoFwVersion = Assets.Resources.Unavailable;

    [ObservableProperty]
    private ConsoleType selectedConsoleModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleConnection))]
    private PortInfo? selectedPort;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleConnection))]
    [NotifyPropertyChangedFor(nameof(ConnectionButtonText))]
    [NotifyPropertyChangedFor(nameof(ConnectionButtonIcon))]
    private bool isConnected;

    public bool CanToggleConnection => IsConnected || SelectedPort != null;

    public string ConnectionButtonText => IsConnected ? Assets.Resources.Disconnect : Assets.Resources.Connect;
    public StreamGeometry? ConnectionButtonIcon =>
        Avalonia.Application.Current?.Resources.TryGetResource(
            IsConnected ? "plug_disconnected_regular" : "play_regular", null, out var resource) == true
            ? resource as StreamGeometry
            : null;

    [ObservableProperty]
    private int selectedTabIndex;

    [ObservableProperty]
    private bool mirrorDisplay;

    [ObservableProperty]
    private bool portraitMode;

    [ObservableProperty]
    private bool printTimestamps;

    [ObservableProperty]
    private bool showTimestamps;

    [ObservableProperty]
    private string i2cScanOutput = Assets.Resources.ScanButtonText;

    [ObservableProperty]
    private string firmwareVersion = Assets.Resources.NotConnected;

    [ObservableProperty]
    private string buildDate = string.Empty;

    [ObservableProperty]
    private string metadataLastUpdate = Assets.Resources.Never;

    [ObservableProperty]
    private string appVersion;

    [ObservableProperty]
    private bool debugModeUnlocked;

    private int _appVersionClickCount;

    public IStorageProvider? StorageProvider
    {
        get => _storageProvider;
        set => SetProperty(ref _storageProvider, value);
    }

    public MainWindowViewModel(
        SerialService serialService,
        ConfigurationService configurationService,
        MetaUpdateService metaUpdateService,
        MetaDefinitionService metaDefinitionService,
        SerialLineDecoder serialLineDecoder,
        GithubUpdateService githubUpdateService,
        ILogger<MainWindowViewModel> logger)
    {
        _serialService = serialService ?? throw new ArgumentNullException(nameof(serialService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _metaUpdateService = metaUpdateService ?? throw new ArgumentNullException(nameof(metaUpdateService));
        _metaDefinitionService = metaDefinitionService ?? throw new ArgumentNullException(nameof(metaDefinitionService));
        _serialLineDecoder = serialLineDecoder ?? throw new ArgumentNullException(nameof(serialLineDecoder));
        _githubUpdateService = githubUpdateService ?? throw new ArgumentNullException(nameof(githubUpdateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Get version from assembly
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AppVersion = version?.ToString() ?? "Unversioned";

        // Initialize console models with only Xbox consoles
        foreach (ConsoleType type in Enum.GetValues(typeof(ConsoleType)))
        {
            if (type.ToString().StartsWith("Xbox"))
            {
                ConsoleModels.Add(type);
            }
        }
        SelectedConsoleModel = ConsoleModels.FirstOrDefault();
        ShowTimestamps = _configurationService.Config.ShowTimestamps;

        RefreshPorts();
        _serialService.DataReceived += OnDataReceived;
        _serialService.Disconnected += OnDisconnected;
        _serialService.DeviceStateChanged += OnDeviceStateChanged;
        _serialService.DeviceConfigChanged += OnDeviceConfigChanged;
    }

    private MessageBoxStandardParams MsgBoxHyperlink(string title, string text, string link)
    {
        return new MessageBoxStandardParams
        {
            ContentTitle = title,
            ContentMessage = text,
            ButtonDefinitions = ButtonEnum.Ok,
            Icon = Icon.None,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            SizeToContent = SizeToContent.WidthAndHeight,
            
            HyperLinkParams = new HyperLinkParams
            {
                Text = link,
                Action = new Action(() => GlobalActions.OpenHyperlinkAction(link)),
            }
        };
    }

    // Executed by code behind view
    public async void OnLoaded()
    {
        var updateAvailable = await _metaUpdateService.CheckForMetaDefinitionUpdatesAsync();
        if (updateAvailable)
        {
            var box = MessageBoxManager
                .GetMessageBoxStandard(
                    Assets.Resources.NewMetadataAvailable,
                    Assets.Resources.NewMetadataAvailableInformation,
                    ButtonEnum.YesNo
            );

            var result = await box.ShowAsPopupAsync(GetParentWindow());

            if (result.HasFlag(ButtonResult.Yes))
            {
                try
                {
                    await _metaUpdateService.UpdateMetaDefinitionAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, Assets.Resources.FailedUpdateMetadata);
                    await MessageBoxManager
                        .GetMessageBoxStandard(Assets.Resources.Error, string.Format(Assets.Resources.FailedUpdateMetadataMessageBoxError, ex.Message), ButtonEnum.Ok)
                        .ShowAsPopupAsync(GetParentWindow());
                }
            }
        }

        // Update the metadata last update timestamp
        MetadataLastUpdate = _metaUpdateService.LastUpdateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? Assets.Resources.Never;

        var success = await _metaUpdateService.TryLoadLocalDefinition();
        if (!success)
        {
            _logger.LogWarning(Assets.Resources.FailedLoadLocalMetadata);
            var box = MessageBoxManager
                .GetMessageBoxStandard(Assets.Resources.Warning, Assets.Resources.FailedLoadLocalMetadataMessageBoxWarning,
                    ButtonEnum.Ok);

            await box.ShowAsPopupAsync(GetParentWindow());
        }

        try
        {
            await _metaDefinitionService.RefreshMetaDefinitionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, Assets.Resources.FailedLoadLocalMetadata);
            await MessageBoxManager
                .GetMessageBoxStandard(Assets.Resources.Error, string.Format(Assets.Resources.FailedLoadLocalMetadataMessageBoxError, ex.Message),
                    ButtonEnum.Ok)
                .ShowAsPopupAsync(GetParentWindow());
        }

        if (_configurationService.Config.CheckForAppUpdates)
        {
            updateAvailable = await _githubUpdateService.CheckForAppUpdatesAsync(AppVersion);
            if (updateAvailable)
            {
                var box = MessageBoxManager
                    .GetMessageBoxStandard(MsgBoxHyperlink(
                        Assets.Resources.Warning,
                        Assets.Resources.NewAppReleaseAvailable,
                        "https://github.com/xboxoneresearch/XboxPostcodeMonitor/releases"
                    ));
                await box.ShowAsPopupAsync(GetParentWindow());
            }
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
        RawLogEntries.Clear();
    }

    [RelayCommand]
    private async Task SaveLogAsync()
    {
        if (_storageProvider == null)
            return;

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var defaultName = $"POST_{SelectedConsoleModel}_{timestamp}_{AppVersion}.log";

        var file = await _storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Assets.Resources.SaveLogFiles,
            DefaultExtension = "log",
            SuggestedFileName = defaultName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(Assets.Resources.LogFiles)
                {
                    Patterns = new[] { "*.log" }
                }
            }
        });

        if (file == null)
            return;

        var sb = new StringBuilder();

        // Add metadata
        sb.AppendLine("=== Metadata ===");
        sb.AppendLine($"Console Type: {SelectedConsoleModel}");
        sb.AppendLine($"Pico Firmware: {lastConnectedPicoFwVersion}");
        sb.AppendLine($"Metadata Update: {MetadataLastUpdate}");
        sb.AppendLine($"App Version: {AppVersion}");
        sb.AppendLine();

        // Add raw log
        sb.AppendLine("=== Raw Log ===");
        foreach (var entry in RawLogEntries)
        {
            sb.AppendLine(entry);
        }
        sb.AppendLine();

        // Add decoded log
        sb.AppendLine("=== Decoded Log ===");
        foreach (var entry in LogEntries.Where(e => e.DecodedCode != null))
        {
            sb.AppendLine(entry.FormattedWithTs);
        }

        try
        {
            await File.WriteAllTextAsync(file.Path.LocalPath, sb.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, Assets.Resources.ErrorSavingLogFile);
            await MessageBoxManager
                .GetMessageBoxStandard(Assets.Resources.Error, string.Format(Assets.Resources.ErrorSavingLogFileMessageBoxError, ex.Message),
                    ButtonEnum.Ok)
                .ShowAsPopupAsync(GetParentWindow());
        }
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        SerialPorts.Clear();
        foreach (var port in _serialService.GetPortInfos())
            SerialPorts.Add(port);
        if (SerialPorts.Count > 0 && SelectedPort == null)
            SelectedPort = SerialPorts.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (SelectedPort == null)
        {
            return;
        }

        try
        {
            if (IsConnected)
            {
                _serialService.Disconnect();
                IsConnected = false;
            }
            else
            {
                await _serialService.ConnectAsync(SelectedPort.Name);
                ClearLog();
                IsConnected = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, Assets.Resources.ErrorConection);
            await MessageBoxManager
                .GetMessageBoxStandard(Assets.Resources.Error, string.Format(Assets.Resources.ErrorConectionMessageBoxError, ex.Message),
                    ButtonEnum.Ok)
                .ShowAsPopupAsync(GetParentWindow());
        }

        if (IsConnected && _configurationService.Config.CheckForFwUpdates)
        {
            var updateAvailable = await _githubUpdateService.CheckForFirmwareUpdatesAsync(_serialService.FirmwareVersion);
            if (updateAvailable)
            {
                var box = MessageBoxManager
                    .GetMessageBoxStandard(MsgBoxHyperlink(
                        Assets.Resources.Warning,
                        Assets.Resources.NewFirmwareReleaseAvailable,
                        "https://github.com/xboxoneresearch/PicoDurangoPOST/releases"
                    ));
                await box.ShowAsPopupAsync(GetParentWindow());
            }
        }
    }

    private void OnDataReceived(string line)
    {
        RawLogEntries.Add(line);

        var decoded = _serialLineDecoder.DecodeLine(line, SelectedConsoleModel);
        if (decoded != null)
        {
            LogEntries.Add(new LogEntry { DecodedCode = decoded });
        }
    }

    private void OnDisconnected()
    {
        IsConnected = false;
        FirmwareVersion = Assets.Resources.NotConnected;
        BuildDate = string.Empty;
        MirrorDisplay = false;
        PortraitMode = false;
        PrintTimestamps = false;
        I2cScanOutput = Assets.Resources.ScanButtonText;
        var prevSelectedPort = SelectedPort;
        RefreshPorts();
        if (prevSelectedPort != null && SerialPorts.Contains(prevSelectedPort)) {
            SelectedPort = prevSelectedPort;
        }
    }

    private void OnDeviceStateChanged()
    {
        FirmwareVersion = _serialService.FirmwareVersion;
        BuildDate = _serialService.BuildDate;
        // Retain this info even after disconnected, for saving the Log
        lastConnectedPicoFwVersion = $"{FirmwareVersion} ({BuildDate})";
    }

    private void OnDeviceConfigChanged()
    {
        MirrorDisplay = _serialService.MirrorDisplay;
        PortraitMode = _serialService.PortraitMode;
        PrintTimestamps = _serialService.PrintTimestamps;
    }

    [RelayCommand]
    private async Task ShowConfigurationAsync()
    {
        var dialog = new ConfigurationDialog
        {
            DataContext = new ConfigurationDialogViewModel(_configurationService)
        };

        await dialog.ShowDialog(GetParentWindow());

        ShowTimestamps = _configurationService.Config.ShowTimestamps;
    }

    [RelayCommand]
    private void AppVersionClicked()
    {
        if (DebugModeUnlocked)
            return;

        _appVersionClickCount++;
        if (_appVersionClickCount >= 5)
            DebugModeUnlocked = true;
    }

    [RelayCommand]
    private async Task ShowDebugMenuAsync()
    {
        var dialog = new DebugDialog
        {
            DataContext = new DebugDialogViewModel(LogEntries, _serialLineDecoder, ConsoleModels)
        };

        await dialog.ShowDialog(GetParentWindow());
    }
} 