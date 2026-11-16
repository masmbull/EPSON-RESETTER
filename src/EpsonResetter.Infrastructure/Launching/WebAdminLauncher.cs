using System.Diagnostics;
using EpsonResetter.Core.Services;
using EpsonResetter.Core.WebAdmin;
using Serilog;

namespace EpsonResetter.Infrastructure.Launching;

/// <summary>
/// Opens a validated Web Admin URL with the operating system's default browser.
/// TLS certificate validation is never modified and no credentials are ever added.
/// </summary>
public sealed class WebAdminLauncher : IWebAdminLauncher
{
    private static readonly ILogger Log = Serilog.Log.ForContext<WebAdminLauncher>();

    public Task<bool> OpenAsync(string? url, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = WebAdminUrlValidator.Validate(url);
        if (!validation.IsValid)
        {
            Log.Warning("Refused to open Web Admin address: {Error}", validation.Error);
            return Task.FromResult(false);
        }

        try
        {
            Process.Start(new ProcessStartInfo(validation.NormalizedUrl!)
            {
                UseShellExecute = true
            });

            Log.Information("Opened Web Admin address {Url} in the default browser.", validation.NormalizedUrl);
            return Task.FromResult(true);
        }
    catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Log.Error(exception, "Failed to open Web Admin address {Url}.", validation.NormalizedUrl);
            return Task.FromResult(false);
        }
    }
}