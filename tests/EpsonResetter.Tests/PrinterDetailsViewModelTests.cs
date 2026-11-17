using EpsonResetter.App.Services;
using EpsonResetter.App.ViewModels;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.Settings;
using EpsonResetter.Core.WebAdmin;

namespace EpsonResetter.Tests;

public class PrinterDetailsViewModelTests
{
    private static (PrinterDetailsViewModel Vm, AppState State) BuildVm()
    {
        var settings = new AppSettings();
        var services = new AppServices(settings);
        var state = new AppState(services);
        var vm = new PrinterDetailsViewModel(services, state, new FakeDialogService(), (_, _) => Task.CompletedTask);
        return (vm, state);
    }

    [Fact]
    public async Task Initial_State_Has_No_Printer()
    {
        var (vm, state) = BuildVm();

        Assert.Null(vm.Printer);
        Assert.False(vm.HasPrinter);
    }

    [Fact]
    public async Task Activate_Sets_Printer_From_Parameter()
    {
        var (vm, state) = BuildVm();
        var printer = TestPrinters.NetworkPrinter();

        await vm.ActivateAsync(printer);

        Assert.Same(printer, vm.Printer);
        Assert.True(vm.HasPrinter);
        Assert.Equal("ET-2850", vm.ModelText);
        Assert.Equal("Network (192.168.1.50)", vm.ConnectionText);
    }

    [Fact]
    public async Task Activate_Uses_State_SelectedPrinter()
    {
        var (vm, state) = BuildVm();
        var printer = TestPrinters.NetworkPrinter();
        state.SelectedPrinter = printer;

        await vm.ActivateAsync(null);

        Assert.Same(printer, vm.Printer);
    }

    [Fact]
    public async Task ResolveWebAdmin_Builds_Candidates_For_Network_Printer()
    {
        var (vm, state) = BuildVm();
        var printer = TestPrinters.NetworkPrinter();
        await vm.ActivateAsync(printer);

        vm.ResolveWebAdminAddressCommand.Execute(null);

        await Task.Delay(300);

        Assert.NotNull(vm.DetectedAddress);
        Assert.Equal("192.168.1.50", vm.DetectedAddress);
    }

    [Fact]
    public async Task Usb_Printer_Reports_No_WebAdmin_Address()
    {
        var (vm, state) = BuildVm();
        var printer = TestPrinters.UsbPrinter();
        await vm.ActivateAsync(printer);

        vm.ResolveWebAdminAddressCommand.Execute(null);

        await Task.Delay(300);

        Assert.Equal(string.Empty, vm.DetectedAddress);
    }

    [Fact]
    public async Task Computed_Properties_Reflect_Printer_Data()
    {
        var (vm, state) = BuildVm();
        var printer = TestPrinters.NetworkPrinter();
        await vm.ActivateAsync(printer);

        Assert.Equal("ET-2850", vm.ModelText);
        Assert.Equal("Epson", vm.ManufacturerText);
        Assert.Equal("EPSON ET-2850", vm.DriverText);
        Assert.Equal("192.168.1.50", vm.AddressText);
        Assert.NotNull(vm.Printer);
        Assert.True(vm.Printer!.IsDefault);
    }

    [Fact]
    public async Task OpenWebAdmin_When_No_Printer_Sets_Message()
    {
        var (vm, state) = BuildVm();

        vm.OpenWebAdminCommand.Execute(null);

        Assert.Contains("Select a printer", vm.WebAdminMessage);
    }

    [Fact]
    public async Task Commands_Exist()
    {
        var (vm, state) = BuildVm();

        Assert.NotNull(vm.ResolveWebAdminAddressCommand);
        Assert.NotNull(vm.OpenWebAdminCommand);
        Assert.NotNull(vm.RunDiagnosticsCommand);
        Assert.NotNull(vm.OpenMaintenanceCommand);
        Assert.NotNull(vm.OpenWasteInkCommand);
        Assert.NotNull(vm.BackToPrintersCommand);
    }
}
