using System.Collections.ObjectModel;
using System.Threading;
using EpsonResetter.App.Services;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.Services;
using EpsonResetter.Core.Settings;
using EpsonResetter.Core.WasteInk;
using Microsoft.Data.Sqlite;

namespace EpsonResetter.Tests;

public sealed class FakePrinterProvider : IPrinterProvider
{
    private readonly IReadOnlyList<PrinterDevice> _printers;

    public FakePrinterProvider(params PrinterDevice[] printers) => _printers = printers;
    public FakePrinterProvider(IReadOnlyList<PrinterDevice> printers) => _printers = printers;

    public Task<IReadOnlyList<PrinterDevice>> GetPrintersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_printers);

    public Task<PrinterDevice?> GetPrinterAsync(string printerName, CancellationToken cancellationToken = default) =>
        Task.FromResult(_printers.FirstOrDefault(p => p.Name == printerName));
}

public sealed class FakeProbe : INetworkProbe
{
    public bool PingReachable { get; set; } = true;
    public bool TcpReachable { get; set; } = true;

    public Task<NetworkProbeResult> PingAsync(string host, int timeoutMs, CancellationToken cancellationToken = default) =>
        Task.FromResult(PingReachable ? NetworkProbeResult.Reachable(5) : NetworkProbeResult.Unreachable("No route to host."));

    public Task<NetworkProbeResult> TcpConnectAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken = default) =>
        Task.FromResult(TcpReachable ? NetworkProbeResult.Reachable(3) : NetworkProbeResult.Unreachable("Connection refused."));
}

public sealed class FakeSystemStatus : ISystemStatus
{
    public bool Running { get; set; } = true;

    public bool IsSpoolerRunning() => Running;
    public string SpoolerStatusText => Running ? "Running" : "Not running";
}

public sealed class FakeMaintenanceService : IPrinterMaintenanceService
{
    public bool OpenQueueCalled { get; private set; }
    public bool OpenPropertiesCalled { get; private set; }
    public bool PrintTestPageCalled { get; private set; }
    public string? LastPrinterName { get; private set; }

    public Task<bool> OpenQueueAsync(string printerName, CancellationToken cancellationToken = default)
    {
        OpenQueueCalled = true;
        LastPrinterName = printerName;
        return Task.FromResult(true);
    }

    public Task<bool> OpenPropertiesAsync(string printerName, CancellationToken cancellationToken = default)
    {
        OpenPropertiesCalled = true;
        LastPrinterName = printerName;
        return Task.FromResult(true);
    }

    public Task<bool> PrintTestPageAsync(string printerName, CancellationToken cancellationToken = default)
    {
        PrintTestPageCalled = true;
        LastPrinterName = printerName;
        return Task.FromResult(true);
    }
}

public sealed class FakeLauncher : IWebAdminLauncher
{
    public List<string> OpenedUrls = new();
    public bool ShouldSucceed = true;

    public Task<bool> OpenAsync(string url, CancellationToken cancellationToken = default)
    {
        OpenedUrls.Add(url);
        return Task.FromResult(ShouldSucceed);
    }
}

public sealed class FakeDialogService : IDialogService
{
    public bool ConfirmResult { get; set; } = true;
    public List<(string Message, string Title, string? Details)> ConfirmCalls = new();

    public bool Confirm(string message, string title, string? details = null)
    {
        ConfirmCalls.Add((message, title, details));
        return ConfirmResult;
    }
}

public static class TestPrinters
{
    public static PrinterDevice NetworkPrinter(string name = "EPSON ET-2850", string ip = "192.168.1.50") => new()
    {
        Id = "epson-et2850",
        Name = name,
        ModelName = "ET-2850",
        DriverName = "EPSON ET-2850",
        PortName = ip,
        IpAddress = ip,
        ConnectionType = PrinterConnectionType.Network,
        Condition = PrinterCondition.Normal,
        Manufacturer = "Epson",
        IsDefault = true,
        DetectedAtUtc = DateTime.UtcNow
    };

    public static PrinterDevice UsbPrinter(string name = "EPSON L3250") => new()
    {
        Id = "usb-l3250",
        Name = name,
        ModelName = "L3250",
        DriverName = "EPSON L3250 Series",
        PortName = "USB001",
        ConnectionType = PrinterConnectionType.Usb,
        Condition = PrinterCondition.Normal,
        Manufacturer = "Epson",
        DetectedAtUtc = DateTime.UtcNow
    };

    public static PrinterDevice OfflinePrinter() => new()
    {
        Id = "epson-offline",
        Name = "EPSON Offline",
        ModelName = "ET-2850",
        DriverName = "EPSON ET-2850",
        PortName = "10.0.0.99",
        IpAddress = "10.0.0.99",
        ConnectionType = PrinterConnectionType.Network,
        Condition = PrinterCondition.Offline,
        Manufacturer = "Epson",
        IsDefault = false,
        DetectedAtUtc = DateTime.UtcNow
    };
}
