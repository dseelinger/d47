using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class TheCarrierPageShowsItsHullTests
{
    private const string Stats =
        """{"timestamp":"2026-09-22T23:24:52Z","event":"CarrierStats","CarrierID":3715429376,"CarrierType":"FleetCarrier","Callsign":"BNH-T2F","Name":"Sacred Fire","DockingAccess":"all","JumpRangeCurr":500.0,"SpaceUsage":{"TotalCapacity":25000,"Cargo":540,"FreeSpace":23530},"Finance":{"CarrierBalance":990302661},"Crew":[{"CrewRole":"Refuel","Activated":true,"Enabled":true}]}""";

    private static int Pictures(bool hullPictures)
    {
        Assert.True(JournalEvent.TryParse(Stats, NullLogger.Instance, out var parsed));
        var carrier = CarrierState.None.Apply(parsed!);

        var page = new CarrierPage(
            new CarrierSource(() => carrier, () => CarrierState.NoSquadron, () => 0),
            hullPictures: hullPictures);
        var window = new Window { Content = page, Width = 924, Height = 640 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var count = window.GetVisualDescendants().OfType<HullPicture>().Count();

        window.Close();

        return count;
    }

    [AvaloniaFact]
    public void TheHullSitsBesideTheTilesWhenHullPicturesIsOn() => Assert.Equal(1, Pictures(hullPictures: true));

    [AvaloniaFact]
    public void TheHullIsAbsentWhenHullPicturesIsOff() => Assert.Equal(0, Pictures(hullPictures: false));
}
