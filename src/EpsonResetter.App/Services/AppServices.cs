using EpsonResetter.Core.Compatibility;
using EpsonResetter.Core.Diagnostics;
using EpsonResetter.Core.Services;
using EpsonResetter.Core.Settings;
using EpsonResetter.Core.WasteInk;
using EpsonResetter.Core.WebAdmin;
using EpsonResetter.Infrastructure.Launching;
using EpsonResetter.Infrastructure.Network;
using EpsonResetter.Infrastructure.Persistence;
using EpsonResetter.Infrastructure.Printing;

namespace EpsonResetter.App.Services;

/// <summary>Composition root: one instance of every service used by the UI.</summary>
public sealed class AppServices
{
    public AppServices(AppSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));

        SettingsStore = new SqliteSettingsStore();
        Activity = new SqliteActivityStore();
        PrinterCache = new SqlitePrinterCacheStore();

        Printers = new SpoolerPrinterProvider();
        SystemStatus = new WindowsSystemStatus();
        Probe = new NetworkProbe();

        Diagnostics = new DiagnosticsService(Printers, SystemStatus, Probe, Settings);
        WebAdmin = new WebAdminAddressResolver(Settings);
        Launcher = new WebAdminLauncher();
        Maintenance = new WindowsMaintenanceService();

        Registry = ModelCompatibilityRegistry.CreateDefault();
        CounterReader = new RegistryBackedWasteInkCounterReader();
        Eligibility = new ResetEligibilityService(Registry);
    }

    public AppSettings Settings { get; }

    public ISettingsStore SettingsStore { get; }

    public IActivityStore Activity { get; }

    public IPrinterCacheStore PrinterCache { get; }

    public IPrinterProvider Printers { get; }

    public ISystemStatus SystemStatus { get; }

    public INetworkProbe Probe { get; }

    public IDiagnosticsService Diagnostics { get; }

    public WebAdminAddressResolver WebAdmin { get; }

    public IWebAdminLauncher Launcher { get; }

    public IPrinterMaintenanceService Maintenance { get; }

    public ModelCompatibilityRegistry Registry { get; }

    public IWasteInkCounterReader CounterReader { get; }

    public ResetEligibilityService Eligibility { get; }
}