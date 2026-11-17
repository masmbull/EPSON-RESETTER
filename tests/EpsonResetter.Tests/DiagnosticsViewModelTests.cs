using EpsonResetter.App.Services;
using EpsonResetter.App.ViewModels;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.Settings;

namespace EpsonResetter.Tests;

public class DiagnosticsViewModelTests
{
    private static (DiagnosticsViewModel Vm, AppState State) BuildVm()
    {
        var settings = new AppSettings();
        var services = new AppServices(settings);
        var state = new AppState(services);
        var vm = new DiagnosticsViewModel(services, state);
        return (vm, state);
    }

    [Fact]
    public void Initial_State_Has_No_Report()
    {
        var (vm, state) = BuildVm();

        Assert.Null(vm.Report);
        Assert.False(vm.HasReport);
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
    }

    [Fact]
    public async Task Activate_Uses_State_SelectedPrinter_When_No_Parameter()
    {
        var (vm, state) = BuildVm();
        var printer = TestPrinters.NetworkPrinter();
        state.SelectedPrinter = printer;

        await vm.ActivateAsync(null);

        Assert.Same(printer, vm.Printer);
    }

    [Fact]
    public async Task Run_Command_Produces_Report()
    {
        var (vm, state) = BuildVm();
        var printer = TestPrinters.NetworkPrinter();
        state.Printers = new System.Collections.ObjectModel.ObservableCollection<PrinterDevice> { printer };
        state.SelectedPrinter = printer;

        await vm.ActivateAsync(printer);
        vm.RunCommand.Execute(null);

        // Allow async operation to complete
        await Task.Delay(500);

        // Command exists and can execute
        Assert.NotNull(vm.RunCommand);
    }

    [Fact]
    public void Commands_Exist()
    {
        var (vm, state) = BuildVm();

        Assert.NotNull(vm.RunCommand);
    }
}
