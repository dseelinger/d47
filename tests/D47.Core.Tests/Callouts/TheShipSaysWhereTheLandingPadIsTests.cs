using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class EveryRingPadHasAPlaceTests
{
    [Fact]
    public void PadsOneToFortyFiveAreAllInTheTable()
    {
        Assert.Equal(Enumerable.Range(1, 45), LandingPads.All.Select(pad => pad.Number).Order());
    }

    [Fact]
    public void EachPadResolvesToASegmentAndADepth()
    {
        Assert.All(LandingPads.All, pad =>
        {
            Assert.InRange(pad.Segment, 1, 12);
            Assert.InRange(pad.Depth, 0, 4);
            Assert.InRange(pad.Hour, 1, 12);
            Assert.NotNull(DockingCallout.Line("Coriolis", pad.Number));
        });
    }

    [Theory]
    [InlineData(1, 6)]
    [InlineData(10, 8)]
    [InlineData(24, 12)]
    [InlineData(26, 1)]
    [InlineData(45, 5)]
    public void SegmentOneIsSixOClockAndEachAfterIsAnHourOn(int number, int hour)
    {
        Assert.Equal(hour, LandingPads.Find(number)!.Value.Hour);
    }
}

public class TheShipSaysWhereTheLandingPadIsTests
{
    private static readonly DateTimeOffset Granted = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Row(string kind, params (string Key, object Value)[] fields)
    {
        var all = new Dictionary<string, object?> { ["timestamp"] = "2026-10-05T12:00:00Z", ["event"] = kind };
        foreach (var (key, value) in fields)
        {
            all[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(all), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Grant(int pad, string stationType = "Coriolis") =>
        Row("DockingGranted", ("LandingPad", pad), ("MarketID", 128000000), ("StationName", "Jameson Memorial"), ("StationType", stationType));

    private static List<Announcement> Examine(DockingCallout callout, DateTimeOffset now, params JournalEvent[] events) =>
        [.. callout.Examine(new CalloutContext(now, false, null, GameStatus.Unknown, NavRoute.None, events))];

    /// <summary>What is said from the grant tick through ten seconds after it, one tick a second.</summary>
    private static List<Announcement> Run(DockingCallout callout, JournalEvent grant, (int Second, JournalEvent Event)? later = null)
    {
        var said = Examine(callout, Granted, grant);

        for (var second = 1; second <= 10; second++)
        {
            JournalEvent[] events = later is { } l && l.Second == second ? [l.Event] : [];
            said.AddRange(Examine(callout, Granted.AddSeconds(second), events));
        }

        return said;
    }

    [Fact]
    public void PadTenAtACoriolisIsEightOClockHalfwayIn()
    {
        var said = Assert.Single(Run(new DockingCallout(), Grant(10)));

        Assert.Equal("Pad ten, eight o'clock, halfway in.", said.Text);
    }

    [Theory]
    [InlineData("AsteroidBase")]
    [InlineData("Orbis")]
    [InlineData("Ocellus")]
    public void EveryRingStationSaysTheSameLineAsACoriolis(string stationType)
    {
        var said = Assert.Single(Run(new DockingCallout(), Grant(10, stationType)));

        Assert.Equal("Pad ten, eight o'clock, halfway in.", said.Text);
    }

    [Theory]
    [InlineData("FleetCarrier")]
    [InlineData("Outpost")]
    [InlineData("CraterPort")]
    [InlineData("CraterOutpost")]
    [InlineData("OnFootSettlement")]
    public void AStationWithoutTheRingSaysNothing(string stationType)
    {
        Assert.Empty(Run(new DockingCallout(), Grant(3, stationType)));
    }

    [Theory]
    [InlineData(1, "near the entrance")]
    [InlineData(2, "near the entrance")]
    [InlineData(4, "at the back")]
    [InlineData(14, "at the back")]
    public void TheDepthIsSaidInWords(int pad, string depth)
    {
        Assert.EndsWith($", {depth}.", DockingCallout.Line("Coriolis", pad));
    }

    [Fact]
    public void TheLineWaitsForTheSettleAndIsSaidOnce()
    {
        var callout = new DockingCallout();
        var settle = DockingCallout.DockingSettle;

        Assert.Empty(Examine(callout, Granted, Grant(10)));
        Assert.Empty(Examine(callout, Granted + settle - TimeSpan.FromMilliseconds(100)));
        Assert.Single(Examine(callout, Granted + settle));
        Assert.Empty(Examine(callout, Granted + settle + TimeSpan.FromMilliseconds(100)));
        Assert.Empty(Examine(callout, Granted + settle + TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData("Docked")]
    [InlineData("DockingCancelled")]
    [InlineData("DockingTimeout")]
    [InlineData("DockingDenied")]
    public void AnEndInsideTheSettleDropsTheLine(string kind)
    {
        Assert.Empty(Run(new DockingCallout(), Grant(10), (2, Row(kind, ("StationName", "Jameson Memorial")))));
    }

    [Fact]
    public void ASecondGrantReplacesTheFirst()
    {
        var said = Assert.Single(Run(new DockingCallout(), Grant(10), (2, Grant(1))));

        Assert.Equal("Pad one, six o'clock, near the entrance.", said.Text);
    }

    [Fact]
    public void AGrantReplayedOnStartupIsNotSpoken()
    {
        var callout = new DockingCallout();

        Assert.Empty(callout.Examine(new CalloutContext(Granted, true, null, GameStatus.Unknown, NavRoute.None, [Grant(10)])));
        Assert.Empty(Examine(callout, Granted.AddSeconds(5)));
    }

    [Fact]
    public void TheStationsOwnLineIsQueuedFirst()
    {
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance)
            .Add(new IncomingMessages { Enabled = () => true, IncludeNpcs = () => true })
            .Add(new DockingCallout());

        var granted = Row(
            "ReceiveText",
            ("From", "Jameson Memorial"),
            ("Message", "$STATION_docking_granted;"),
            ("Message_Localised", "Docking request granted."),
            ("Channel", "npc"));

        engine.Tick(new CalloutContext(Granted, false, null, GameStatus.Unknown, NavRoute.None, [granted, Grant(10)]));
        var first = engine.Drain();

        engine.Tick(new CalloutContext(Granted + DockingCallout.DockingSettle, false, null, GameStatus.Unknown, NavRoute.None, []));
        var second = engine.Drain();

        Assert.NotEmpty(first);
        Assert.DoesNotContain(first, said => said.Key == DockingCallout.Key);
        Assert.Equal(DockingCallout.Key, Assert.Single(second).Key);
    }

    [Fact]
    public void WithTheRowOffNothingIsSaid()
    {
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(new DockingCallout());
        engine.SetEnabled("docking", false);

        for (var second = 0; second <= 10; second++)
        {
            JournalEvent[] events = second == 0 ? [Grant(10)] : [];
            engine.Tick(new CalloutContext(Granted.AddSeconds(second), false, null, GameStatus.Unknown, NavRoute.None, events));
        }

        Assert.Empty(engine.Drain());
    }
}
