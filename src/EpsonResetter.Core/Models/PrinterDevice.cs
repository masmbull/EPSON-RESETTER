namespace EpsonResetter.Core.Models;

/// <summary>
/// A printer reported by the Windows print subsystem. Every field is derived from
/// the spooler or the printer port configuration; nothing is inferred or invented.
/// </summary>
public sealed class PrinterDevice
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string DriverName { get; init; } = string.Empty;

    public string? DriverManufacturer { get; init; }

    public string? DriverVersion { get; init; }

    public string? DriverInfName { get; init; }

    public string PortName { get; init; } = string.Empty;

    public PrinterConnectionType ConnectionType { get; init; } = PrinterConnectionType.Unknown;

    public string? IpAddress { get; init; }

    public int? PortNumber { get; init; }

    public PrinterCondition Condition { get; init; } = PrinterCondition.Unknown;

    public bool WorkOffline { get; init; }

    public bool IsDefault { get; init; }

    public string? Location { get; init; }

    public string? Comment { get; init; }

    public string Manufacturer { get; init; } = "Unknown";

    public string ModelName { get; init; } = string.Empty;

    public bool IsEpson => string.Equals(Manufacturer, "Epson", StringComparison.OrdinalIgnoreCase);

    public bool IsNetwork => ConnectionType == PrinterConnectionType.Network;

    public DateTime DetectedAtUtc { get; init; }

    public string ConnectionDisplay => ConnectionType switch
    {
        PrinterConnectionType.Usb => "USB",
        PrinterConnectionType.Network => IpAddress is null ? "Network" : $"Network ({IpAddress})",
        PrinterConnectionType.Local => PortName is { Length: > 0 } ? $"Local port ({PortName})" : "Local port",
        _ => PortName is { Length: > 0 } ? PortName : "Unknown"
    };
}