using EpsonResetter.Core.Models;
using EpsonResetter.Infrastructure.Printing;

namespace EpsonResetter.Tests;

public class PrinterMappingTests
{
    [Fact]
    public void Maps_Epson_Printer_With_Model_And_Driver_Details()
    {
        var device = PrinterMapper.Map(new SpoolerPrinterData
        {
            Name = "EPSON ET-2850 Series",
            DriverName = "EPSON ET-2850 Series",
            PortName = "USB001",
            PrinterStatus = 3,
            DriverManufacturer = "Epson",
            DriverVersion = "2.4.0.0",
            DriverInfName = "epson040001.inf"
        });

        Assert.Equal("Epson", device.Manufacturer);
        Assert.True(device.IsEpson);
        Assert.Equal("ET-2850", device.ModelName);
        Assert.Equal("EPSON ET-2850 Series", device.DriverName);
        Assert.Equal("Epson", device.DriverManufacturer);
        Assert.Equal("2.4.0.0", device.DriverVersion);
        Assert.Equal("epson040001.inf", device.DriverInfName);
        Assert.Equal("USB", device.ConnectionDisplay);
        Assert.Equal(PrinterConnectionType.Usb, device.ConnectionType);
        Assert.Equal(PrinterCondition.Normal, device.Condition);
    }

    [Fact]
    public void Maps_Network_Printer_From_Tcp_Ip_Port()
    {
        var device = PrinterMapper.Map(new SpoolerPrinterData
        {
            Name = "EPSON L3250",
            DriverName = "EPSON L3250 Series",
            PortName = "192.168.1.50",
            IpAddress = "192.168.1.50",
            PortNumber = 9100,
            PrinterStatus = 3
        });

        Assert.Equal(PrinterConnectionType.Network, device.ConnectionType);
        Assert.True(device.IsNetwork);
        Assert.Equal("192.168.1.50", device.IpAddress);
        Assert.Equal(9100, device.PortNumber);
        Assert.Contains("192.168.1.50", device.ConnectionDisplay);
    }

    [Fact]
    public void Extracts_Address_From_Ip_Named_Port()
    {
        var device = PrinterMapper.Map(new SpoolerPrinterData
        {
            Name = "EPSON XP-6100",
            DriverName = "EPSON XP-6100 Series",
            PortName = "IP_192.168.0.24",
            PrinterStatus = 3
        });

        Assert.Equal(PrinterConnectionType.Network, device.ConnectionType);
        Assert.Equal("192.168.0.24", EpsonResetter.Core.WebAdmin.WebAdminAddressResolver.ExtractAddress(device.PortName));
    }

    [Fact]
    public void Maps_Offline_Printer_Without_Inventing_Status()
    {
        var device = PrinterMapper.Map(new SpoolerPrinterData
        {
            Name = "EPSON WF-2850",
            DriverName = "EPSON WF-2850 Series",
            PortName = "WSD-1234",
            WorkOffline = true,
            PrinterStatus = 3
        });

        Assert.Equal(PrinterConnectionType.Network, device.ConnectionType);
        Assert.Equal(PrinterCondition.Offline, device.Condition);
    }

    [Fact]
    public void Maps_Non_Epson_Printer_Correctly()
    {
        var device = PrinterMapper.Map(new SpoolerPrinterData
        {
            Name = "HP LaserJet Professional P1102",
            DriverName = "HP LaserJet Professional P1102",
            PortName = "USB002",
            PrinterStatus = 3,
            IsDefault = true
        });

        Assert.False(device.IsEpson);
        Assert.Equal("HP", device.Manufacturer);
        Assert.Equal("LaserJet Professional P1102", device.ModelName);
        Assert.True(device.IsDefault);
    }

    [Fact]
    public void Unknown_Manufacturer_Is_Not_Identified_As_Epson()
    {
        var device = PrinterMapper.Map(new SpoolerPrinterData
        {
            Name = "Generic Text Only",
            DriverName = "Generic / Text Only",
            PortName = "LPT1",
            PrinterStatus = 1
        });

        Assert.Equal("Unknown", device.Manufacturer);
        Assert.False(device.IsEpson);
        Assert.Equal(PrinterConnectionType.Local, device.ConnectionType);
        Assert.Equal(PrinterCondition.Unknown, device.Condition);
    }

    [Fact]
    public void Printer_Id_Is_Stable_And_Case_Insensitive()
    {
        var device = PrinterMapper.Map(new SpoolerPrinterData
        {
            Name = "EPSON ET-2850",
            DriverName = "EPSON ET-2850",
            PortName = "USB001",
            PrinterStatus = 3
        });

        Assert.Equal("epson et-2850", device.Id);
    }

    [Theory]
    [InlineData("USB001", PrinterConnectionType.Usb)]
    [InlineData("DOT4_001", PrinterConnectionType.Usb)]
    [InlineData("PORTPROMPT:", PrinterConnectionType.Local)]
    [InlineData("192.168.1.5", PrinterConnectionType.Network)]
    [InlineData("IP_192.168.1.5", PrinterConnectionType.Network)]
    [InlineData("WSD-ABC", PrinterConnectionType.Network)]
    [InlineData("SomethingElse", PrinterConnectionType.Unknown)]
    public void Classifies_Known_Port_Schemes(string port, PrinterConnectionType expected)
    {
        Assert.Equal(expected, PortClassifier.Classify(port, null));
    }
}