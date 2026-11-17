using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsonResetter.App.Services;
using EpsonResetter.Core.Models;

namespace EpsonResetter.App.ViewModels;

public sealed partial class DashboardViewModel : ViewModelBase, INavigationPage
{
    private readonly AppServices _services;
    private readonly AppState _state;
    private readonly NavigateHandler _navigate;

    public DashboardViewModel(AppServices services, AppState state, NavigateHandler navigate)
    {
        _services = services;
        _state = state;
        _navigate = navigate;

        state.PropertyChanged += OnStatePropertyChanged;
        state.RecentActivity.CollectionChanged += OnRecentActivityChanged;

        LoadRecent();
    }

    public string Title => "Dashboard";

    public ObservableCollection<ActivityRecord> RecentActivity { get; } = new();

    public bool HasActivity { get; private set; }

    public int TotalPrinters => _state.Printers.Count;

    public int EpsonPrinters => _state.Printers.Count(printer => printer.IsEpson);

    public int NetworkPrinters => _state.Printers.Count(printer => printer.IsNetwork);

    public int OfflinePrinters => _state.Printers.Count(printer =>
        printer.Condition is PrinterCondition.Offline or PrinterCondition.Error);

    public bool HasPrinters => _state.Printers.Count > 0;

    public string LastScanText => _state.LastScan is null
        ? "Not scanned yet"
        : $"Last scan {_state.LastScan:yyyy-MM-dd HH:mm:ss}";

    public string SpoolerText => _services.SystemStatus.IsSpoolerRunning()
        ? "Windows Print Spooler is running"
        : "Windows Print Spooler is not running";

    public string CompatibilityText => $"{_services.Registry.Profiles.Count} models in the compatibility registry";

    public string ResetSupportText => _services.Registry.Profiles.All(profile => !profile.SupportsReset)
        ? "No model currently has a verified, vendor-authorised reset method."
        : "One or more models have a verified reset method.";

    public Task ActivateAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        LoadRecent();
        OnPropertyChanged(nameof(SpoolerText));
        return Task.CompletedTask;
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(AppState.Printers) or nameof(AppState.LastScan)))
        {
            return;
        }

        OnPropertyChanged(nameof(TotalPrinters));
        OnPropertyChanged(nameof(EpsonPrinters));
        OnPropertyChanged(nameof(NetworkPrinters));
        OnPropertyChanged(nameof(OfflinePrinters));
        OnPropertyChanged(nameof(HasPrinters));
        OnPropertyChanged(nameof(LastScanText));
    }

    private void OnRecentActivityChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
        {
            foreach (ActivityRecord record in e.NewItems)
            {
                RecentActivity.Insert(0, record);
            }

            while (RecentActivity.Count > 6)
            {
                RecentActivity.RemoveAt(RecentActivity.Count - 1);
            }

            HasActivity = RecentActivity.Count > 0;
            OnPropertyChanged(nameof(HasActivity));
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            LoadRecent();
        }
    }

    private void LoadRecent()
    {
        RecentActivity.Clear();

        foreach (var record in _state.RecentActivity.Take(6))
        {
            RecentActivity.Add(record);
        }

        HasActivity = RecentActivity.Count > 0;
        OnPropertyChanged(nameof(HasActivity));
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await _state.ScanAsync().ConfigureAwait(true);
        await _state.LoadActivityAsync().ConfigureAwait(true);
        LoadRecent();
    }

    [RelayCommand]
    private void OpenPrinters() => _ = _navigate(typeof(PrintersViewModel), null);

    [RelayCommand]
    private void OpenHistory() => _ = _navigate(typeof(HistoryViewModel), null);

    [RelayCommand]
    private void OpenSettings() => _ = _navigate(typeof(SettingsViewModel), null);
}
