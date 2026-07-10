using System;
using System.ComponentModel;

namespace PostCodeSerialMonitor.Models;
// Simple model to hold log entry data
public class LogEntry : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool isSelected;
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value) return;
            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public string RawText { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string TimestampText => Timestamp.ToString("HH:mm:ss.fff");
    public required DecodedCode DecodedCode { get; set; }

    public string FormattedWithTs => $"{TimestampText} {FormattedText}";
    // CodeText + Description
    public string FormattedText => FormatText();
    // Flavor, index and code (hex)
    public string CodeText => FormatCodeText();
    // Individual fields, for column-aligned display
    public string FlavorText => DecodedCode.Flavor.ToString();
    public string IndexText => $"({DecodedCode.Index}):";
    public string CodeHexText => $"{DecodedCode.Code:X4}";
    public string NameText => string.IsNullOrEmpty(DecodedCode?.Name) ? string.Empty : $"[{DecodedCode.Name}]";
    // Name + description on one line, for the truncated inline preview
    public string InlinePreviewText => string.IsNullOrEmpty(Description)
        ? NameText
        : string.IsNullOrEmpty(NameText) ? Description : $"{NameText} {Description}";
    // Description or null
    public string? Description => string.IsNullOrEmpty(DecodedCode.Description) ? null : DecodedCode?.Description;
    public bool HasDescription => !string.IsNullOrEmpty(Description);
    public bool IsWarning => SeverityLevel == CodeSeverity.Warning;
    public bool IsError => SeverityLevel == CodeSeverity.Error;
    public CodeSeverity SeverityLevel => DecodedCode.SeverityLevel;

    private string FormatCodeText()
    {
        // Format flavor, index, and code with fixed spacing
        var formatted = $"{DecodedCode?.Flavor,-4} ({DecodedCode?.Index}): {DecodedCode?.Code,4:X4}";
        if (!string.IsNullOrEmpty(DecodedCode?.Name))
            formatted += $" [{DecodedCode?.Name}]";
        return formatted;
    }

    private string FormatText()
    {
        var formatted = $"{CodeText}";
        if (!string.IsNullOrEmpty(Description))
            formatted += $"\n- {Description}";
        return formatted;
    }
}