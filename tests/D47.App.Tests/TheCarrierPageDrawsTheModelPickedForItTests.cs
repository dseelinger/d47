using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class TheCarrierPageDrawsTheModelPickedForItTests
{
    private const string Stats =
        """{"timestamp":"2026-09-22T23:24:52Z","event":"CarrierStats","CarrierID":3715429376,"CarrierType":"FleetCarrier","Callsign":"BNH-T2F","Name":"Sacred Fire","DockingAccess":"all","JumpRangeCurr":500.0,"SpaceUsage":{"TotalCapacity":25000,"Cargo":540,"FreeSpace":23530},"Finance":{"CarrierBalance":990302661},"Crew":[{"CrewRole":"Refuel","Activated":true,"Enabled":true}]}""";

    private static CarrierHullMemory Memory() => new(new ViewStateStore(
        new AppPaths("/carrier-hull-tests"),
        new MemoryFileSystem(),
        NullLogger<ViewStateStore>.Instance));

    private static CarrierPage Page(CarrierHullMemory memory)
    {
        Assert.True(JournalEvent.TryParse(Stats, NullLogger.Instance, out var parsed));
        var carrier = CarrierState.None.Apply(parsed!);

        return new CarrierPage(
            new CarrierSource(() => carrier, () => CarrierState.NoSquadron, () => 0),
            hullPictures: true,
            hulls: memory);
    }

    private static string? Drawn(Window window) =>
        window.GetVisualDescendants().OfType<HullPicture>().Single().Hull;

    [AvaloniaFact]
    public void ACarrierWithNoPickIsDrawnAsTheDrake()
    {
        var window = new Window { Content = Page(Memory()), Width = 924, Height = 640 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("fleetcarrier", Drawn(window));

        window.Close();
    }

    [AvaloniaFact]
    public void ThePickedModelIsDrawnAndKeptByANewPage()
    {
        var memory = Memory();
        var window = new Window { Content = Page(memory), Width = 924, Height = 640 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var segment = window.GetVisualDescendants().OfType<Segment>().Single();
        segment.GetVisualDescendants().OfType<RadioButton>().ElementAt(2).IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("fleetcarrier_nautilus", Drawn(window));

        window.Close();

        var again = new Window { Content = Page(memory), Width = 924, Height = 640 };

        again.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("fleetcarrier_nautilus", Drawn(again));

        again.Close();
    }

    [Fact]
    public void ACarrierModelIsRememberedPerCarrier()
    {
        var memory = Memory();

        memory.Remember("AAA-111", "fleetcarrier_victory");
        memory.Remember("BBB-222", "fleetcarrier_fortune");

        Assert.Equal("fleetcarrier_victory", memory.Of("AAA-111"));
        Assert.Equal("fleetcarrier_fortune", memory.Of("BBB-222"));
        Assert.Equal("fleetcarrier", memory.Of("CCC-333"));
    }

    [Fact]
    public void AnUnknownModelReadsAsTheDrake()
    {
        var memory = Memory();

        memory.Remember("AAA-111", "fleetcarrier_unheard_of");

        Assert.Equal("fleetcarrier", memory.Of("AAA-111"));
    }
}
