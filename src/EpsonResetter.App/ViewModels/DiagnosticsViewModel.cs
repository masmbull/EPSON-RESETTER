using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsonResetter.App.Services;
using EpsonResetter.Core.Models;
using Serilog;

namespace EpsonResetter.App.ViewModels;

public sealed partial class DiagnosticsViewModel : ViewModelBase, INavigationPage
{
    private readonly AppServices _services;
    private readonly AppState _state;

    public DiagnosticsViewModel(AppServices services, AppState state)
    {
        _services = services;
        _state = state;
    }

    public string Title => "Diagnostics";

    [ObservableProperty]
    private PrinterDevice? _printer;

    [ObservableProperty]
    private DiagnosticReport? _report;

    [ObservableProperty]
    private string _summary = "Run a diagnostic pass to check the print path.";

    [ObservableProperty]
    private bool _hasReport;

    public bool HasPrinter => Printer is not null;

    public string ModelText => string.IsNullOrWhiteSpace(Printer?.ModelName) ? string.Empty : Printer!.ModelName;

    partial void OnPrinterChanged(PrinterDevice? value)
    {
        OnPropertyChanged(nameof(HasPrinter));
        OnPropertyChanged(nameof(ModelText));
    }

    public Task ActivateAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        var printer = parameter as PrinterDevice ?? _state.SelectedPrinter;

        if (Printer?.Id != printer?.Id)
        {
            Printer = printer;
            Report = null;
            HasReport = false;
            Summary = printer is null
                ? "Select a printer to run diagnostics."
                : "Run a diagnostic pass to check the print path.";
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task Run()
    {
        if (Printer is null)
        {
            Summary = "Select a printer first.";
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            var report = await _services.Diagnostics.RunAsync(Printer).ConfigureAwait(true);
            Report = report;
            HasReport = true;
            Summary = report.Summary;

            await _services.Activity.AddAsync(new ActivityRecord
            {
                PrinterName = Printer.Name,
                ModelName = Printer.ModelName,
                Action = "Diagnostics.Run",
                Result = report.HasFailures ? ActivityResult.Failure : ActivityResult.Success,
                Details = report.Summary
            }).ConfigureAwait(true);

            await _state.LoadActivityAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Diagnostics failed for {Printer}.", Printer?.Name);
            SetError("Diagnostics failed: " + exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}