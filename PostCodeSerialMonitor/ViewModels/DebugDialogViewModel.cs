using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using PostCodeSerialMonitor.Models;
using PostCodeSerialMonitor.Services;

namespace PostCodeSerialMonitor.ViewModels;

public partial class DebugDialogViewModel : ViewModelBase
{
    private readonly ObservableCollection<String> _rawLogEntries;
    private readonly ObservableCollection<LogEntry> _logEntries;
    private readonly SerialLineDecoder _serialLineDecoder;

    public ObservableCollection<ConsoleType> ConsoleTypes { get; }
    public ObservableCollection<CodeFlavor> CodeFlavors { get; } = new(new[]
    {
        CodeFlavor.SMC, CodeFlavor.SP, CodeFlavor.CPU, CodeFlavor.OS
    });

    [ObservableProperty]
    private ConsoleType selectedConsoleType;

    [ObservableProperty]
    private decimal entryCount = 20;

    [ObservableProperty]
    private string codeInput = string.Empty;

    [ObservableProperty]
    private string decodedResultText = string.Empty;

    public DebugDialogViewModel(
        ObservableCollection<String> rawLogEntries,
        ObservableCollection<LogEntry> logEntries,
        SerialLineDecoder serialLineDecoder,
        ObservableCollection<ConsoleType> consoleTypes)
    {
        _rawLogEntries = rawLogEntries;
        _logEntries = logEntries;
        _serialLineDecoder = serialLineDecoder;
        ConsoleTypes = consoleTypes;

        SelectedConsoleType = ConsoleTypes.FirstOrDefault();
    }

    [RelayCommand]
    private void FillDummyData()
    {
        for (int i = 0; i < (int)EntryCount; i++)
        {
            var code = (UInt64)(i * 0x11);
            var segment = i % 4;
            var flavor = CodeFlavors[i % CodeFlavors.Count];

            var rawLine = $"{flavor.ToString().PadRight(4)} ({segment}) 0x{code:X4}\r\n";
            _rawLogEntries.Add(rawLine);
            _logEntries.Add(new LogEntry
            {
                DecodedCode = new DecodedCode
                {
                    Flavor = flavor,
                    Code = code,
                    SeverityLevel = (CodeSeverity)(i % 3),
                    Name = $"DEBUG_CODE_{i}",
                    Description = i % 4 == 0
                        ? $"This is a long debug description for entry {i}, used to verify that the log window wraps text correctly and fills the full width of the resized main window instead of being clipped at a fixed maximum width."
                        : $"Debug entry {i}"
                }
            });
        }
    }

    [RelayCommand]
    private async Task DecodeStandaloneAsync()
    {
        UInt64 code;
        try
        {
            var hex = CodeInput.Trim();
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex[2..];
            code = Convert.ToUInt64(hex, 16);
        }
        catch (Exception)
        {
            await MessageBoxManager
                .GetMessageBoxStandard(Assets.Resources.Error, string.Format(Assets.Resources.InvalidCodeFormatMessageBoxError, CodeInput), ButtonEnum.Ok)
                .ShowAsPopupAsync(GetParentWindow());
            return;
        }

        DecodedResultText = String.Empty;
        foreach (var codeFlavor in CodeFlavors)
        {
            var line = $"{codeFlavor} (0): 0x{code:X4}";
            var decoded = _serialLineDecoder.DecodeLine(line, SelectedConsoleType);
            DecodedResultText += decoded != null
                ? new LogEntry { DecodedCode = decoded }.FormattedText
                : $"Decoding '{line}' failed";
            DecodedResultText += "\n";
        }
    }

    [RelayCommand]
    private void Close(Avalonia.Controls.Window window)
    {
        window.Close();
    }
}
