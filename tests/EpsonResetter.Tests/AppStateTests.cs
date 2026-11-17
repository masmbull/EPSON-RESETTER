using EpsonResetter.App.Services;
using EpsonResetter.App.ViewModels;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.Settings;
using System.Collections.ObjectModel;

namespace EpsonResetter.Tests;

public class AppStateTests
{
    [Fact]
    public void Computed_Properties_Reflect_Printer_List()
    {
        var settings = new AppSettings();
        var services = new AppServices(settings);
        var state = new AppState(services);

        // Replace collection to trigger PropertyChanged
        state.Printers = new ObservableCollection<PrinterDevice>
        {
            TestPrinters.NetworkPrinter(),
            TestPrinters.UsbPrinter(),
            TestPrinters.OfflinePrinter()
        };

        Assert.Equal(3, state.EpsonCount);
        Assert.Equal(2, state.NetworkCount);
        Assert.Equal(1, state.OfflineCount);
        Assert.Equal(0, state.UnknownCount);
    }

    [Fact]
    public void SelectedPrinter_Defaults_To_Null()
    {
        var settings = new AppSettings();
        var services = new AppServices(settings);
        var state = new AppState(services);

        Assert.Null(state.SelectedPrinter);
    }

    [Fact]
    public void Status_Message_Defaults_To_Ready()
    {
        var settings = new AppSettings();
        var services = new AppServices(settings);
        var state = new AppState(services);

        Assert.Equal("Ready.", state.StatusMessage);
        Assert.False(state.IsScanning);
        Assert.Null(state.LastScan);
    }

    [Fact]
    public void Settings_Are_Exposed_From_Services()
    {
        var settings = new AppSettings { ShowOnlyEpsonByDefault = true, HttpPort = 8080 };
        var services = new AppServices(settings);
        var state = new AppState(services);

        Assert.Same(settings, state.Settings);
        Assert.True(state.Settings.ShowOnlyEpsonByDefault);
        Assert.Equal(8080, state.Settings.HttpPort);
    }

    [Fact]
    public void RecentActivity_Starts_Empty()
    {
        var settings = new AppSettings();
        var services = new AppServices(settings);
        var state = new AppState(services);

        Assert.Empty(state.RecentActivity);
    }
}
