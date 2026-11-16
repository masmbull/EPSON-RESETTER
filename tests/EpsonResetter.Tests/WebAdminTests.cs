using EpsonResetter.Core.Models;
using EpsonResetter.Core.Settings;
using EpsonResetter.Core.WebAdmin;

namespace EpsonResetter.Tests;

public class WebAdminUrlValidatorTests
{
    [Theory]
    [InlineData("192.168.1.50", "http://192.168.1.50/")]
    [InlineData("http://192.168.1.50", "http://192.168.1.50/")]
    [InlineData("https://192.168.1.50", "https://192.168.1.50/")]
    [InlineData("  printer.local  ", "http://printer.local/")]
    [InlineData("printer.local:8080", "http://printer.local:8080/")]
    [InlineData("http://192.168.1.50:8080/", "http://192.168.1.50:8080/")]
    [InlineData("https://printer.local:8443/admin", "https://printer.local:8443/admin")]
    public void Accepts_Supported_Addresses(string input, string expected)
    {
        var result = WebAdminUrlValidator.Validate(input);

        Assert.True(result.IsValid, result.Error);
        Assert.Equal(expected, result.NormalizedUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://192.168.1.50")]
    [InlineData("file://192.168.1.50")]
    [InlineData("http://admin:secret@192.168.1.50")]
    [InlineData("http://192.168.1.50:99999")]
    [InlineData("http://bad host name")]
    [InlineData("http://")]
    [InlineData("javascript:alert(1)")]
    public void Rejects_Unsupported_Or_Unsafe_Addresses(string input)
    {
        var result = WebAdminUrlValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Null(result.NormalizedUrl);
    }

    [Fact]
    public void Rejects_Credentials_Explicitly()
    {
        var result = WebAdminUrlValidator.Validate("https://user:pass@printer.local");

        Assert.False(result.IsValid);
        Assert.Contains("username or password", result.Error);
    }

    [Fact]
    public void Flags_Insecure_Http_Addresses()
    {
        var result = WebAdminUrlValidator.Validate("http://printer.local");

        Assert.True(result.IsValid);
        Assert.True(result.UsedInsecureScheme);
        Assert.False(result.HasCredentials);
    }

    [Fact]
    public void Accepts_IPv6_Literal()
    {
        var result = WebAdminUrlValidator.Validate("http://[fe80::1]/");

        Assert.True(result.IsValid, result.Error);
        Assert.Equal("http://[fe80::1]/", result.NormalizedUrl);
    }
}

public class WebAdminAddressResolverTests
{
    private static AppSettings Settings(WebAdminSchemePreference scheme = WebAdminSchemePreference.Auto) => new()
    {
        PreferredScheme = scheme,
        HttpPort = 80,
        HttpsPort = 443,
        NetworkTimeoutMs = 1000
    };

    private static PrinterDevice NetworkPrinter(string? ip = "192.168.1.50", string port = "192.168.1.50") => new()
    {
        Id = "epson",
        Name = "EPSON ET-2850",
        ModelName = "ET-2850",
        DriverName = "EPSON ET-2850",
        PortName = port,
        IpAddress = ip,
        ConnectionType = PrinterConnectionType.Network,
        DetectedAtUtc = DateTime.UtcNow
    };

    private static PrinterDevice UsbPrinter() => new()
    {
        Id = "usb",
        Name = "EPSON ET-2850",
        ModelName = "ET-2850",
        DriverName = "EPSON ET-2850",
        PortName = "USB001",
        ConnectionType = PrinterConnectionType.Usb,
        DetectedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public void Builds_Http_And_Https_Candidates_From_Detected_Ip()
    {
        var resolution = new WebAdminAddressResolver(Settings()).Resolve(NetworkPrinter());

        Assert.Equal("192.168.1.50", resolution.DetectedAddress);
        Assert.Equal("port", resolution.AddressSource);
        Assert.Equal(2, resolution.Candidates.Count);
        Assert.Equal(WebAdminScheme.Http, resolution.Candidates[0].Scheme);
        Assert.Equal("http://192.168.1.50/", resolution.Candidates[0].Url);
        Assert.Equal(WebAdminScheme.Https, resolution.Candidates[1].Scheme);
        Assert.Contains(resolution.Warnings, warning => warning.Contains("unencrypted"));
    }

    [Fact]
    public void Prefers_Https_When_Configured()
    {
        var resolution = new WebAdminAddressResolver(Settings(WebAdminSchemePreference.Https)).Resolve(NetworkPrinter());

        Assert.Equal(WebAdminScheme.Https, resolution.Candidates[0].Scheme);
    }

    [Fact]
    public void Honours_Custom_Ports()
    {
        var settings = Settings();
        settings.HttpPort = 8080;

        var resolution = new WebAdminAddressResolver(settings).Resolve(NetworkPrinter());

        Assert.Equal("http://192.168.1.50:8080/", resolution.Candidates[0].Url);
    }

    [Fact]
    public void Manual_Address_Overrides_Detection()
    {
        var resolution = new WebAdminAddressResolver(Settings()).Resolve(NetworkPrinter(), "printer.local");

        Assert.Equal("manual", resolution.AddressSource);
        Assert.Single(resolution.Candidates);
        Assert.Equal("http://printer.local/", resolution.Candidates[0].Url);
    }

    [Fact]
    public void Invalid_Manual_Address_Produces_Warning_And_No_Candidates()
    {
        var resolution = new WebAdminAddressResolver(Settings()).Resolve(NetworkPrinter(), "ftp://bad");

        Assert.Empty(resolution.Candidates);
        Assert.Contains(resolution.Warnings, warning => warning.Contains("http:// and https://"));
    }

    [Fact]
    public void Usb_Printer_Without_Ip_Reports_No_Address()
    {
        var resolution = new WebAdminAddressResolver(Settings()).Resolve(UsbPrinter());

        Assert.Null(resolution.DetectedAddress);
        Assert.Equal("none", resolution.AddressSource);
        Assert.Empty(resolution.Candidates);
        Assert.Contains(resolution.Warnings, warning => warning.Contains("not connected over the network"));
    }

    [Fact]
    public void Network_Printer_Without_Ip_Asks_For_Manual_Address()
    {
        var printer = NetworkPrinter(ip: null, port: "WSD-1234");

        var resolution = new WebAdminAddressResolver(Settings()).Resolve(printer);

        Assert.Equal("none", resolution.AddressSource);
        Assert.Contains(resolution.Warnings, warning => warning.Contains("Enter the address manually"));
    }

    [Fact]
    public void Extracts_Ip_From_Port_Name()
    {
        var printer = NetworkPrinter(ip: null, port: "IP_10.0.0.7");

        var resolution = new WebAdminAddressResolver(Settings()).Resolve(printer);

        Assert.Equal("10.0.0.7", resolution.DetectedAddress);
        Assert.Equal("port-name", resolution.AddressSource);
    }

    [Fact]
    public void Candidate_Urls_Always_Pass_Validation()
    {
        var resolution = new WebAdminAddressResolver(Settings()).Resolve(NetworkPrinter());

        foreach (var candidate in resolution.Candidates)
        {
            Assert.True(WebAdminUrlValidator.Validate(candidate.Url).IsValid, candidate.Url);
        }
    }
}