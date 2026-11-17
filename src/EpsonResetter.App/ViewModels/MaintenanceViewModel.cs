using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsonResetter.App.Services;
using EpsonResetter.Core.Models;
using Serilog;

namespace EpsonResetter.App.ViewModels;

public sealed partial class MaintenanceViewModel : ViewModelBase, INavigationPage
{
    private readonly AppServices _services;
    private readonly AppState _state;
    private readonly IDialogService _dialogs;

    public MaintenanceViewModel(AppServices services, AppState state, IDialogService dialogs)
    {
        _services = services;
        _state = state;
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    public string Title => "Maintenance";

    [ObservableProperty]
    private PrinterDevice? _printer;

    [ObservableProperty]
    private string _message = "Choose an action. All actions are standard Windows print-system operations.";

    [ObservableProperty]
    private bool _hasPrinter;

    public string ModelText => string.IsNullOrWhiteSpace(Printer?.ModelName) ? string.Empty : Printer!.ModelName;

    partial void OnPrinterChanged(PrinterDevice? value)
    {
        HasPrinter = value is not null;
        OnPropertyChanged(nameof(ModelText));
    }

    public Task ActivateAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        var printer = parameter as PrinterDevice ?? _state.SelectedPrinter;
        Printer = printer;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task OpenQueue() => ExecuteAsync(
        () => _services.Maintenance.OpenQueueAsync(Printer!.Name),
        "print queue",
        "Maintenance.OpenQueue");

    [RelayCommand]
    private Task OpenProperties() => ExecuteAsync(
        () => _services.Maintenance.OpenPropertiesAsync(Printer!.Name),
        "printer properties",
        "Maintenance.OpenProperties");

    [RelayCommand]
    private Task PrintTestPage() => ExecuteAsync(
        async () =>
        {
            if (Printer is null || !_dialogs.Confirm(
                    "Print a Windows test page for this printer?",
                    "Print test page"))
            {
                return false;
            }

            return await _services.Maintenance.PrintTestPageAsync(Printer!.Name);
        },
        "Windows test page",
        "Maintenance.PrintTestPage");

    [RelayCommand]
    private async Task OpenWebAdmin()
    {
        if (Printer is null)
        {
            Message = "Select a printer first.";
            return;
        }

        var resolution = _services.WebAdmin.Resolve(Printer);

        if (!resolution.HasCandidates)
        {
            Message = "No Web Admin address could be detected. Enter the address on the Printer Details page.";
            await RecordAsync("Maintenance.WebAdmin", ActivityResult.Unavailable, string.Join("; ", resolution.Warnings)).ConfigureAwait(true);
            return;
        }

        var candidate = resolution.Candidates[0];
        var opened = await _services.Launcher.OpenAsync(candidate.Url).ConfigureAwait(true);

        Message = opened
            ? $"Opened {candidate.Url} in the default browser."
            : $"Windows could not open {candidate.Url}.";

        await RecordAsync(
            "Maintenance.WebAdmin",
            opened ? ActivityResult.Success : ActivityResult.Failure,
            candidate.Url).ConfigureAwait(true);
    }

    private async Task ExecuteAsync(Func<Task<bool>> action, string label, string actionName)
    {
        if (Printer is null)
        {
            Message = "Select a printer first.";
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            var launched = await action().ConfigureAwait(true);
            Message = launched
                ? $"Windows opened the {label}."
                : $"Windows could not open the {label}.";

            await RecordAsync(actionName, launched ? ActivityResult.Success : ActivityResult.Failure).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Maintenance action {Action} failed.", actionName);
            SetError($"The {label} could not be opened: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RecordAsync(string action, ActivityResult result, string? details = null)
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
                Details = details
            }).ConfigureAwait(true);

            await _state.LoadActivityAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Could not record activity {Action}.", action);
        }
    }
}