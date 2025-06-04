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

using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace PostCodeSerialMonitor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SerialService _serialService;
    private readonly ConfigurationService _configurationService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private SerialLineDecoder _serialLineDecoder;
    private MetaUpdateService _metaUpdateService;
    private MetaDefinitionService _metaDefinitionService;
    private IStorageProvider? _storageProvider;

    public ObservableCollection<string> SerialPorts { get; } = new();

    public ObservableCollection<ConsoleType> ConsoleModels { get; } = new();

    public ObservableCollection<LogEntry> LogEntries { get; } = new();
    public ObservableCollection<string> RawLogEntries { get; } = new();

    private string lastConnectedPicoFwVersion = Assets.Resources.Unavailable;

    [ObservableProperty]
    private ConsoleType selectedConsoleModel;

    [ObservableProperty]
    private string? selectedPort;

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private int selectedTabIndex;

    [ObservableProperty]
    private bool mirrorDisplay;

    [ObservableProperty]
    private bool portraitMode;

    [ObservableProperty]
    private bool printTimestamps;

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
        ILogger<MainWindowViewModel> logger)
    {
        _serialService = serialService ?? throw new ArgumentNullException(nameof(serialService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _metaUpdateService = metaUpdateService ?? throw new ArgumentNullException(nameof(metaUpdateService));
        _metaDefinitionService = metaDefinitionService ?? throw new ArgumentNullException(nameof(metaDefinitionService));
        _serialLineDecoder = serialLineDecoder ?? throw new ArgumentNullException(nameof(serialLineDecoder));
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

        RefreshPorts();
        _serialService.DataReceived += OnDataReceived;
        _serialService.Disconnected += OnDisconnected;
        _serialService.DeviceStateChanged += OnDeviceStateChanged;
        _serialService.DeviceConfigChanged += OnDeviceConfigChanged;
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

            var result = await box.ShowAsync();

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
                        .ShowAsync();
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

            await box.ShowAsync();
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
                .ShowAsync();
        }
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
            sb.AppendLine(entry.FormattedText);
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
                .ShowAsync();
        }
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        SerialPorts.Clear();
        foreach (var port in _serialService.GetPortNames())
            SerialPorts.Add(port);
        if (SerialPorts.Count > 0 && SelectedPort == null)
            SelectedPort = SerialPorts.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (SelectedPort != null)
        {
            try
            {
                await _serialService.ConnectAsync(SelectedPort);
                RawLogEntries?.Clear();
                LogEntries?.Clear();
                IsConnected = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, Assets.Resources.ErrorConection);
                await MessageBoxManager
                    .GetMessageBoxStandard(Assets.Resources.Error, string.Format(Assets.Resources.ErrorConectionMessageBoxError,ex.Message),
                        ButtonEnum.Ok)
                    .ShowAsync();
            }
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        _serialService.Disconnect();
        IsConnected = false;
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
    }

    private Window GetParentWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop?.MainWindow ?? throw new Exception(Assets.Resources.FailedGetMainWindow);
        else
            throw new Exception(Assets.Resources.FailedGetApplicationLifetime);
    }
} 