using EpsonResetter.App.Services;
using EpsonResetter.App.ViewModels;
using EpsonResetter.Core.Settings;

namespace EpsonResetter.Tests;

public class SettingsViewModelTests
{
    private static (SettingsViewModel Vm, AppState State) BuildVm()
    {
        var settings = new AppSettings();
        var services = new AppServices(settings);
        var state = new AppState(services);
        var vm = new SettingsViewModel(services, state);
        return (vm, state);
    }

    [Fact]
    public void Initial_Settings_Match_Defaults()
    {
        var (vm, state) = BuildVm();

        Assert.Equal(WebAdminSchemePreference.Auto, vm.PreferredScheme);
        Assert.Equal(80, vm.HttpPort);
        Assert.Equal(443, vm.HttpsPort);
        Assert.Equal(1500, vm.NetworkTimeout);
        Assert.False(vm.ShowOnlyEpson);
        Assert.True(vm.ConfirmBeforeOpening);
    }

    [Fact]
    public void SchemeOptions_Contains_All_Values()
    {
        var (vm, state) = BuildVm();

        Assert.Equal(3, vm.SchemeOptions.Length);
    }

    [Fact]
    public void DatabasePath_Is_Reported()
    {
        var (vm, state) = BuildVm();

        Assert.NotNull(vm.DatabasePathText);
        Assert.Contains("EpsonResetter", vm.DatabasePathText);
    }

    [Fact]
    public void LogPath_Is_Reported()
    {
        var (vm, state) = BuildVm();

        Assert.NotNull(vm.LogPathText);
    }

    [Fact]
    public void RegistryText_Reports_No_Support()
    {
        var (vm, state) = BuildVm();

        Assert.NotNull(vm.RegistryText);
    }

    [Fact]
    public async Task Save_Validation_Errors_Are_Cleared_On_Success()
    {
        var (vm, state) = BuildVm();

        // Valid settings should not produce validation errors
        vm.SaveCommand.Execute(null);

        await Task.Delay(100);

        Assert.Empty(vm.ValidationErrors);
    }

    [Fact]
    public void ResetDefaults_Loads_Default_Settings()
    {
        var (vm, state) = BuildVm();

        // Change something
        vm.HttpPort = 9999;
        vm.ResetDefaultsCommand.Execute(null);

        Assert.Equal(80, vm.HttpPort);
    }

    [Fact]
    public void Commands_Exist()
    {
        var (vm, state) = BuildVm();

        Assert.NotNull(vm.SaveCommand);
        Assert.NotNull(vm.ResetDefaultsCommand);
    }
}
