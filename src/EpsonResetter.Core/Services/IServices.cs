using EpsonResetter.Core.Models;
using EpsonResetter.Core.Settings;

namespace EpsonResetter.Core.Services;

public interface IPrinterProvider
{
    Task<IReadOnlyList<PrinterDevice>> GetPrintersAsync(CancellationToken cancellationToken = default);

    Task<PrinterDevice?> GetPrinterAsync(string printerName, CancellationToken cancellationToken = default);
}

public sealed class NetworkProbeResult
{
    public bool IsReachable { get; init; }

    public long ElapsedMilliseconds { get; init; }

    public string? Error { get; init; }

    public static NetworkProbeResult Reachable(long elapsed) => new() { IsReachable = true, ElapsedMilliseconds = elapsed };

    public static NetworkProbeResult Unreachable(string? error, long elapsed = 0) =>
        new() { IsReachable = false, ElapsedMilliseconds = elapsed, Error = error };
}

public interface INetworkProbe
{
    Task<NetworkProbeResult> PingAsync(string host, int timeoutMs, CancellationToken cancellationToken = default);

    Task<NetworkProbeResult> TcpConnectAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken = default);
}

public interface ISystemStatus
{
    bool IsSpoolerRunning();

    string SpoolerStatusText { get; }
}

public interface IDiagnosticsService
{
    Task<DiagnosticReport> RunAsync(PrinterDevice printer, CancellationToken cancellationToken = default);
}

public interface IWebAdminLauncher
{
    Task<bool> OpenAsync(string url, CancellationToken cancellationToken = default);
}

public interface IPrinterMaintenanceService
{
    Task<bool> OpenQueueAsync(string printerName, CancellationToken cancellationToken = default);

    Task<bool> OpenPropertiesAsync(string printerName, CancellationToken cancellationToken = default);

    Task<bool> PrintTestPageAsync(string printerName, CancellationToken cancellationToken = default);
}

public interface IActivityStore
{
    Task AddAsync(ActivityRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActivityRecord>> ListAsync(int maxRecords = 200, CancellationToken cancellationToken = default);

    Task<int> ClearAsync(CancellationToken cancellationToken = default);
}

public interface IPrinterCacheStore
{
    Task SaveAsync(IReadOnlyList<PrinterDevice> printers, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrinterDevice>> LoadAsync(CancellationToken cancellationToken = default);
}

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}