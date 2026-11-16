using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EpsonResetter.App.Services;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.Settings;
using Serilog;

namespace EpsonResetter.App.ViewModels;

/// <summary>Shared, observable application state passed to every page.</summary>
public sealed partial class AppState : ObservableObject
{
    private readonly AppServices _services;

    public AppState(AppServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        Settings = services.Settings;
    }

    public AppSettings Settings { get; }

    [ObservableProperty]
    private PrinterDevice? _selectedPrinter;

    [ObservableProperty]
    private ObservableCollection<PrinterDevice> _printers = new();

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private DateTime? _lastScan;

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    public ObservableCollection<ActivityRecord> RecentActivity { get; } = new();

    public int EpsonCount => Printers.Count(printer => printer.IsEpson);

    public int NetworkCount => Printers.Count(printer => printer.IsNetwork);

    public int OfflineCount => Printers.Count(printer => printer.Condition is PrinterCondition.Offline or PrinterCondition.Error);

    public int UnknownCount => Printers.Count(printer => printer.Condition == PrinterCondition.Unknown);

    partial void OnPrintersChanged(ObservableCollection<PrinterDevice> value)
    {
        OnPropertyChanged(nameof(EpsonCount));
        OnPropertyChanged(nameof(NetworkCount));
        OnPropertyChanged(nameof(OfflineCount));
        OnPropertyChanged(nameof(UnknownCount));
    }

    public async Task<bool> ScanAsync(CancellationToken cancellationToken = default)
    {
        if (IsScanning)
        {
            return false;
        }

        IsScanning = true;
        StatusMessage = "Scanning the Windows print subsystem...";

        try
        {
            var printers = await _services.Printers.GetPrintersAsync(cancellationToken).ConfigureAwait(true);

            Printers = new ObservableCollection<PrinterDevice>(printers);

            if (SelectedPrinter is not null)
            {
                SelectedPrinter = printers.FirstOrDefault(printer => printer.Id == SelectedPrinter.Id);
            }

            await _services.PrinterCache.SaveAsync(printers, cancellationToken).ConfigureAwait(true);

            LastScan = DateTime.Now;
            StatusMessage = printers.Count == 0
                ? "Scan finished: no printers were reported by Windows."
                : $"Scan finished: {printers.Count} printer(s) found at {LastScan:HH:mm:ss}. " +
                  $"{EpsonCount} Epson, {NetworkCount} network, {OfflineCount} offline.";

            return true;
        }
        catch (Exception exception)
        {
            Serilog.Log.Error(exception, "Printer scan failed.");
            StatusMessage = "Printer scan failed: " + exception.Message;
            return false;
        }
        finally
        {
            IsScanning = false;
        }
    }

    public async Task LoadActivityAsync(int maxRecords = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var records = await _services.Activity.ListAsync(maxRecords, cancellationToken).ConfigureAwait(true);

            RecentActivity.Clear();
            foreach (var record in records)
            {
                RecentActivity.Add(record);
            }
        }
        catch (Exception exception)
        {
            Serilog.Log.Warning(exception, "Activity history could not be loaded.");
            StatusMessage = "Activity history could not be loaded: " + exception.Message;
        }
    }
}