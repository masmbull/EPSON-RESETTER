using System.Management;
using EpsonResetter.Core.Identification;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.Services;
using Serilog;

namespace EpsonResetter.Infrastructure.Printing;

/// <summary>
/// Reads printers, drivers and TCP/IP ports from the Windows print subsystem
/// through WMI. Every value returned comes from a live query; if the query fails
/// the caller receives an empty list plus a logged error rather than sample data.
/// </summary>
public sealed class SpoolerPrinterProvider : IPrinterProvider
{
    private static readonly ILogger Log = Serilog.Log.ForContext<SpoolerPrinterProvider>();

    public Task<IReadOnlyList<PrinterDevice>> GetPrintersAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var printers = new List<PrinterDevice>();
            var drivers = ReadDrivers();
            var tcpPorts = ReadTcpPorts();

            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DriverName, PortName, WorkOffline, PrinterStatus, Default, Location, Comment FROM Win32_Printer");

            try
            {
                foreach (ManagementObject printer in searcher.Get())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var name = GetString(printer, "Name");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var driverName = GetString(printer, "DriverName");
                    var portName = GetString(printer, "PortName");

                    (string? Version, string? InfName)? driver =
                        driverName is not null && drivers.TryGetValue(driverName, out var foundDriver) ? foundDriver : null;
                    (string? HostAddress, int PortNumber)? tcp =
                        tcpPorts.TryGetValue(portName ?? string.Empty, out var foundPort) ? foundPort : null;

                    printers.Add(PrinterMapper.Map(new SpoolerPrinterData
                    {
                        Name = name,
                        DriverName = driverName,
                        PortName = portName,
                        WorkOffline = GetBool(printer, "WorkOffline"),
                        PrinterStatus = GetInt(printer, "PrinterStatus"),
                        IsDefault = GetBool(printer, "Default"),
                        Location = GetString(printer, "Location"),
                        Comment = GetString(printer, "Comment"),
                        IpAddress = tcp?.HostAddress,
                        PortNumber = tcp?.PortNumber,
                        DriverManufacturer = PrinterModelInfo.ResolveManufacturer(driverName, null),
                        DriverVersion = driver?.Version,
                        DriverInfName = driver?.InfName
                    }));
                }
            }
            catch (ManagementException exception)
            {
                Log.Error(exception, "WMI printer query failed.");
                throw;
            }

            return (IReadOnlyList<PrinterDevice>)printers
                .GroupBy(printer => printer.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(printer => printer.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, cancellationToken);
    }

    public async Task<PrinterDevice?> GetPrinterAsync(string printerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return null;
        }

        var printers = await GetPrintersAsync(cancellationToken).ConfigureAwait(false);
        return printers.FirstOrDefault(printer =>
            string.Equals(printer.Name, printerName, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, (string? Version, string? InfName)> ReadDrivers()
    {
        var result = new Dictionary<string, (string?, string?)>(StringComparer.OrdinalIgnoreCase);

        // Win32_PrinterDriver exposes Version and InfName; it has no Manufacturer or
        // DriverVersion property, so selecting those would make the whole query fail.
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, Version, InfName FROM Win32_PrinterDriver");

        try
        {
            foreach (ManagementObject driver in searcher.Get())
            {
                var name = GetString(driver, "Name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                // Win32_PrinterDriver names carry a ",version,platform" suffix that
                // Win32_Printer.DriverName does not have.
                var baseName = name.Split(',')[0];
                var entry = (Version: GetText(driver, "Version"), InfName: GetText(driver, "InfName"));

                result[name] = entry;
                result[baseName] = entry;
            }
        }
        catch (ManagementException exception)
        {
            Log.Warning(exception, "WMI driver query failed; driver details will be incomplete.");
        }

        return result;
    }

    private static Dictionary<string, (string? HostAddress, int PortNumber)> ReadTcpPorts()
    {
        var result = new Dictionary<string, (string?, int)>(StringComparer.OrdinalIgnoreCase);

        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, HostAddress, PortNumber FROM Win32_TCPIPPrinterPort");

        try
        {
            foreach (ManagementObject port in searcher.Get())
            {
                var name = GetString(port, "Name");
                var host = GetString(port, "HostAddress");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(host))
                {
                    continue;
                }

                var portNumber = GetInt(port, "PortNumber", 9100);
                result[name] = (host, portNumber);
            }
        }
        catch (ManagementException exception)
        {
            Log.Warning(exception, "WMI TCP/IP port query failed; network addresses may be unavailable.");
        }

        return result;
    }

    private static string? GetString(ManagementObject source, string property)
    {
        try
        {
            return source[property] as string;
        }
        catch (ManagementException)
        {
            return null;
        }
    }

    private static string? GetText(ManagementObject source, string property)
    {
        try
        {
            return source[property] switch
            {
                null => null,
                string text => string.IsNullOrWhiteSpace(text) ? null : text,
                IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                var value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            };
        }
        catch (Exception exception) when (exception is ManagementException or FormatException or InvalidCastException)
        {
            return null;
        }
    }

    private static bool GetBool(ManagementObject source, string property)
    {
        try
        {
            return source[property] is bool value && value;
        }
        catch (ManagementException)
        {
            return false;
        }
    }

    private static int GetInt(ManagementObject source, string property, int fallback = 0)
    {
        try
        {
            return source[property] switch
            {
                null => fallback,
                int value => value,
                IConvertible convertible => Convert.ToInt32(convertible, System.Globalization.CultureInfo.InvariantCulture),
                _ => fallback
            };
        }
        catch (Exception exception) when (exception is ManagementException or FormatException or InvalidCastException or OverflowException)
        {
            return fallback;
        }
    }
}