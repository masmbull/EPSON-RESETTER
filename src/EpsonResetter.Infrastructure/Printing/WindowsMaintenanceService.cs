using System.Diagnostics;
using EpsonResetter.Core.Services;
using Serilog;

namespace EpsonResetter.Infrastructure.Printing;

/// <summary>
/// Opens supported Windows print dialogs for a printer. The actions are limited to
/// the built-in printui entry points (queue, properties, test page); no vendor
/// service utility is invoked.
/// </summary>
public sealed class WindowsMaintenanceService : IPrinterMaintenanceService
{
    private static readonly ILogger Log = Serilog.Log.ForContext<WindowsMaintenanceService>();

    public Task<bool> OpenQueueAsync(string printerName, CancellationToken cancellationToken = default) =>
        RunPrintUiAsync(printerName, "/o", cancellationToken);

    public Task<bool> OpenPropertiesAsync(string printerName, CancellationToken cancellationToken = default) =>
        RunPrintUiAsync(printerName, "/p", cancellationToken);

    public Task<bool> PrintTestPageAsync(string printerName, CancellationToken cancellationToken = default) =>
        RunPrintUiAsync(printerName, "/k", cancellationToken);

    private static Task<bool> RunPrintUiAsync(string printerName, string action, CancellationToken cancellationToken)
    {
        if (!IsSafePrinterName(printerName))
        {
            Log.Warning("Rejected maintenance action {Action} for invalid printer name '{Printer}'.", action, printerName);
            return Task.FromResult(false);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "rundll32.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("printui.dll,PrintUIEntry");
            startInfo.ArgumentList.Add(action);
            startInfo.ArgumentList.Add("/n");
            startInfo.ArgumentList.Add(printerName);

            var process = Process.Start(startInfo);
            if (process is null)
            {
                return Task.FromResult(false);
            }

            Log.Information("Launched printui {Action} for printer '{Printer}'.", action, printerName);
            return Task.FromResult(true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Log.Error(exception, "Failed to launch printui {Action} for '{Printer}'.", action, printerName);
            return Task.FromResult(false);
        }
    }

    public static bool IsSafePrinterName(string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return false;
        }

        var trimmed = printerName.Trim();
        if (trimmed.Length > 200)
        {
            return false;
        }

        foreach (var character in trimmed)
        {
            if (char.IsControl(character) || character == '"' || character == '\'' || character == ';')
            {
                return false;
            }
        }

        return true;
    }
}