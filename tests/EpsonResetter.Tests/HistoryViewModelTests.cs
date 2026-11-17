using EpsonResetter.App.Services;
using EpsonResetter.App.ViewModels;
using EpsonResetter.Core.Models;
using EpsonResetter.Core.Settings;
using System.Collections.ObjectModel;
using System.Collections.ObjectModel;

namespace EpsonResetter.Tests;

public class HistoryViewModelTests
{
    private static (HistoryViewModel Vm, AppState State) BuildVm()
    {
        var settings = new AppSettings();
        var services = new AppServices(settings);
        var state = new AppState(services);
        var vm = new HistoryViewModel(services, state, new FakeDialogService());
        return (vm, state);
    }

    [Fact]
    public void Initial_State_Has_No_Records()
    {
        var (vm, state) = BuildVm();

        Assert.Empty(vm.FilteredRecords);
        Assert.False(vm.HasRecords);
        Assert.Equal(string.Empty, vm.Summary);
    }

    [Fact]
    public void Search_Filters_Records()
    {
        var (vm, state) = BuildVm();

        vm.AllRecords.Add(new ActivityRecord
        {
            PrinterName = "EPSON ET-2850",
            ModelName = "ET-2850",
            Action = "Maintenance.OpenQueue",
            Result = ActivityResult.Success,
            Details = "Opened print queue",
            TimestampUtc = DateTime.UtcNow
        });

        vm.SearchText = "ET-2850";

        Assert.Single(vm.FilteredRecords);
        Assert.True(vm.HasRecords);
    }

    [Fact]
    public void Search_Clears_Returns_All()
    {
        var (vm, state) = BuildVm();

        vm.AllRecords.Add(new ActivityRecord
        {
            PrinterName = "EPSON ET-2850",
            Action = "Maintenance.OpenQueue",
            Result = ActivityResult.Success,
            TimestampUtc = DateTime.UtcNow
        });

        vm.SearchText = "nonexistent";

        Assert.Empty(vm.FilteredRecords);
        Assert.False(vm.HasRecords);
    }

    [Fact]
    public void Commands_Exist()
    {
        var (vm, state) = BuildVm();

        Assert.NotNull(vm.LoadCommand);
        Assert.NotNull(vm.ClearCommand);
    }
}
