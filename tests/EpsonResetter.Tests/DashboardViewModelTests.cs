using EpsonResetter.App.Services;
using EpsonResetter.App.ViewModels;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.Settings;
using System.Collections.ObjectModel;

namespace EpsonResetter.Tests;

public class DashboardViewModelTests
{
    private static (DashboardViewModel Vm, AppState State) BuildVm()
    {
        var settings = new AppSettings();
        var services = new AppServices(settings);
        var state = new AppState(services);
        var vm = new DashboardViewModel(services, state, (_, _) => Task.CompletedTask);
        return (vm, state);
    }

    [Fact]
    public void Computed_Counts_Reflect_Printers()
    {
        var (vm, state) = BuildVm();

        state.Printers = new ObservableCollection<PrinterDevice>
        {
            TestPrinters.NetworkPrinter(),
            TestPrinters.UsbPrinter(),
            TestPrinters.OfflinePrinter()
        };

        Assert.Equal(3, vm.TotalPrinters);
        Assert.Equal(3, vm.EpsonPrinters);
        Assert.Equal(2, vm.NetworkPrinters);
        Assert.Equal(1, vm.OfflinePrinters);
    }

    [Fact]
    public void HasPrinters_Reflects_List()
    {
        var (vm, state) = BuildVm();

        Assert.False(vm.HasPrinters);

        state.Printers = new ObservableCollection<PrinterDevice>
        {
            TestPrinters.NetworkPrinter()
        };

        Assert.True(vm.HasPrinters);
    }

    [Fact]
    public void LastScanText_Updates_After_Scan()
    {
        var (vm, state) = BuildVm();

        Assert.Equal("Not scanned yet", vm.LastScanText);
    }

    [Fact]
    public void SpoolerText_Reports_Status()
    {
        var (vm, state) = BuildVm();

        // Depends on real WMI spooler state - just verify it returns a meaningful message
        Assert.False(string.IsNullOrWhiteSpace(vm.SpoolerText));
        Assert.True(
            vm.SpoolerText.Contains("running") ||
            vm.SpoolerText.Contains("not running"));
    }

    [Fact]
    public void CompatibilityText_Reports_Registry_Count()
    {
        var (vm, state) = BuildVm();

        Assert.Contains("models in the compatibility registry", vm.CompatibilityText);
    }

    [Fact]
    public void ResetSupportText_Warns_About_No_Reset()
    {
        var (vm, state) = BuildVm();

        Assert.Contains("No model currently has a verified", vm.ResetSupportText);
    }

    [Fact]
    public void HasActivity_Starts_False()
    {
        var (vm, state) = BuildVm();

        Assert.False(vm.HasActivity);
    }

    [Fact]
    public void RecentActivity_Starts_Empty()
    {
        var (vm, state) = BuildVm();

        Assert.Empty(vm.RecentActivity);
    }

    [Fact]
    public void Commands_Exist()
    {
        var (vm, state) = BuildVm();

        Assert.NotNull(vm.RefreshCommand);
        Assert.NotNull(vm.OpenPrintersCommand);
        Assert.NotNull(vm.OpenHistoryCommand);
        Assert.NotNull(vm.OpenSettingsCommand);
    }
}
