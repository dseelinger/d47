using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A closed mining run is summarised on docking and a full hold is said once per run (#609).</summary>
public class AMiningRunSaysItsSummaryAndWhenTheHoldIsFullTests
{
    private const string DockedLine = """{ "timestamp":"2025-07-10T12:14:28Z", "event":"Docked", "StationName":"Jameson Memorial" }""";

    private const string DiedLine = """{ "timestamp":"2025-07-10T12:14:28Z", "event":"Died" }""";

    private static string Line(string time, string kind, string fields = "") =>
        "{ \"timestamp\":\"2025-07-10T" + time + "Z\", \"event\":\"" + kind + "\"" + (fields.Length > 0 ? ", " + fields : "") + " }";

    private static string Launch(string time, string type) => Line(time, "LaunchDrone", $"\"Type\":\"{type}\"");

    private static string Refine(string time, string symbol, string spoken) =>
        Line(time, "MiningRefined", $"\"Type\":\"${symbol}_name;\", \"Type_Localised\":\"{spoken}\"");

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState Commander(IEnumerable<string> lines, int capacity = 128)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{ "timestamp":"2025-07-10T06:23:44Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }"""));
        store.Apply(Event($$"""{ "timestamp":"2025-07-10T06:24:00Z", "event":"Loadout", "Ship":"python", "ShipID":7, "CargoCapacity":{{capacity}}, "Modules":[] }"""));

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static CalloutContext Context(CommanderGameState state, bool priming = false, params string[] events) =>
        new(DateTimeOffset.UnixEpoch, priming, state, GameStatus.Unknown, NavRoute.None, [.. events.Select(Event)]);

    private static IEnumerable<string> Repeat(int times, string line) => Enumerable.Repeat(line, times);

    private static List<string> TheJulyRun(string closing)
    {
        var lines = new List<string> { Launch("10:48:06", "Collection") };
        lines.AddRange(Repeat(33, Launch("10:49:30", "Prospector")));
        lines.AddRange(Repeat(18, Launch("10:49:40", "Collection")));
        lines.AddRange(Repeat(90, Refine("10:50:59", "platinum", "Platinum")));
        lines.AddRange(Repeat(12, Refine("11:10:00", "samarium", "Samarium")));
        lines.AddRange(Repeat(6, Refine("11:59:31", "praseodymium", "Praseodymium")));
        lines.Add(closing);

        return lines;
    }

    private static CommanderGameState Mining(int refined) =>
        Commander(Repeat(refined, Refine("10:50:00", "platinum", "Platinum")));

    private static CommanderGameState WithHold(CommanderGameState state, int tonnes)
    {
        state.Hold = new CargoHold { Vessel = "Ship", Count = tonnes, ReadAt = DateTimeOffset.UnixEpoch };
        return state;
    }

    [Fact]
    public void TheJulyRunIsSummarisedAtTheDocking() =>
        Assert.Equal(
            "Mining run: 108 tonnes in 1 hour 11 minutes, about 91 an hour. "
            + "90 Platinum, 12 Samarium, 6 Praseodymium. 33 prospectors and 19 collectors.",
            Assert.Single(new MiningSummaryCallout().Examine(Context(Commander(TheJulyRun(DockedLine)), false, DockedLine))).Text);

    [Fact]
    public void ARunWithNothingRefinedSaysNothing() =>
        Assert.Empty(new MiningSummaryCallout().Examine(
            Context(Commander([Launch("10:48:06", "Prospector"), DockedLine]), false, DockedLine)));

    [Fact]
    public void ARunThatEndedInADeathSaysNothing() =>
        Assert.Empty(new MiningSummaryCallout().Examine(
            Context(Commander(TheJulyRun(DiedLine)), false, DiedLine)));

    [Fact]
    public void AFourthMaterialIsCountedAndCoresAreAdded()
    {
        var lines = new List<string>
        {
            Line("10:48:06", "ProspectedAsteroid", "\"Materials\":[ { \"Name\":\"Platinum\", \"Proportion\":58.2 } ], \"MotherlodeMaterial\":\"Painite\""),
            Line("10:49:00", "ProspectedAsteroid", "\"Materials\":[ { \"Name\":\"Platinum\", \"Proportion\":58.2 } ], \"MotherlodeMaterial\":\"Painite\""),
        };
        lines.AddRange(Repeat(4, Refine("10:50:00", "platinum", "Platinum")));
        lines.AddRange(Repeat(3, Refine("10:51:00", "samarium", "Samarium")));
        lines.AddRange(Repeat(2, Refine("10:52:00", "osmium", "Osmium")));
        lines.Add(Refine("10:58:00", "gold", "Gold"));
        lines.Add(DockedLine);

        Assert.Equal(
            "Mining run: 10 tonnes in 9 minutes, about 61 an hour. 4 Platinum, 3 Samarium, 2 Osmium, and 1 more. 2 cores.",
            Assert.Single(new MiningSummaryCallout().Examine(Context(Commander(lines), false, DockedLine))).Text);
    }

    [Fact]
    public void PrimingSaysNoSummary() =>
        Assert.Empty(new MiningSummaryCallout().Examine(Context(Commander(TheJulyRun(DockedLine)), true, DockedLine)));

    [Fact]
    public void ARunningHoldReachingCapacitySaysHoldFullOnce()
    {
        var state = WithHold(Mining(108), 128);
        var callout = new HoldFullCallout();

        Assert.Equal("Hold full. 108 tonnes refined this run.", Assert.Single(callout.Examine(Context(state))).Text);
        Assert.Empty(callout.Examine(Context(state)));
    }

    [Fact]
    public void FillingAgainInTheSameRunIsNotRepeated()
    {
        var state = WithHold(Mining(108), 128);
        var callout = new HoldFullCallout();

        Assert.Single(callout.Examine(Context(state)));

        WithHold(state, 20);
        Assert.Empty(callout.Examine(Context(state)));

        WithHold(state, 128);
        Assert.Empty(callout.Examine(Context(state)));
    }

    [Fact]
    public void AFullHoldWithNoRunOpenSaysNothing() =>
        Assert.Empty(new HoldFullCallout().Examine(Context(WithHold(Commander([]), 128))));

    [Fact]
    public void AHoldWithRoomLeftSaysNothing() =>
        Assert.Empty(new HoldFullCallout().Examine(Context(WithHold(Mining(108), 127))));

    [Fact]
    public void ARunFullBeforePrimingEndedIsNotSaidAfterIt()
    {
        var state = WithHold(Mining(108), 128);
        var callout = new HoldFullCallout();

        Assert.Empty(callout.Examine(Context(state, priming: true)));
        Assert.Empty(callout.Examine(Context(state)));
    }

    [Fact]
    public void BothRowsAreOnByDefaultAndEachSwitchesItsOwnLine()
    {
        var settings = new D47Settings();

        Assert.True(settings.Callouts.MiningSummary);
        Assert.True(settings.Callouts.HoldFull);
        Assert.Equal("mining-summary", new MiningSummaryCallout().Id);
        Assert.Equal("hold-full", new HoldFullCallout().Id);
    }

    [Fact]
    public void ARowSwitchedOffIsNotSaid()
    {
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(new HoldFullCallout());
        engine.SetEnabled("hold-full", false, DateTimeOffset.UnixEpoch);

        Assert.False(engine.IsEnabled("hold-full"));
    }
}
