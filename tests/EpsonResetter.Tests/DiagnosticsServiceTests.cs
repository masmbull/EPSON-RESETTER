using EpsonResetter.Core.Diagnostics;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.Services;
using EpsonResetter.Core.Settings;

namespace EpsonResetter.Tests;

public class DiagnosticsServiceTests
{
    private sealed class FakePrinterProvider : IPrinterProvider
    {
        private readonly PrinterDevice? _printer;

        public FakePrinterProvider(PrinterDevice? printer) => _printer = printer;

        public Task<IReadOnlyList<PrinterDevice>> GetPrintersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PrinterDevice>>(_printer is null ? Array.Empty<PrinterDevice>() : new[] { _printer });

        public Task<PrinterDevice?> GetPrinterAsync(string printerName, CancellationToken cancellationToken = default) =>
            Task.FromResult<PrinterDevice?>(_printer?.Name == printerName ? _printer : null);
    }

    private sealed class FakeProbe : INetworkProbe
    {
        public bool PingReachable { get; set; } = true;
        public bool TcpReachable { get; set; } = true;

        public Task<NetworkProbeResult> PingAsync(string host, int timeoutMs, CancellationToken cancellationToken = default) =>
            Task.FromResult(PingReachable ? NetworkProbeResult.Reachable(5) : NetworkProbeResult.Unreachable("No route to host."));

        public Task<NetworkProbeResult> TcpConnectAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken = default) =>
            Task.FromResult(TcpReachable ? NetworkProbeResult.Reachable(3) : NetworkProbeResult.Unreachable("Connection refused."));
    }

    private sealed class FakeSystemStatus : ISystemStatus
    {
        public bool Running { get; set; } = true;

        public bool IsSpoolerRunning() => Running;

        public string SpoolerStatusText => Running ? "Running" : "Not running";
    }

    private static PrinterDevice NetworkPrinter(bool workOffline = false) => new()
    {
        Id = "epson-et2850",
        Name = "EPSON ET-2850",
        ModelName = "ET-2850",
        DriverName = "EPSON ET-2850",
        PortName = "192.168.1.50",
        IpAddress = "192.168.1.50",
        ConnectionType = PrinterConnectionType.Network,
        Condition = workOffline ? PrinterCondition.Offline : PrinterCondition.Normal,
        WorkOffline = workOffline,
        DetectedAtUtc = DateTime.UtcNow
    };

    private static DiagnosticsService Build(
        PrinterDevice? printer,
        bool spoolerRunning = true,
        bool pingReachable = true,
        bool tcpReachable = true) =>
        new(
            new FakePrinterProvider(printer),
            new FakeSystemStatus { Running = spoolerRunning },
            new FakeProbe { PingReachable = pingReachable, TcpReachable = tcpReachable },
            new AppSettings { NetworkTimeoutMs = 500 });

    [Fact]
    public async Task Healthy_Network_Printer_Has_No_Failures()
    {
        var report = await Build(NetworkPrinter()).RunAsync(NetworkPrinter());

        Assert.False(report.HasFailures);
        Assert.Contains("Raw print port (9100)", report.Checks.Select(check => check.Name));
        Assert.Contains("Network reachability", report.Checks.Select(check => check.Name));
        Assert.Contains("Web Admin address", report.Checks.Select(check => check.Name));
    }

    [Fact]
    public async Task Stopped_Spooler_Is_A_Failure()
    {
        var printer = NetworkPrinter();

        var report = await Build(printer, spoolerRunning: false).RunAsync(printer);

        var check = report.Checks.Single(item => item.Name == "Print Spooler");
        Assert.Equal(DiagnosticStatus.Fail, check.Status);
        Assert.True(report.HasFailures);
    }

    [Fact]
    public async Task Offline_Printer_Is_Reported()
    {
        var printer = NetworkPrinter(workOffline: true);

        var report = await Build(printer).RunAsync(printer);

        var check = report.Checks.Single(item => item.Name == "Printer state");
        Assert.Equal(DiagnosticStatus.Fail, check.Status);
    }

    [Fact]
    public async Task Printer_Missing_From_Spooler_Is_A_Failure()
    {
        var printer = NetworkPrinter();

        var report = await Build(printer: null).RunAsync(printer);

        var check = report.Checks.Single(item => item.Name == "Spooler registration");
        Assert.Equal(DiagnosticStatus.Fail, check.Status);
    }

    [Fact]
    public async Task Unreachable_Network_Is_A_Warning_Not_A_Success()
    {
        var printer = NetworkPrinter();

        var report = await Build(printer, pingReachable: false, tcpReachable: false).RunAsync(printer);

        Assert.True(report.HasWarnings);
        Assert.Equal(DiagnosticStatus.Warning, report.Checks.Single(item => item.Name == "Network reachability").Status);
        Assert.Equal(DiagnosticStatus.Warning, report.Checks.Single(item => item.Name == "Raw print port (9100)").Status);
    }

    [Fact]
    public async Task Non_Network_Printer_Skips_Network_Checks()
    {
        var printer = new PrinterDevice
        {
            Id = "usb",
            Name = "EPSON ET-2850",
            ModelName = "ET-2850",
            DriverName = "EPSON ET-2850",
            PortName = "USB001",
            ConnectionType = PrinterConnectionType.Usb,
            DetectedAtUtc = DateTime.UtcNow
        };

        var report = await Build(printer).RunAsync(printer);

        Assert.Equal(DiagnosticStatus.Skipped, report.Checks.Single(item => item.Name == "Network reachability").Status);
        Assert.Equal(DiagnosticStatus.Skipped, report.Checks.Single(item => item.Name == "Raw print port (9100)").Status);
        Assert.False(report.HasFailures);
    }

    [Fact]
    public async Task Missing_Driver_Is_A_Warning()
    {
        var printer = new PrinterDevice
        {
            Id = "usb",
            Name = "EPSON ET-2850",
            ModelName = "ET-2850",
            PortName = "USB001",
            ConnectionType = PrinterConnectionType.Usb,
            DetectedAtUtc = DateTime.UtcNow
        };

        var report = await Build(printer).RunAsync(printer);

        Assert.Equal(DiagnosticStatus.Warning, report.Checks.Single(item => item.Name == "Printer driver").Status);
    }

    [Fact]
    public async Task Summary_Reflects_Check_Results()
    {
        var printer = NetworkPrinter();

        var report = await Build(printer, spoolerRunning: false).RunAsync(printer);

        Assert.Contains("failed", report.Summary);
        Assert.Equal(report.Checks.Count, report.Checks.Count);
    }
}