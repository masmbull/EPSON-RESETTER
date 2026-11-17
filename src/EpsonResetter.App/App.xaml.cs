using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using EpsonResetter.App.Services;
using EpsonResetter.App.ViewModels;
using EpsonResetter.App.Views;
using EpsonResetter.Core.Models;
using EpsonResetter.Infrastructure.Persistence;
using EpsonResetter.Infrastructure.Printing;
using Serilog;

namespace EpsonResetter.App;

public partial class App : Application
{
    private const int AttachParentProcess = -1;

    private static bool _headless;

    [DllImport("kernel32.dll", SetLastError = true)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600", Justification = "P/Invoke")]
    private static extern bool AttachConsole(int dwProcessId);

    private static ShellViewModel? _shell;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigureLogging();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        var args = e.Args;
        _headless = args.Any(argument => argument is "--scan" or "--ui-smoke" or "--version");

        if (args.Contains("--version", StringComparer.OrdinalIgnoreCase))
        {
            WriteToConsole($"EPSON RESETTER {VersionText}");
            Shutdown(0);
            return;
        }

        if (args.Contains("--ui-smoke", StringComparer.OrdinalIgnoreCase))
        {
            RunUiSmoke();
            return;
        }

        if (args.Contains("--scan", StringComparer.OrdinalIgnoreCase))
        {
            RunHeadlessScan(args);
            Shutdown(0);
            return;
        }

        try
        {
            var services = new AppServices(LoadSettings());
            _shell = new ShellViewModel(services, new MessageBoxDialogService());

            MainWindow = new MainWindow { DataContext = _shell };
            MainWindow.Show();

            _ = _shell.InitializeAsync();
        }
        catch (Exception exception)
        {
            Serilog.Log.Fatal(exception, "Application startup failed.");
            if (!_headless)
            {
                MessageBox.Show(
                    "EPSON RESETTER could not start.\n\n" + exception.Message,
                    "EPSON RESETTER",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            Shutdown(-1);
        }
    }

    /// <summary>
    /// Creates the real window and activates every page on a dispatcher frame.
    /// Used by the build pipeline to catch XAML and binding errors without a user.
    /// </summary>
    private void RunUiSmoke()
    {
        var failures = new List<string>();

        try
        {
            var services = new AppServices(LoadSettings());
            var shell = new ShellViewModel(services, new MessageBoxDialogService());

            var sample = new PrinterDevice
            {
                Id = "smoke-epson",
                Name = "EPSON ET-2850",
                ModelName = "ET-2850",
                DriverName = "EPSON ET-2850 Series",
                PortName = "192.168.1.50",
                IpAddress = "192.168.1.50",
                PortNumber = 9100,
                ConnectionType = PrinterConnectionType.Network,
                Condition = PrinterCondition.Normal,
                Manufacturer = "Epson",
                DetectedAtUtc = DateTime.UtcNow
            };

            shell.State.Printers = new ObservableCollection<PrinterDevice>(new[] { sample });
            shell.State.SelectedPrinter = sample;

            var window = new MainWindow { DataContext = shell };
            window.Show();

            var frame = new DispatcherFrame();

            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                try
                {
                    await shell.NavigateToAsync(typeof(DashboardViewModel));
                    await shell.NavigateToAsync(typeof(PrintersViewModel));
                    await shell.NavigateToAsync(typeof(PrinterDetailsViewModel), sample);
                    await shell.NavigateToAsync(typeof(DiagnosticsViewModel));
                    await shell.NavigateToAsync(typeof(MaintenanceViewModel));
                    await shell.NavigateToAsync(typeof(WasteInkViewModel));
                    await shell.NavigateToAsync(typeof(HistoryViewModel));
                    await shell.NavigateToAsync(typeof(SettingsViewModel));

                    window.UpdateLayout();

                    WriteToConsole("UI smoke test passed: every page was created and laid out.");
                    Serilog.Log.Information("UI smoke test passed: every page was created and laid out.");
                }
                catch (Exception exception)
                {
                    failures.Add(exception.ToString());
                    Serilog.Log.Error(exception, "UI smoke test failed.");
                    WriteToConsole("UI smoke test failed: " + exception);
                }
                finally
                {
                    frame.Continue = false;
                    window.Close();
                }
            }));

            Dispatcher.PushFrame(frame);

            if (failures.Count > 0)
            {
                Shutdown(1);
            }
            else
            {
                Shutdown(0);
            }
        }
        catch (Exception exception)
        {
            Serilog.Log.Error(exception, "UI smoke test could not start.");
            WriteToConsole("UI smoke test failed: " + exception);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shell?.Dispose();
        Serilog.Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static string VersionText =>
        $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    private static EpsonResetter.Core.Settings.AppSettings LoadSettings()
    {
        try
        {
            return new SqliteSettingsStore().LoadAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            Serilog.Log.Warning(exception, "Settings could not be loaded; defaults are used.");
            return new EpsonResetter.Core.Settings.AppSettings();
        }
    }

    private static void ConfigureLogging()
    {
        Directory.CreateDirectory(DatabasePaths.LogDirectory);

        Serilog.Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(DatabasePaths.LogDirectory, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private static void RunHeadlessScan(string[] args)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), "epson-resetter-scan.json");
        var outIndex = Array.IndexOf(args, "--out");
        if (outIndex >= 0 && outIndex + 1 < args.Length)
        {
            outputPath = args[outIndex + 1];
        }

        try
        {
            var printers = new SpoolerPrinterProvider().GetPrintersAsync().GetAwaiter().GetResult();

            var payload = new
            {
                generatedAtUtc = DateTime.UtcNow,
                machine = Environment.MachineName,
                count = printers.Count,
                printers = printers.Select(printer => new
                {
                    printer.Name,
                    printer.ModelName,
                    printer.Manufacturer,
                    printer.IsEpson,
                    printer.DriverName,
                    printer.DriverManufacturer,
                    printer.DriverVersion,
                    printer.DriverInfName,
                    printer.PortName,
                    Connection = printer.ConnectionType.ToString(),
                    printer.IpAddress,
                    Condition = printer.Condition.ToString(),
                    printer.IsDefault,
                    printer.Location,
                    printer.DetectedAtUtc
                })
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(outputPath, json);

            WriteToConsole(json);
            Serilog.Log.Information("Headless scan found {Count} printers; output written to {Path}", printers.Count, outputPath);
        }
        catch (Exception exception)
        {
            Serilog.Log.Error(exception, "Headless scan failed.");
            WriteToConsole("ERROR: " + exception.Message);
        }
    }

    private static void WriteToConsole(string message)
    {
        try
        {
            AttachConsole(AttachParentProcess);
            Console.WriteLine(message);
        }
        catch (Exception exception)
        {
            Serilog.Log.Debug(exception, "Console output was not available.");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Serilog.Log.Fatal(e.Exception, "Unhandled UI exception.");
        if (!_headless)
        {
            MessageBox.Show(
                "An unexpected error occurred.\n\n" + e.Exception.Message,
                "EPSON RESETTER",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Serilog.Log.Fatal(exception, "Unhandled non-UI exception.");
        }
    }
}