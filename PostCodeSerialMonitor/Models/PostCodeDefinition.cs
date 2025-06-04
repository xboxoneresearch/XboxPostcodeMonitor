using CsvHelper.Configuration.Attributes;

namespace PostCodeSerialMonitor.Models;
public class PostCodeDefinition
{
    [Name("Console")]
    [TypeConverter(typeof(ConsoleTypeConverter))]
    public ConsoleType[] ConsoleType { get; set; } = [];

    [Name("Type")]
    [TypeConverter(typeof(CodeFlavorEnumConverter))]
    public CodeFlavor CodeFlavor { get; set; } = CodeFlavor.UNKNOWN;

    [TypeConverter(typeof(HexNumberConverter))]
    public uint Code { get; set; }
    [TypeConverter(typeof(HexNumberConverter))]
    public uint? Bitmask { get; set; }
    public bool IsError { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
