using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsonResetter.App.Services;
using EpsonResetter.Core.Models;
using Serilog;

namespace EpsonResetter.App.ViewModels;

public sealed partial class PrinterDetailsViewModel : ViewModelBase, INavigationPage
{
    private readonly IDialogService _dialogs;
    private readonly NavigateHandler _navigate;
    private readonly AppServices _services;
    private readonly AppState _state;

    public PrinterDetailsViewModel(AppServices services, AppState state, IDialogService dialogs, NavigateHandler navigate)
    {
        _services = services;
        _state = state;
        _dialogs = dialogs;
        _navigate = navigate;
    }

    public string Title => "Printer Details";

    [ObservableProperty]
    private PrinterDevice? _printer;

    [ObservableProperty]
    private bool _hasPrinter;

    [ObservableProperty]
    private string _manualAddress = string.Empty;

    [ObservableProperty]
    private string _detectedAddress = string.Empty;

    [ObservableProperty]
    private string _addressSource = string.Empty;

    [ObservableProperty]
    private ObservableCollection<WebAdminCandidate> _webAdminCandidates = new();

    [ObservableProperty]
    private string _webAdminWarnings = string.Empty;

    [ObservableProperty]
    private string _webAdminMessage = string.Empty;

    [ObservableProperty]
    private bool _webAdminIsError;

    [ObservableProperty]
    private DiagnosticReport? _diagnosticReport;

    [ObservableProperty]
    private string _diagnosticSummary = string.Empty;

    public string ConditionText => Printer?.Condition switch
    {
        PrinterCondition.Normal => "Online",
        PrinterCondition.Offline => "Offline",
        PrinterCondition.Error => "Error",
        _ => "Unknown"
    };

    public string ModelText => string.IsNullOrWhiteSpace(Printer?.ModelName) ? "Unknown" : Printer!.ModelName;

    public string ManufacturerText => Printer?.Manufacturer ?? "Unknown";

    public string DriverText => string.IsNullOrWhiteSpace(Printer?.DriverName) ? "Not reported" : Printer!.DriverName;

    public string DriverVersionText => string.IsNullOrWhiteSpace(Printer?.DriverVersion) ? "Not reported" : Printer!.DriverVersion;

    public string PortText => string.IsNullOrWhiteSpace(Printer?.PortName) ? "Not reported" : Printer!.PortName;

    public string ConnectionText => Printer?.ConnectionDisplay ?? "Unknown";

    public string AddressText => string.IsNullOrWhiteSpace(Printer?.IpAddress) ? "Not detected" : Printer!.IpAddress;

    public string PortNumberText => Printer?.PortNumber is null ? "Not reported" : Printer.PortNumber.Value.ToString();

    public string DetectedText => Printer?.DetectedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;

    public string AddressSourceText => AddressSource switch
    {
        "port" => "Detected from the printer port",
        "port-name" => "Detected from the port name",
        "manual" => "Entered manually",
        _ => "Not available"
    };

    partial void OnAddressSourceChanged(string value) => OnPropertyChanged(nameof(AddressSourceText));

    partial void OnPrinterChanged(PrinterDevice? value)
    {
        HasPrinter = value is not null;

        OnPropertyChanged(nameof(ConditionText));
        OnPropertyChanged(nameof(ModelText));
        OnPropertyChanged(nameof(ManufacturerText));
        OnPropertyChanged(nameof(DriverText));
        OnPropertyChanged(nameof(DriverVersionText));
        OnPropertyChanged(nameof(PortText));
        OnPropertyChanged(nameof(ConnectionText));
        OnPropertyChanged(nameof(AddressText));
        OnPropertyChanged(nameof(PortNumberText));
        OnPropertyChanged(nameof(DetectedText));
    }

    public Task ActivateAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        var printer = parameter as PrinterDevice ?? _state.SelectedPrinter;

        if (printer is null)
        {
            Printer = null;
            WebAdminMessage = "No printer is selected.";
            WebAdminIsError = true;
            return Task.CompletedTask;
        }

        if (Printer?.Id != printer.Id)
        {
            Printer = printer;
            _state.SelectedPrinter = printer;
            ManualAddress = string.Empty;
            DiagnosticReport = null;
            DiagnosticSummary = string.Empty;
            RefreshWebAdmin();
        }

