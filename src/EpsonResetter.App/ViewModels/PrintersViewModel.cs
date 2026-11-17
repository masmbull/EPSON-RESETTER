using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsonResetter.App.Services;
using EpsonResetter.Core.Models;

namespace EpsonResetter.App.ViewModels;

public sealed partial class PrintersViewModel : ViewModelBase, INavigationPage
{
    private readonly Action<PrinterDevice> _showPrinter;
    private readonly AppState _state;

    public PrintersViewModel(AppServices services, AppState state, Action<PrinterDevice> showPrinter)
    {
        _services = services;
        _state = state;
        _showPrinter = showPrinter;

        OnlyEpson = state.Settings.ShowOnlyEpsonByDefault;

        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.Printers))
            {
                ApplyFilter();
            }
        };
    }

    private readonly AppServices _services;

    public string Title => "Printers";

    public ObservableCollection<PrinterDevice> FilteredPrinters { get; } = new();

    public Array ConnectionOptions { get; } = new[] { "All", "USB", "Network", "Local", "Unknown" };

    [ObservableProperty]
    private PrinterDevice? _selectedPrinter;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _connectionFilter = "All";

    [ObservableProperty]
    private bool _onlyEpson;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    private string _emptyMessage = string.Empty;

    [ObservableProperty]
    private bool _hasPrinters;

    [ObservableProperty]
    private bool _hasMatches;

    partial void OnSelectedPrinterChanged(PrinterDevice? value)
    {
        if (value is not null)
        {
            _state.SelectedPrinter = value;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnConnectionFilterChanged(string value) => ApplyFilter();

    partial void OnOnlyEpsonChanged(bool value) => ApplyFilter();

    public Task ActivateAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        ApplyFilter();
        return Task.CompletedTask;
    }

    private void ApplyFilter()
    {
        var selectedId = SelectedPrinter?.Id;
        IEnumerable<PrinterDevice> query = _state.Printers;

        if (OnlyEpson)
        {
            query = query.Where(printer => printer.IsEpson);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(printer =>
                printer.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                printer.ModelName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                printer.DriverName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                printer.IpAddress?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (ConnectionFilter != "All")
        {
            query = query.Where(printer =>
                string.Equals(printer.ConnectionType.ToString(), ConnectionFilter, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query.ToList();

        FilteredPrinters.Clear();
        foreach (var printer in filtered)
        {
            FilteredPrinters.Add(printer);
        }

        HasPrinters = _state.Printers.Count > 0;
        HasMatches = FilteredPrinters.Count > 0;

        ResultSummary = $"{FilteredPrinters.Count} of {_state.Printers.Count} printer(s) shown";
        EmptyMessage = HasPrinters
            ? "No printer matches the current search and filters."
            : "No printers were found in the Windows print subsystem. Connect a printer and use Rescan.";

        if (selectedId is not null && SelectedPrinter?.Id != selectedId)
        {
            SelectedPrinter = FilteredPrinters.FirstOrDefault(printer => printer.Id == selectedId);
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await _state.ScanAsync().ConfigureAwait(true);
        ApplyFilter();
    }

    [RelayCommand]
    private void OpenDetails(PrinterDevice? printer)
    {
        if (printer is not null)
        {
            _showPrinter(printer);
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        ConnectionFilter = "All";
        OnlyEpson = false;
    }
}