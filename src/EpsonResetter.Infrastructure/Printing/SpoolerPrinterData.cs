namespace EpsonResetter.Infrastructure.Printing;

/// <summary>
/// Raw values read from the Windows print subsystem. Kept separate from
/// <see cref="EpsonResetter.Core.Models.PrinterDevice"/> so the mapping can be
/// tested without WMI or a spooler.
/// </summary>
public sealed record SpoolerPrinterData
{
    public required string Name { get; init; }

    public string? DriverName { get; init; }

    public string? PortName { get; init; }

    public bool WorkOffline { get; init; }

    public int PrinterStatus { get; init; }

    public bool IsDefault { get; init; }

    public string? Location { get; init; }

    public string? Comment { get; init; }

    public string? IpAddress { get; init; }

    public int? PortNumber { get; init; }

    public string? DriverManufacturer { get; init; }

    public string? DriverVersion { get; init; }

    public string? DriverInfName { get; init; }
}