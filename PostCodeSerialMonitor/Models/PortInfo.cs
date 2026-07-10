namespace PostCodeSerialMonitor.Models;

public record PortInfo(string Name, string? Description)
{
    public override string ToString() => Name;
}
