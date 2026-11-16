using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsonResetter.App.Services;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.Settings;
using EpsonResetter.Infrastructure.Persistence;
using Serilog;

namespace EpsonResetter.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase, INavigationPage
{
    private readonly AppServices _services;
    private readonly AppState _state;

    public SettingsViewModel(AppServices services, AppState state)
    {
        _services = services;
        _state = state;
        LoadFromSettings(services.Settings);
    }

    public string Title => "Settings";

    public Array SchemeOptions { get; } = Enum.GetValues<WebAdminSchemePreference>();

    [ObservableProperty]
    private WebAdminSchemePreference _preferredScheme;

    [ObservableProperty]
    private int _httpPort;

    [ObservableProperty]
    private int _httpsPort;

    [ObservableProperty]
    private int _networkTimeout;

    [ObservableProperty]
    private bool _showOnlyEpson;

    [ObservableProperty]
    private bool _confirmBeforeOpening;

    [ObservableProperty]
    private ObservableCollection<string> _validationErrors = new();

    [ObservableProperty]
    private string _message = string.Empty;

    public string DatabasePathText => DatabasePaths.DatabasePath;

    public string LogPathText => DatabasePaths.LogDirectory;

    public string RegistryText => $"{_services.Registry.Profiles.Count} model profiles are registered. None currently has a verified reset method.";

    public Task ActivateAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        LoadFromSettings(_services.Settings);
        return Task.CompletedTask;
    }

    private void LoadFromSettings(AppSettings settings)
    {
        PreferredScheme = settings.PreferredScheme;
        HttpPort = settings.HttpPort;
        HttpsPort = settings.HttpsPort;
        NetworkTimeout = settings.NetworkTimeoutMs;
        ShowOnlyEpson = settings.ShowOnlyEpsonByDefault;
        ConfirmBeforeOpening = settings.ConfirmBeforeOpeningAdmin;
        ValidationErrors = new ObservableCollection<string>();
        Message = string.Empty;
    }

    [RelayCommand]
    private async Task Save()
    {
        var candidate = new AppSettings
        {
            PreferredScheme = PreferredScheme,
            HttpPort = HttpPort,
            HttpsPort = HttpsPort,
            NetworkTimeoutMs = NetworkTimeout,
            ShowOnlyEpsonByDefault = ShowOnlyEpson,
            ConfirmBeforeOpeningAdmin = ConfirmBeforeOpening
        };

        var errors = candidate.Validate();
        ValidationErrors = new ObservableCollection<string>(errors);

        if (errors.Count > 0)
        {
            Message = "Settings were not saved.";
            return;
        }

        ApplyTo(_services.Settings, candidate);
        IsBusy = true;

        try
        {
            await _services.SettingsStore.SaveAsync(_services.Settings).ConfigureAwait(true);

            Message = "Settings saved.";
            _state.StatusMessage = "Settings saved.";

            try
            {
                await _services.Activity.AddAsync(new ActivityRecord
                {
                    PrinterName = "Application",
                    Action = "Settings.Save",
                    Result = ActivityResult.Success,
                    Details = $"Web Admin scheme: {PreferredScheme}; HTTP port: {HttpPort}; HTTPS port: {HttpsPort}"
                }).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "Could not record the settings change.");
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Settings could not be saved.");
            SetError("Settings could not be saved: " + exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        LoadFromSettings(new AppSettings());
        Message = "Defaults restored in the form. Save to apply them.";
    }

    private static void ApplyTo(AppSettings target, AppSettings source)
    {
        target.PreferredScheme = source.PreferredScheme;
        target.HttpPort = source.HttpPort;
        target.HttpsPort = source.HttpsPort;
        target.NetworkTimeoutMs = source.NetworkTimeoutMs;
        target.ShowOnlyEpsonByDefault = source.ShowOnlyEpsonByDefault;
        target.ConfirmBeforeOpeningAdmin = source.ConfirmBeforeOpeningAdmin;
    }
}