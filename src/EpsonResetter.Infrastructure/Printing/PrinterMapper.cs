using EpsonResetter.Core.Identification;
using EpsonResetter.Core.Models;

namespace EpsonResetter.Infrastructure.Printing;

/// <summary>
/// Classifies a Windows printer port. Only well known port naming schemes are
/// used; anything unrecognised stays <see cref="PrinterConnectionType.Unknown"/>.
/// </summary>
public static class PortClassifier
{
    public static PrinterConnectionType Classify(string? portName, string? ipAddress)
    {
        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            return PrinterConnectionType.Network;
        }

        if (string.IsNullOrWhiteSpace(portName))
        {
            return PrinterConnectionType.Unknown;
        }

        var port = portName.Trim();

        if (PrinterModelInfo.Normalize(port).StartsWith("USB", StringComparison.Ordinal) ||
            PrinterModelInfo.Normalize(port).StartsWith("DOT4", StringComparison.Ordinal) ||
            PrinterModelInfo.Normalize(port).StartsWith("WSD", StringComparison.Ordinal))
        {
            return port.StartsWith("WSD", StringComparison.OrdinalIgnoreCase)
                ? PrinterConnectionType.Network
                : PrinterConnectionType.Usb;
        }

        if (port.StartsWith("IP", StringComparison.OrdinalIgnoreCase) && port.Contains('.'))
        {
            return PrinterConnectionType.Network;
        }

        if (port.Contains('.') && port.All(character => char.IsDigit(character) || character == '.'))
        {
            return PrinterConnectionType.Network;
        }

        if (port.StartsWith("Standard TCP/IP", StringComparison.OrdinalIgnoreCase))
        {
            return PrinterConnectionType.Network;
        }

        if (port.Equals("PORTPROMPT:", StringComparison.OrdinalIgnoreCase) ||
            port.Equals("nul:", StringComparison.OrdinalIgnoreCase) ||
            port.Equals("FILE:", StringComparison.OrdinalIgnoreCase) ||
            port.Equals("LPT1", StringComparison.OrdinalIgnoreCase) ||
            port.Equals("LPT2", StringComparison.OrdinalIgnoreCase) ||
            port.Equals("COM1", StringComparison.OrdinalIgnoreCase) ||
            port.Equals("COM2", StringComparison.OrdinalIgnoreCase) ||
            port.StartsWith("SHRFAX", StringComparison.OrdinalIgnoreCase))
        {
            return PrinterConnectionType.Local;
        }

        return PrinterConnectionType.Unknown;
    }
}

/// <summary>Converts spooler data into the domain model without inventing values.</summary>
public static class PrinterMapper
{
    public static PrinterDevice Map(SpoolerPrinterData data, DateTime? detectedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        var connectionType = PortClassifier.Classify(data.PortName, data.IpAddress);
        var modelName = PrinterModelInfo.ResolveModel(data.Name, data.DriverName);
        var manufacturer = PrinterModelInfo.ResolveManufacturer(data.Name, data.DriverName);

        return new PrinterDevice
        {
            Id = data.Name.Trim().ToLowerInvariant(),
            Name = data.Name.Trim(),
            DriverName = data.DriverName?.Trim() ?? string.Empty,
            DriverManufacturer = data.DriverManufacturer,
            DriverVersion = data.DriverVersion,
            DriverInfName = data.DriverInfName,
            PortName = data.PortName?.Trim() ?? string.Empty,
            ConnectionType = connectionType,
            IpAddress = string.IsNullOrWhiteSpace(data.IpAddress) ? null : data.IpAddress.Trim(),
            PortNumber = data.PortNumber,
            Condition = ResolveCondition(data.PrinterStatus, data.WorkOffline),
            WorkOffline = data.WorkOffline,
            IsDefault = data.IsDefault,
            Location = data.Location,
            Comment = data.Comment,
            Manufacturer = manufacturer,
            ModelName = modelName,
            DetectedAtUtc = detectedAtUtc ?? DateTime.UtcNow
        };
    }

    private static PrinterCondition ResolveCondition(int printerStatus, bool workOffline)
    {
        if (workOffline)
        {
            return PrinterCondition.Offline;
        }

        // Values from Win32_Printer::PrinterStatus.
        return printerStatus switch
        {
            3 or 4 or 5 => PrinterCondition.Normal, // idle, printing, warmup
            6 => PrinterCondition.Error,            // stopped printing
            7 => PrinterCondition.Offline,          // offline
            _ => PrinterCondition.Unknown
        };
    }
}