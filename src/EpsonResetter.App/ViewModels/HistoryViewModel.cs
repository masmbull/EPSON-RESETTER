using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsonResetter.App.Services;
using EpsonResetter.Core.Models;
using Serilog;

namespace EpsonResetter.App.ViewModels;

public sealed partial class HistoryViewModel : ViewModelBase, INavigationPage
{
    private readonly IDialogService _dialogs;
    private readonly AppServices _services;
    private readonly AppState _state;

    public HistoryViewModel(AppServices services, AppState state, IDialogService dialogs)
    {
        _services = services;
        _state = state;
        _dialogs = dialogs;
    }

    public string Title => "Activity History";

    public ObservableCollection<ActivityRecord> AllRecords { get; } = new();

    [ObservableProperty]
    private ObservableCollection<ActivityRecord> _filteredRecords = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _hasRecords;

    [ObservableProperty]
    private ActivityRecord? _selectedRecord;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    public Task ActivateAsync(object? parameter = null, CancellationToken cancellationToken = default) => LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;

        try
        {
            var records = await _services.Activity.ListAsync(500).ConfigureAwait(true);

            AllRecords.Clear();
            foreach (var record in records)
            {
                AllRecords.Add(record);
            }

            ApplyFilter();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Activity history could not be loaded.");
            SetError("Activity history could not be loaded: " + exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Clear()
    {
        if (AllRecords.Count == 0)
        {
            Summary = "There is no activity to clear.";
            return;
        }

        if (!_dialogs.Confirm(
                "Delete all recorded activity? This cannot be undone.",
                "Clear activity history"))
        {
            return;
        }

        IsBusy = true;

        try
        {
            await _services.Activity.ClearAsync().ConfigureAwait(true);
            await _state.LoadActivityAsync().ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
            Summary = "Activity history cleared.";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Activity history could not be cleared.");
            SetError("Activity history could not be cleared: " + exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<ActivityRecord> query = AllRecords;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(record =>
                record.PrinterName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                record.Action.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                record.ModelName?.Contains(term, StringComparison.OrdinalIgnoreCase) == true ||
                record.Details?.Contains(term, StringComparison.OrdinalIgnoreCase) == true ||
                record.Error?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
        }

        var filtered = query.ToList();

        FilteredRecords = new ObservableCollection<ActivityRecord>(filtered);
        Summary = $"{filtered.Count} of {AllRecords.Count} entr(ies)";
        HasRecords = filtered.Count > 0;
    }
}