        return Task.CompletedTask;
    }

    private WebAdminResolution ResolveWebAdmin()
    {
        var manual = string.IsNullOrWhiteSpace(ManualAddress) ? null : ManualAddress;
        return _services.WebAdmin.Resolve(Printer!, manual);
    }

    private void RefreshWebAdmin()
    {
        if (Printer is null)
        {
            return;
        }

        var resolution = ResolveWebAdmin();

        DetectedAddress = resolution.DetectedAddress ?? string.Empty;
        AddressSource = resolution.AddressSource;
        WebAdminWarnings = string.Join(Environment.NewLine, resolution.Warnings);

        WebAdminCandidates = new ObservableCollection<WebAdminCandidate>(resolution.Candidates);

        WebAdminMessage = resolution.HasCandidates
            ? $"Validated {resolution.Candidates.Count} address(es)."
            : "No Web Admin address could be determined.";
        WebAdminIsError = !resolution.HasCandidates;
    }

    [RelayCommand]
    private void ResolveWebAdminAddress()
    {
        if (Printer is null)
        {
            WebAdminMessage = "Select a printer first.";
            WebAdminIsError = true;
            return;
        }

        RefreshWebAdmin();
    }

    [RelayCommand]
    private async Task OpenWebAdmin()
    {
        if (Printer is null)
        {
            WebAdminMessage = "Select a printer first.";
            WebAdminIsError = true;
            return;
        }

        var resolution = ResolveWebAdmin();
        WebAdminCandidates = new ObservableCollection<WebAdminCandidate>(resolution.Candidates);
        WebAdminWarnings = string.Join(Environment.NewLine, resolution.Warnings);

        if (!resolution.HasCandidates)
        {
            WebAdminMessage = "No Web Admin address could be validated. Enter the printer IP address or hostname manually.";
            WebAdminIsError = true;
            await RecordAsync("WebAdmin.Open", ActivityResult.Unavailable, "No address detected", string.Join("; ", resolution.Warnings)).ConfigureAwait(true);
            return;
        }

        await OpenCandidateAsync(resolution.Candidates[0]).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenCandidate(WebAdminCandidate? candidate)
    {
        if (candidate is null)
        {
            return;
        }

        await OpenCandidateAsync(candidate).ConfigureAwait(true);
    }

    private async Task OpenCandidateAsync(WebAdminCandidate candidate)
    {
        if (_state.Settings.ConfirmBeforeOpeningAdmin &&
            !_dialogs.Confirm(
                $"Open the printer administration page in the default browser?\n\n{candidate.Url}",
                "Open Web Admin"))
        {
            WebAdminMessage = "The request was cancelled.";
            WebAdminIsError = false;
            return;
        }

        var opened = await _services.Launcher.OpenAsync(candidate.Url).ConfigureAwait(true);

        WebAdminMessage = opened
            ? $"Opened {candidate.Url} in the default browser."
            : $"Windows could not open {candidate.Url}. Check the address and try again.";
        WebAdminIsError = !opened;

        await RecordAsync(
            "WebAdmin.Open",
            opened ? ActivityResult.Success : ActivityResult.Failure,
            candidate.Url).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RunDiagnostics()
    {
        if (Printer is null)
        {
            DiagnosticSummary = "Select a printer first.";
            return;
        }

        IsBusy = true;

        try
        {
            var report = await _services.Diagnostics.RunAsync(Printer).ConfigureAwait(true);
            DiagnosticReport = report;
            DiagnosticSummary = report.Summary;

            await RecordAsync(
                "Diagnostics.Run",
                report.HasFailures ? ActivityResult.Failure : ActivityResult.Success,
                report.Summary).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Diagnostics failed for {Printer}.", Printer.Name);
            DiagnosticSummary = "Diagnostics failed: " + exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenDiagnostics() => _ = _navigate(typeof(DiagnosticsViewModel), null);

    [RelayCommand]
    private void OpenMaintenance() => _ = _navigate(typeof(MaintenanceViewModel), null);

    [RelayCommand]
    private void OpenWasteInk() => _ = _navigate(typeof(WasteInkViewModel), null);

    [RelayCommand]
    private void BackToPrinters() => _ = _navigate(typeof(PrintersViewModel), null);

    private async Task RecordAsync(string action, ActivityResult result, string? details = null, string? error = null)
    {
        if (Printer is null)
        {
            return;
        }

        try
        {
            await _services.Activity.AddAsync(new ActivityRecord
            {
                PrinterName = Printer.Name,
                ModelName = Printer.ModelName,
                Action = action,
                Result = result,
                Details = details,
                Error = error
            }).ConfigureAwait(true);

            await _state.LoadActivityAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Could not record activity {Action}.", action);
        }
    }
}