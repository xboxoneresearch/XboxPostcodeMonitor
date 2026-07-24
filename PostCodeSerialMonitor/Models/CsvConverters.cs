using System;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Linq;

namespace PostCodeSerialMonitor.Models;
public class HexNumberConverter : DefaultTypeConverter
{
    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        if (text == null || text == "")
            return null;

        return Convert.ToUInt64(text, 16);
    }

    public override string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
    {
        if (value == null)
            return null;

        return $"0x{value:X8}";
    }
}

public class CodeFlavorEnumConverter : DefaultTypeConverter
{
    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        if (text == null)
            return null;

        switch (text)
        {
            case "SMC":
                return CodeFlavor.SMC;
            case "SP":
                return CodeFlavor.SP;
            case "CPU":
                return CodeFlavor.CPU;
            case "OS":
                return CodeFlavor.OS;
            case "UNK":
            default:
                return CodeFlavor.UNKNOWN;
        }
    }

    public override string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
    {
        if (value == null)
            return null;

        switch ((CodeFlavor)value)
        {
            case CodeFlavor.SMC:
                return "SMC";
            case CodeFlavor.SP:
                return "SP";
            case CodeFlavor.CPU:
                return "CPU";
            case CodeFlavor.OS:
                return "OS";
            case CodeFlavor.UNKNOWN:
            default:
                return "UNK";
        }
    }
}

public class ConsoleTypeConverter : DefaultTypeConverter
{
    private ConsoleType ParseConsoleType(string text)
    {
        return text.Trim() switch
        {
            "ALL" => ConsoleType.ALL,
            "XOP" => ConsoleType.XboxOnePhat,
            "XOS" => ConsoleType.XboxOneS,
            "XOX" => ConsoleType.XboxOneX,
            "XSS" => ConsoleType.XboxSeriesS,
            "XSX" => ConsoleType.XboxSeriesX,
            _ => ConsoleType.UNKNOWN
        };
    }

    private string ConsoleTypeToString(ConsoleType type)
    {
        return type switch
        {
            ConsoleType.ALL => "ALL",
            ConsoleType.XboxOnePhat => "XOP",
            ConsoleType.XboxOneS => "XOS",
            ConsoleType.XboxOneX => "XOX",
            ConsoleType.XboxSeriesS => "XSS",
            ConsoleType.XboxSeriesX => "XSX",
            _ => "UNK"
        };
    }

    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        if (text == null)
            return null;

        // Split by comma and trim each value
        var types = text.Split(',')
                        .Select(t => ParseConsoleType(t))
                        .ToArray();

        return types;
    }

    public override string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
    {
        if (value == null)
            return null;

        if (value is ConsoleType[] types)
        {
            return string.Join(",", types.Select(ConsoleTypeToString));
        }

        return ConsoleTypeToString((ConsoleType)value);
    }
}