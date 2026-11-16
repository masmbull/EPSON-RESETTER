using EpsonResetter.Core.Identification;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.Services;
using EpsonResetter.Core.Settings;
using EpsonResetter.Core.WebAdmin;

namespace EpsonResetter.Core.Diagnostics;

/// <summary>
/// Runs read-only checks against the Windows print subsystem and, for network
/// printers, the printer itself. No check modifies the printer.
/// </summary>
public sealed class DiagnosticsService : IDiagnosticsService
{
    private const int RawPrintPort = 9100;

    private readonly IPrinterProvider _printers;
    private readonly ISystemStatus _systemStatus;
    private readonly INetworkProbe _probe;
    private readonly AppSettings _settings;
    private readonly WebAdminAddressResolver _webAdminResolver;

    public DiagnosticsService(
        IPrinterProvider printers,
        ISystemStatus systemStatus,
        INetworkProbe probe,
        AppSettings settings)
    {
        _printers = printers ?? throw new ArgumentNullException(nameof(printers));
        _systemStatus = systemStatus ?? throw new ArgumentNullException(nameof(systemStatus));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _webAdminResolver = new WebAdminAddressResolver(settings);
    }

    public async Task<DiagnosticReport> RunAsync(PrinterDevice printer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(printer);

        var checks = new List<DiagnosticCheck>();

        checks.Add(_systemStatus.IsSpoolerRunning()
            ? DiagnosticCheck.Pass("Print Spooler", "The Windows Print Spooler service is running.")
            : DiagnosticCheck.Fail("Print Spooler", "The Windows Print Spooler service is not running."));

        var refreshed = await _printers.GetPrinterAsync(printer.Name, cancellationToken).ConfigureAwait(false);
        checks.Add(refreshed is null
            ? DiagnosticCheck.Fail("Spooler registration", $"The printer '{printer.Name}' is no longer registered with Windows.")
            : DiagnosticCheck.Pass("Spooler registration", "The printer is registered with the Windows print subsystem."));

        var target = refreshed ?? printer;

        checks.Add(string.IsNullOrWhiteSpace(target.DriverName)
            ? DiagnosticCheck.Warning("Printer driver", "No driver name was reported for this printer.")
            : DiagnosticCheck.Pass("Printer driver", $"Driver: {target.DriverName}"));

        checks.Add(string.IsNullOrWhiteSpace(target.PortName)
            ? DiagnosticCheck.Warning("Printer port", "No printer port is assigned.")
            : DiagnosticCheck.Pass("Printer port", $"Port: {target.PortName}"));

        checks.Add(target.ConnectionType == PrinterConnectionType.Unknown
            ? DiagnosticCheck.Warning("Connection type", "The connection type could not be determined from the port configuration.")
            : DiagnosticCheck.Pass("Connection type", $"Connection: {target.ConnectionDisplay}"));

        if (target.WorkOffline || target.Condition == PrinterCondition.Offline)
        {
            checks.Add(DiagnosticCheck.Fail("Printer state", "Windows reports the printer as offline or in use offline."));
        }
        else if (target.Condition == PrinterCondition.Error)
        {
            checks.Add(DiagnosticCheck.Warning("Printer state", "Windows reports a non-idle printer state."));
        }
        else
        {
            checks.Add(DiagnosticCheck.Pass("Printer state", "The printer is not marked offline."));
        }

        var address = WebAdminAddressResolver.ExtractAddress(target.IpAddress)
                      ?? WebAdminAddressResolver.ExtractAddress(target.PortName);

        if (address is not null)
        {
            await AddNetworkChecksAsync(checks, address, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            checks.Add(DiagnosticCheck.Skipped("Network reachability", "No IP address is available for this printer."));
            checks.Add(DiagnosticCheck.Skipped("Raw print port (9100)", "No IP address is available for this printer."));
        }

        var resolution = _webAdminResolver.Resolve(target);
        checks.Add(resolution.HasCandidates
            ? DiagnosticCheck.Pass("Web Admin address", $"Detected {resolution.PrimaryUrl}")
            : DiagnosticCheck.Warning("Web Admin address", resolution.Warnings.FirstOrDefault() ?? "No Web Admin address could be detected."));

        return new DiagnosticReport
        {
            PrinterName = target.Name,
            ModelName = target.ModelName,
            Checks = checks
        };
    }

    private async Task AddNetworkChecksAsync(List<DiagnosticCheck> checks, string address, CancellationToken cancellationToken)
    {
        var ping = await _probe.PingAsync(address, _settings.NetworkTimeoutMs, cancellationToken).ConfigureAwait(false);
        checks.Add(ping.IsReachable
            ? DiagnosticCheck.Pass("Network reachability", $"{address} answered an ICMP echo request.", $"{ping.ElapsedMilliseconds} ms")
            : DiagnosticCheck.Warning("Network reachability", $"{address} did not answer an ICMP echo request.", ping.Error));

        var rawPort = await _probe.TcpConnectAsync(address, RawPrintPort, _settings.NetworkTimeoutMs, cancellationToken).ConfigureAwait(false);
        checks.Add(rawPort.IsReachable
            ? DiagnosticCheck.Pass("Raw print port (9100)", $"TCP {RawPrintPort} on {address} is open.")
            : DiagnosticCheck.Warning("Raw print port (9100)", $"TCP {RawPrintPort} on {address} is not reachable.", rawPort.Error));
    }
}