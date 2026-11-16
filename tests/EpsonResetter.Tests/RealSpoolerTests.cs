using EpsonResetter.Infrastructure.Printing;

namespace EpsonResetter.Tests;

/// <summary>
/// End-to-end checks against the real Windows spooler on the build machine.
/// They assert that values come from the operating system and that nothing is
/// fabricated when a printer is not present.
/// </summary>
public class RealSpoolerTests
{
    [Fact]
    public async Task Discovery_Returns_Live_Printer_Data()
    {
        var provider = new SpoolerPrinterProvider();

        var printers = await provider.GetPrintersAsync();

        foreach (var printer in printers)
        {
            Assert.False(string.IsNullOrWhiteSpace(printer.Name));
            Assert.False(string.IsNullOrWhiteSpace(printer.DriverName));
            Assert.NotEqual(default, printer.DetectedAtUtc);
        }

        // This machine has a locally installed HP printer. If it is present the
        // identification must be real and must not be mistaken for Epson.
        var hp = printers.FirstOrDefault(item => item.Name.Contains("HP", StringComparison.OrdinalIgnoreCase));
        if (hp is not null)
        {
            Assert.Equal("HP", hp.Manufacturer);
            Assert.False(hp.IsEpson);
            Assert.False(string.IsNullOrWhiteSpace(hp.PortName));
        }
    }

    [Fact]
    public async Task Unknown_Printer_Name_Returns_Null()
    {
        var provider = new SpoolerPrinterProvider();

        var printer = await provider.GetPrinterAsync("No Such Printer 12345");

        Assert.Null(printer);
    }

    [Fact]
    public async Task Discovery_Is_Deterministic_Across_Calls()
    {
        var provider = new SpoolerPrinterProvider();

        var first = await provider.GetPrintersAsync();
        var second = await provider.GetPrintersAsync();

        Assert.Equal(
            first.Select(item => item.Id),
            second.Select(item => item.Id));
    }
}