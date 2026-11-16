using System.Management;
using EpsonResetter.Core.Services;
using Serilog;

namespace EpsonResetter.Infrastructure.Printing;

/// <summary>Reports the state of the Windows Print Spooler service via WMI.</summary>
public sealed class WindowsSystemStatus : ISystemStatus
{
    private static readonly ILogger Log = Serilog.Log.ForContext<WindowsSystemStatus>();

    public bool IsSpoolerRunning()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT State FROM Win32_Service WHERE Name = 'Spooler'");

            foreach (ManagementObject service in searcher.Get())
            {
                return service["State"] as string == "Running";
            }

            return false;
        }
        catch (ManagementException exception)
        {
            Log.Warning(exception, "Could not query the Print Spooler service state.");
            return false;
        }
    }

    public string SpoolerStatusText => IsSpoolerRunning() ? "Running" : "Not running";
}