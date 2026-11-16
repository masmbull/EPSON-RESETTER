using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsonResetter.App.Services;
using EpsonResetter.Core.Models;
using Serilog;

namespace EpsonResetter.App.ViewModels;

public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly Dictionary<Type, INavigationPage> _pages;
    private readonly DispatcherTimer _statusTimer;

    public ShellViewModel(AppServices services, IDialogService dialogs)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        State = new AppState(services);

        var dashboard = new DashboardViewModel(services, State, NavigateToAsync);
        var printers = new PrintersViewModel(services, State, printer => _ = ShowPrinterAsync(printer));
        var details = new PrinterDetailsViewModel(services, State, dialogs, NavigateToAsync);
        var diagnostics = new DiagnosticsViewModel(services, State);
        var maintenance = new MaintenanceViewModel(services, State);
        var wasteInk = new WasteInkViewModel(services, State);
        var history = new HistoryViewModel(services, State, dialogs);
        var settings = new SettingsViewModel(services, State);

        _pages = new Dictionary<Type, INavigationPage>
        {
            [typeof(DashboardViewModel)] = dashboard,
            [typeof(PrintersViewModel)] = printers,
            [typeof(PrinterDetailsViewModel)] = details,
            [typeof(DiagnosticsViewModel)] = diagnostics,
            [typeof(MaintenanceViewModel)] = maintenance,
            [typeof(WasteInkViewModel)] = wasteInk,
            [typeof(HistoryViewModel)] = history,
            [typeof(SettingsViewModel)] = settings
        };

        Navigation = new ObservableCollection<NavigationItem>
        {
            new("Dashboard", typeof(DashboardViewModel)),
            new("Printers", typeof(PrintersViewModel)),
            new("Diagnostics", typeof(DiagnosticsViewModel)),
            new("Maintenance", typeof(MaintenanceViewModel)),
            new("Waste Ink Counter", typeof(WasteInkViewModel)),
            new("Activity History", typeof(HistoryViewModel)),
            new("Settings", typeof(SettingsViewModel))
        };

        CurrentPage = dashboard;
        PageTitle = dashboard.Title;
        SelectNavigation(typeof(DashboardViewModel));

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
        _statusTimer.Tick += OnStatusTimerTick;
        _statusTimer.Start();
    }

    private async void OnStatusTimerTick(object? sender, EventArgs e)
    {
        if (State.IsScanning)
        {
            return;
        }

        await State.ScanAsync().ConfigureAwait(true);
    }

    private readonly AppServices _services;

    private readonly IDialogService _dialogs;

    public AppState State { get; }

    public ObservableCollection<NavigationItem> Navigation { get; }

    [ObservableProperty]
    private INavigationPage? _currentPage;

    [ObservableProperty]
    private NavigationItem? _selectedNavigation;

    [ObservableProperty]
    private string _pageTitle = "Dashboard";

    public async Task InitializeAsync()
    {
        await State.ScanAsync().ConfigureAwait(true);
        await State.LoadActivityAsync().ConfigureAwait(true);
        await NavigateToAsync(typeof(DashboardViewModel)).ConfigureAwait(true);
    }

    public async Task ShowPrinterAsync(PrinterDevice printer)
    {
        ArgumentNullException.ThrowIfNull(printer);
        State.SelectedPrinter = printer;
        await NavigateToAsync(typeof(PrinterDetailsViewModel), printer).ConfigureAwait(true);
    }

    public async Task NavigateToAsync(Type pageType, object? parameter = null)
    {
        if (!_pages.TryGetValue(pageType, out var page))
        {
            Log.Warning("Unknown page type requested: {PageType}", pageType);
            return;
        }

        try
        {
            await page.ActivateAsync(parameter).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Activation of {Page} failed.", pageType.Name);
            State.StatusMessage = $"Could not open {page.Title}: {exception.Message}";
        }

        CurrentPage = page;
        PageTitle = page.Title;
        SelectNavigation(pageType);
    }

    private void SelectNavigation(Type pageType)
    {
        var navType = pageType == typeof(PrinterDetailsViewModel) ? typeof(PrintersViewModel) : pageType;
        var target = Navigation.FirstOrDefault(item => item.ViewModelType == navType);

        foreach (var item in Navigation)
        {
            item.IsSelected = false;
        }

        if (target is not null)
        {
            target.IsSelected = true;
            SelectedNavigation = target;
        }
    }

    [RelayCommand]
    private async Task Navigate(NavigationItem? item)
    {
        if (item is null)
        {
            return;
        }

        await NavigateToAsync(item.ViewModelType).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task Rescan()
    {
        await State.ScanAsync().ConfigureAwait(true);
        await State.LoadActivityAsync().ConfigureAwait(true);
    }
}