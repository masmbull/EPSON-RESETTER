using EpsonResetter.App.Services;
using EpsonResetter.App.ViewModels;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.Settings;

namespace EpsonResetter.Tests;

public class WasteInkViewModelTests
{
    private static (WasteInkViewModel Vm, AppState State) BuildVm()
    {
        var settings = new AppSettings();
        var services = new AppServices(settings);
        var state = new AppState(services);
        var vm = new WasteInkViewModel(services, state);
        return (vm, state);
    }

    [Fact]
    public void Initial_State_Has_No_Printer()
    {
        var (vm, state) = BuildVm();

        Assert.Null(vm.Printer);
        Assert.False(vm.HasPrinter);
        Assert.Equal("Unknown", vm.SupportLevel);
    }

    [Fact]
    public void Activate_Sets_Printer_And_Loads_Profile()
    {
        var (vm, state) = BuildVm();
        var printer = TestPrinters.NetworkPrinter();

        vm.ActivateAsync(printer).Wait();

        Assert.Same(printer, vm.Printer);
        Assert.True(vm.HasPrinter);
        Assert.Equal("ET-2850", vm.ModelText);
        // ET-2850 is in the default registry but unsupported
        Assert.Equal("Unsupported", vm.SupportLevel);
    }

    [Fact]
    public void Unknown_Model_Reports_Unknown()
    {
        var (vm, state) = BuildVm();
        var printer = new PrinterDevice
        {
            Id = "unknown",
            Name = "Unknown Printer",
            ModelName = "ZZZ-9999",
            DriverName = "Unknown",
            PortName = "USB001",
            ConnectionType = PrinterConnectionType.Usb,
            DetectedAtUtc = DateTime.UtcNow
        };

        vm.ActivateAsync(printer).Wait();

        Assert.Equal("Unknown", vm.SupportLevel);
        Assert.False(vm.IsKnownModel);
    }

    [Fact]
    public void ReadCounter_When_No_Printer_Sets_Message()
    {
        var (vm, state) = BuildVm();

        vm.ReadCounterCommand.Execute(null);

        Assert.Contains("Select a printer", vm.CounterMessage);
    }

    [Fact]
    public void EvaluateEligibility_When_No_Printer_Sets_Message()
    {
        var (vm, state) = BuildVm();

        vm.EvaluateEligibilityCommand.Execute(null);

        Assert.Contains("Select a printer", vm.EligibilitySummary);
    }

    [Fact]
    public void Commands_Exist()
    {
        var (vm, state) = BuildVm();

        Assert.NotNull(vm.ReadCounterCommand);
        Assert.NotNull(vm.EvaluateEligibilityCommand);
    }
}
