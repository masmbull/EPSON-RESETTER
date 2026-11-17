using EpsonResetter.App.Services;
using EpsonResetter.App.ViewModels;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.Settings;
using System.Collections.ObjectModel;

namespace EpsonResetter.Tests;

public class PrintersViewModelTests
{
    private static (PrintersViewModel Vm, AppState State) BuildVm(Action<PrinterDevice>? showPrinter = null)
    {
        var settings = new AppSettings();
        var services = new AppServices(settings);
        var state = new AppState(services);
        var vm = new PrintersViewModel(services, state, showPrinter ?? (_ => { }));
        return (vm, state);
    }

    [Fact]
    public void Initial_State_Shows_All_Printers()
    {
        var (vm, state) = BuildVm();

        state.Printers = new ObservableCollection<PrinterDevice>
        {
            TestPrinters.NetworkPrinter(),
            TestPrinters.UsbPrinter()
        };

        Assert.Equal(2, vm.FilteredPrinters.Count);
        Assert.True(vm.HasMatches);
        Assert.Equal("2 of 2 printer(s) shown", vm.ResultSummary);
    }

    [Fact]
    public void Search_Filters_By_Name_Model_And_Driver()
    {
        var (vm, state) = BuildVm();

        state.Printers = new ObservableCollection<PrinterDevice>
        {
            TestPrinters.NetworkPrinter("EPSON ET-2850", "192.168.1.50"),
            TestPrinters.UsbPrinter("EPSON L3250")
        };

        vm.SearchText = "ET-2850";

        Assert.Single(vm.FilteredPrinters);
        Assert.Equal("EPSON ET-2850", vm.FilteredPrinters[0].Name);
        Assert.Equal("1 of 2 printer(s) shown", vm.ResultSummary);
    }

    [Fact]
    public void Search_Is_Case_Insensitive()
    {
        var (vm, state) = BuildVm();

        state.Printers = new ObservableCollection<PrinterDevice>
        {
            TestPrinters.NetworkPrinter("EPSON ET-2850")
        };

        vm.SearchText = "et-2850";

        Assert.Single(vm.FilteredPrinters);
    }

    [Fact]
    public void Connection_Filter_Works()
    {
        var (vm, state) = BuildVm();

        state.Printers = new ObservableCollection<PrinterDevice>
        {
            TestPrinters.NetworkPrinter(),
            TestPrinters.UsbPrinter()
        };

        vm.ConnectionFilter = "USB";

        Assert.Single(vm.FilteredPrinters);
        Assert.Equal(PrinterConnectionType.Usb, vm.FilteredPrinters[0].ConnectionType);
    }

    [Fact]
    public void Epson_Only_Filter_Works()
    {
        var (vm, state) = BuildVm();

        state.Printers = new ObservableCollection<PrinterDevice>
        {
            TestPrinters.NetworkPrinter(),
            TestPrinters.UsbPrinter(),
            new PrinterDevice
            {
                Id = "hp",
                Name = "HP LaserJet",
                DriverName = "HP LaserJet",
                PortName = "USB002",
                ConnectionType = PrinterConnectionType.Usb,
                Manufacturer = "HP",
                DetectedAtUtc = DateTime.UtcNow
            }
        };

        vm.OnlyEpson = true;

        Assert.Equal(2, vm.FilteredPrinters.Count);
        Assert.All(vm.FilteredPrinters, p => Assert.True(p.IsEpson));
    }

    [Fact]
    public void ClearFilters_Resets_All_Filters()
    {
        var (vm, state) = BuildVm();

        state.Printers = new ObservableCollection<PrinterDevice>
        {
            TestPrinters.NetworkPrinter()
        };

        vm.SearchText = "test";
        vm.ConnectionFilter = "USB";
        vm.OnlyEpson = true;

        vm.ClearFiltersCommand.Execute(null);

        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal("All", vm.ConnectionFilter);
        Assert.False(vm.OnlyEpson);
    }

    [Fact]
    public void Empty_State_When_No_Printers()
    {
        var (vm, state) = BuildVm();

        state.Printers = new ObservableCollection<PrinterDevice>();

        Assert.False(vm.HasPrinters);
        Assert.False(vm.HasMatches);
        Assert.Contains("No printers were found", vm.EmptyMessage);
    }

    [Fact]
    public void Empty_State_When_No_Matches()
    {
        var (vm, state) = BuildVm();

        state.Printers = new ObservableCollection<PrinterDevice>
        {
            TestPrinters.NetworkPrinter()
        };
        vm.SearchText = "nonexistent";

        Assert.True(vm.HasPrinters);
        Assert.False(vm.HasMatches);
        Assert.Contains("No printer matches", vm.EmptyMessage);
    }

    [Fact]
    public void SelectedPrinter_Updates_State()
    {
        var (vm, state) = BuildVm();
        var printer = TestPrinters.NetworkPrinter();
        state.Printers = new ObservableCollection<PrinterDevice> { printer };

        vm.SelectedPrinter = printer;

        Assert.Same(printer, state.SelectedPrinter);
    }

    [Fact]
    public void OnlyEpson_Defaults_From_Settings()
    {
        var settings = new AppSettings { ShowOnlyEpsonByDefault = true };
        var services = new AppServices(settings);
        var state = new AppState(services);
        var vm = new PrintersViewModel(services, state, _ => { });

        Assert.True(vm.OnlyEpson);
    }
}
