using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsonResetter.App.Services;
using EpsonResetter.Core.Compatibility;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.WasteInk;
using EpsonResetter.Core.WebAdmin;
using Serilog;

namespace EpsonResetter.App.ViewModels;

public sealed partial class WasteInkViewModel : ViewModelBase, INavigationPage
{
    private readonly AppServices _services;
    private readonly AppState _state;

    public WasteInkViewModel(AppServices services, AppState state)
    {
        _services = services;
        _state = state;
    }

    public string Title => "Waste Ink Counter";

    [ObservableProperty]
    private PrinterDevice? _printer;

    [ObservableProperty]
    private bool _hasPrinter;

    [ObservableProperty]
    private string _modelText = string.Empty;

    [ObservableProperty]
    private bool _isKnownModel;

    public string KnownModelText => IsKnownModel ? "Yes" : "No";

    partial void OnIsKnownModelChanged(bool value) => OnPropertyChanged(nameof(KnownModelText));

    [ObservableProperty]
    private string _supportLevel = "Unknown";

    [ObservableProperty]
    private string _supportMessage = "Select a printer to check model compatibility.";

    [ObservableProperty]
    private string _counterText = "Not read";

    [ObservableProperty]
    private string _counterMessage = "Read the counter only through a verified, permitted method.";

    [ObservableProperty]
    private bool _padInspected;

    [ObservableProperty]
    private bool _ownerAuthorised;

    [ObservableProperty]
    private bool _methodConfirmed;

    [ObservableProperty]
    private bool _hasEvaluated;

    [ObservableProperty]
    private bool _isEligible;

    [ObservableProperty]
    private string _eligibilityCode = string.Empty;

    [ObservableProperty]
    private string _eligibilitySummary = "Not evaluated yet.";

    [ObservableProperty]
    private ObservableCollection<string> _eligibilityReasons = new();

    [ObservableProperty]
    private ModelProfile? _profile;

    partial void OnPrinterChanged(PrinterDevice? value)
    {
        HasPrinter = value is not null;
        LoadProfile();
    }

    public Task ActivateAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        var printer = parameter as PrinterDevice ?? _state.SelectedPrinter;

        if (Printer?.Id != printer?.Id)
        {
            Printer = printer;
        }

        return Task.CompletedTask;
    }

    private void LoadProfile()
    {
        if (Printer is null)
        {
            ModelText = string.Empty;
            IsKnownModel = false;
            SupportLevel = "Unknown";
            SupportMessage = "Select a printer to check model compatibility.";
            CounterText = "Not read";
            CounterMessage = "Read the counter only through a verified, permitted method.";
            HasEvaluated = false;
            EligibilityReasons = new ObservableCollection<string>();
            EligibilitySummary = "Not evaluated yet.";
            Profile = null;
            return;
        }

        var profile = _services.Registry.Find(Printer.ModelName);
        Profile = profile;
        ModelText = Printer.ModelName;
        IsKnownModel = profile is not null;
        SupportLevel = profile is null
            ? "Unknown"
            : profile.SupportsReset
                ? "Supported"
                : "Unsupported";
        SupportMessage = profile?.SupportSummary
                         ?? $"The model '{Printer.ModelName}' is not in the compatibility registry.";

        PadInspected = false;
        OwnerAuthorised = false;
        MethodConfirmed = false;
        HasEvaluated = false;
        EligibilityReasons = new ObservableCollection<string>();
        EligibilitySummary = "Not evaluated yet.";
        CounterText = "Not read";
        CounterMessage = "Read the counter only through a verified, permitted method.";
    }

    [RelayCommand]
    private async Task ReadCounter()
    {
        if (Printer is null)
        {
            CounterMessage = "Select a printer first.";
            return;
        }

        IsBusy = true;

        try
        {
            var reading = _services.CounterReader.Read(Printer, Profile);

            CounterText = reading.IsSupported && reading.CounterValue is not null
                ? reading.CounterValue.Value.ToString()
                : "Unavailable";
            CounterMessage = reading.Message;

            await RecordAsync(
                "WasteInk.Read",
                reading.IsSupported ? ActivityResult.Success : ActivityResult.Unavailable,
                reading.Message,
                reading.IsSupported ? null : reading.Message).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Waste ink counter read failed for {Printer}.", Printer.Name);
            CounterText = "Unavailable";
            CounterMessage = "The counter could not be read: " + exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EvaluateEligibility()
    {
        if (Printer is null)
        {
            EligibilitySummary = "Select a printer first.";
            return;
        }

        IsBusy = true;

        try
        {
            var online = await IsOnlineAsync(Printer).ConfigureAwait(true);

            var result = _services.Eligibility.Evaluate(new ResetEligibilityRequest
            {
                ModelName = Printer.ModelName,
                PrinterOnline = online,
                PadInspectedAndServiced = PadInspected,
                OwnerAuthorised = OwnerAuthorised,
                MethodConfirmedByOperator = MethodConfirmed
            });

            HasEvaluated = true;
            IsEligible = result.IsEligible;
            EligibilityCode = result.Code;
            EligibilitySummary = result.Summary;
            EligibilityReasons = new ObservableCollection<string>(result.Reasons);

            await RecordAsync(
                "Reset.EligibilityCheck",
                result.IsEligible ? ActivityResult.Success : ActivityResult.Unavailable,
                $"{result.Code}: {result.Summary}",
                result.IsEligible ? null : string.Join("; ", result.Reasons)).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Reset eligibility evaluation failed for {Printer}.", Printer.Name);
            EligibilitySummary = "The eligibility check failed: " + exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> IsOnlineAsync(PrinterDevice printer)
    {
        var address = WebAdminAddressResolver.ExtractAddress(printer.IpAddress)
                      ?? WebAdminAddressResolver.ExtractAddress(printer.PortName);

        var spoolerOnline = !printer.WorkOffline && printer.Condition is not (PrinterCondition.Offline or PrinterCondition.Unknown);

        if (address is null)
        {
            return spoolerOnline;
        }

        var ping = await _services.Probe.PingAsync(address, _state.Settings.NetworkTimeoutMs).ConfigureAwait(true);
        return ping.IsReachable && spoolerOnline;
    }

    private async Task RecordAsync(string action, ActivityResult result, string details, string? error = null)
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