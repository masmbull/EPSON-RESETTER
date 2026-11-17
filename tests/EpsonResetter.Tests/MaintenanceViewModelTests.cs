using EpsonResetter.App.Services;
using EpsonResetter.App.ViewModels;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.Settings;

namespace EpsonResetter.Tests;

public class MaintenanceViewModelTests
{
    private static (MaintenanceViewModel Vm, AppState State) BuildVm()
    {
        var settings = new AppSettings();
        var services = new AppServices(settings);
        var state = new AppState(services);
        var vm = new MaintenanceViewModel(services, state, new FakeDialogService());
        return (vm, state);
    }

    [Fact]
    public void Initial_State_Has_No_Printer()
    {
        var (vm, state) = BuildVm();

        Assert.Null(vm.Printer);
        Assert.False(vm.HasPrinter);
        Assert.Contains("Choose an action", vm.Message);
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
    public async Task Activate_Uses_State_SelectedPrinter()
    {
        var (vm, state) = BuildVm();
        var printer = TestPrinters.NetworkPrinter();
        state.SelectedPrinter = printer;

        await vm.ActivateAsync(null);

        Assert.Same(printer, vm.Printer);
    }

    [Fact]
    public void OpenQueue_When_No_Printer_Sets_Message()
    {
        var (vm, state) = BuildVm();

        vm.OpenQueueCommand.Execute(null);

        Assert.Contains("Select a printer first", vm.Message);
    }

    [Fact]
    public void Commands_Exist()
    {
        var (vm, state) = BuildVm();

        Assert.NotNull(vm.OpenQueueCommand);
        Assert.NotNull(vm.OpenPropertiesCommand);
        Assert.NotNull(vm.PrintTestPageCommand);
        Assert.NotNull(vm.OpenWebAdminCommand);
    }
}
