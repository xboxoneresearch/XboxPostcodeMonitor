using System;
using CsvHelper.Configuration.Attributes;

namespace PostCodeSerialMonitor.Models;

public class OSErrorDefinition
{
    [Name("Console")]
    [TypeConverter(typeof(ConsoleTypeConverter))]
    public ConsoleType[] ConsoleType { get; set; } = [];

    [Name("Type")]
    [TypeConverter(typeof(CodeFlavorEnumConverter))]
    public CodeFlavor CodeFlavor { get; set; } = CodeFlavor.UNKNOWN;

    [TypeConverter(typeof(HexNumberConverter))]
    public UInt64 Code { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}