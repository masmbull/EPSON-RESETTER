using EpsonResetter.Core.Models;
using EpsonResetter.Core.Settings;
using EpsonResetter.Infrastructure.Persistence;
using EpsonResetter.Infrastructure.Printing;

namespace EpsonResetter.Tests;

public sealed class PersistenceTests : IDisposable
{
    private readonly string _directory;

    public PersistenceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "epson-resetter-tests", Guid.NewGuid().ToString("N"));
        SqliteDatabase.CurrentDatabasePath = Path.Combine(_directory, "test.db");
    }

    public void Dispose()
    {
        SqliteDatabase.ResetToDefaultPath();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (Directory.Exists(_directory))
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
                // Best effort cleanup.
            }
        }
    }

    [Fact]
    public async Task Activity_Is_Persisted_And_Read_Back_In_Newest_First_Order()
    {
        var store = new SqliteActivityStore();

        await store.AddAsync(new ActivityRecord
        {
            TimestampUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            PrinterName = "EPSON ET-2850",
            ModelName = "ET-2850",
            Action = "Diagnostics.Run",
            Result = ActivityResult.Success,
            Details = "{\"passed\":9}"
        });

        await store.AddAsync(new ActivityRecord
        {
            TimestampUtc = new DateTime(2026, 1, 2, 3, 5, 5, DateTimeKind.Utc),
            PrinterName = "EPSON ET-2850",
            ModelName = "ET-2850",
            Action = "WasteInk.Read",
            Result = ActivityResult.Unavailable,
            Error = "No verified method"
        });

        var records = await store.ListAsync();

        Assert.Equal(2, records.Count);
        Assert.Equal("WasteInk.Read", records[0].Action);
        Assert.Equal(ActivityResult.Unavailable, records[0].Result);
        Assert.Equal("No verified method", records[0].Error);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), records[1].TimestampUtc);
        Assert.True(records[0].Id > records[1].Id);
    }

    [Fact]
    public async Task Activity_Can_Be_Cleared()
    {
        var store = new SqliteActivityStore();
        await store.AddAsync(new ActivityRecord { PrinterName = "P", Action = "Test", Result = ActivityResult.Success });

        var deleted = await store.ClearAsync();
        var remaining = await store.ListAsync();

        Assert.Equal(1, deleted);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task Printer_Cache_Round_Trips_All_Fields()
    {
        var store = new SqlitePrinterCacheStore();
        var printer = new PrinterDevice
        {
            Id = "epson l3250",
            Name = "EPSON L3250",
            ModelName = "L3250",
            DriverName = "EPSON L3250 Series",
            DriverManufacturer = "Epson",
            DriverVersion = "3.1.0.0",
            DriverInfName = "epson030001.inf",
            PortName = "192.168.1.50",
            IpAddress = "192.168.1.50",
            PortNumber = 9100,
            ConnectionType = PrinterConnectionType.Network,
            Condition = PrinterCondition.Normal,
            WorkOffline = false,
            IsDefault = true,
            Location = "Office",
            Comment = "Front desk",
            Manufacturer = "Epson",
            DetectedAtUtc = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc)
        };

        await store.SaveAsync(new[] { printer });
        var loaded = await store.LoadAsync();

        var single = Assert.Single(loaded);
        Assert.Equal(printer.Id, single.Id);
        Assert.Equal(printer.Name, single.Name);
        Assert.Equal(printer.ModelName, single.ModelName);
        Assert.Equal(printer.DriverName, single.DriverName);
        Assert.Equal(printer.DriverManufacturer, single.DriverManufacturer);
        Assert.Equal(printer.DriverVersion, single.DriverVersion);
        Assert.Equal(printer.DriverInfName, single.DriverInfName);
        Assert.Equal(printer.PortName, single.PortName);
        Assert.Equal(printer.IpAddress, single.IpAddress);
        Assert.Equal(printer.PortNumber, single.PortNumber);
        Assert.Equal(printer.ConnectionType, single.ConnectionType);
        Assert.Equal(printer.Condition, single.Condition);
        Assert.Equal(printer.WorkOffline, single.WorkOffline);
        Assert.Equal(printer.IsDefault, single.IsDefault);
        Assert.Equal(printer.Location, single.Location);
        Assert.Equal(printer.Comment, single.Comment);
        Assert.Equal(printer.Manufacturer, single.Manufacturer);
        Assert.Equal(printer.DetectedAtUtc, single.DetectedAtUtc);
    }

    [Fact]
    public async Task Saving_Printers_Replaces_The_Previous_Snapshot()
    {
        var store = new SqlitePrinterCacheStore();
        var first = new PrinterDevice { Id = "one", Name = "Printer One", DetectedAtUtc = DateTime.UtcNow };
        var second = new PrinterDevice { Id = "two", Name = "Printer Two", DetectedAtUtc = DateTime.UtcNow };

        await store.SaveAsync(new[] { first });
        await store.SaveAsync(new[] { second });

        var loaded = await store.LoadAsync();
        Assert.Single(loaded);
        Assert.Equal("Printer Two", loaded[0].Name);
    }

    [Fact]
    public async Task Settings_Round_Trip()
    {
        var store = new SqliteSettingsStore();
        var settings = new AppSettings
        {
            PreferredScheme = WebAdminSchemePreference.Https,
            HttpPort = 8081,
            HttpsPort = 8443,
            NetworkTimeoutMs = 2500,
            ShowOnlyEpsonByDefault = false,
            ConfirmBeforeOpeningAdmin = false
        };

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        Assert.Equal(settings.PreferredScheme, loaded.PreferredScheme);
        Assert.Equal(settings.HttpPort, loaded.HttpPort);
        Assert.Equal(settings.HttpsPort, loaded.HttpsPort);
        Assert.Equal(settings.NetworkTimeoutMs, loaded.NetworkTimeoutMs);
        Assert.False(loaded.ShowOnlyEpsonByDefault);
        Assert.False(loaded.ConfirmBeforeOpeningAdmin);
    }

    [Fact]
    public async Task Settings_Default_When_Empty()
    {
        var store = new SqliteSettingsStore();

        var loaded = await store.LoadAsync();

        Assert.Equal(WebAdminSchemePreference.Auto, loaded.PreferredScheme);
        Assert.Equal(80, loaded.HttpPort);
        Assert.Equal(443, loaded.HttpsPort);
    }

    [Fact]
    public void Invalid_Settings_Are_Reported()
    {
        var settings = new AppSettings { HttpPort = 0, HttpsPort = 70000, NetworkTimeoutMs = 10 };

        var errors = settings.Validate();

        Assert.Equal(3, errors.Count);
    }
}

public class MaintenanceSafetyTests
{
    [Theory]
    [InlineData("EPSON ET-2850", true)]
    [InlineData("HP LaserJet Professional P1102", true)]
    [InlineData("Printer (Copy 1)", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Printer\"Name", false)]
    [InlineData("Printer;Name", false)]
    [InlineData("Printer\nName", false)]
    public void Only_Safe_Printer_Names_Are_Accepted(string name, bool expected)
    {
        Assert.Equal(expected, WindowsMaintenanceService.IsSafePrinterName(name));
    }
}