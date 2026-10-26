using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Reminders;
using D47.Core.Storage;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Reminders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Records that outlive an Operation take no place, signal or faction from the lines the Operation wrote.</summary>
public class NothingKeptAfterAnOperationTakesAPlaceFromInsideItTests
{
    private const string Fid = "F1";

    private static readonly DateTimeOffset Start = new(2026, 10, 20, 20, 0, 0, TimeSpan.Zero);

    private static string Line(int minute, string kind, string fields = "") =>
        $$"""{ "timestamp":"{{Start.AddMinutes(minute):yyyy-MM-ddTHH:mm:ssZ}}", "event":"{{kind}}", "FID":"{{Fid}}"{{fields}} }""";

    private static string Place(int minute, string kind, string system, long address, string extra = "") =>
        Line(minute, kind, $", \"StarSystem\":\"{system}\", \"SystemAddress\":{address}, \"StarPos\":[1.0,2.0,3.0]{extra}");

    private static string Faction(string name) =>
        $", \"Factions\":[{{ \"Name\":\"{name}\", \"MyReputation\":50.0, \"Influence\":0.4, \"FactionState\":\"None\" }}]";

    /// <summary>A main-game session, an Operation in the same system, and the main game again.</summary>
    private static string[] Journal() =>
    [
        Line(0, "Commander"),
        Line(0, "LoadGame"),
        Line(0, "GameModeChange", ", \"GameMode\":\"MainGame\""),
        Place(1, "Location", "Sol", 100, Faction("Sol Freeholds")),
        Line(5, "Commander"),
        Line(5, "LoadGame"),
        Line(5, "GameModeChange", ", \"GameMode\":\"Operation\""),
        Place(6, "Location", "Wolf 359", 200, ", \"Docked\":true, \"MarketID\":77, \"StationName\":\"Instance\"" + Faction("$faction_FrontlineSolutions;")),
        Line(7, "FSSSignalDiscovered", ", \"SystemAddress\":200, \"SignalName\":\"$Operations_Runner_Name;\", \"SignalType\":\"Megaship\""),
        Line(7, "FSSSignalDiscovered", ", \"SystemAddress\":200, \"SignalName\":\"Nav Beacon\", \"SignalType\":\"NavBeacon\""),
        Line(8, "ShipTargeted", ", \"TargetLocked\":true, \"Ship_Localised\":\"Python\", \"PilotName\":\"$cmdr_decorate:#name=Ada;\", \"PilotName_Localised\":\"CMDR Ada\""),
        Line(40, "Commander"),
        Line(40, "LoadGame"),
        Line(40, "GameModeChange", ", \"GameMode\":\"MainGame\""),
        Place(41, "Location", "Sol", 100),
    ];

    private static List<JournalEvent> Marked() => [.. OperationMarker.Mark(Journal().Select(ReminderBench.Parse))];

    private static List<JournalEvent> InTheOperation() => [.. Marked().Where(journalEvent => journalEvent.InOperation)];

    [Fact]
    public void TheTravelLogHoldsOnlyTheMainGamePlaces()
    {
        var log = new TravelLog(new MemoryFileSystem(), NullLogger.Instance);

        log.Fold(Journal());

        Assert.All(log.Steps(Fid), step => Assert.Equal("Sol", step.System));
        Assert.Equal(2, log.Count(Fid));
    }

    [Fact]
    public void TheSignalSightingsHoldNothingFromTheInstance()
    {
        var sightings = new SignalSightings(new MemoryFileSystem(), NullLogger.Instance);

        sightings.Fold(Journal());

        Assert.Empty(sightings.Snapshot());
    }

    [Fact]
    public void ACommanderTargetedInsideIsLoggedWithNoSystem()
    {
        var log = new CommanderLog(new MemoryFileSystem(), NullLogger.Instance);

        log.Fold(Journal());

        Assert.Equal(string.Empty, log.LastTargeted()?.System);
    }

    [Fact]
    public void LiveStateStillFoldsTheInstanceLocation()
    {
        var state = new CommanderGameState(new CommanderIdentity(Fid, "Fixture"));

        foreach (var journalEvent in Marked().TakeWhile(journalEvent => journalEvent.Timestamp < Start.AddMinutes(10)))
        {
            state.Apply(journalEvent);
        }

        Assert.Equal("Wolf 359", state.Location.StarSystem);
    }

    [Fact]
    public void TheMainGameLocationAfterTheExitIsFoldedAsNormal()
    {
        var log = new TravelLog(new MemoryFileSystem(), NullLogger.Instance);
        log.Fold(Journal());

        Assert.Equal(Start.AddMinutes(41), log.Steps(Fid)[^1].At);
    }

    [Fact]
    public void AFactionReadingFromTheInstanceIsNotKept()
    {
        var reputation = Marked().Aggregate(ReputationState.Empty, (held, journalEvent) => held.Apply(journalEvent));

        Assert.Equal(["Sol Freeholds"], reputation.Factions.Keys);
    }

    [Fact]
    public void ASystemReadingFromTheInstanceIsNotKept()
    {
        var standings = Marked().Aggregate(SystemStandings.Empty, (held, journalEvent) => held.Apply(journalEvent));

        Assert.Null(standings.Latest("Wolf 359"));
        Assert.NotNull(standings.Latest("Sol"));
    }

    [Fact]
    public void ARoutePlanStopIsNotReachedFromInsideAnOperation()
    {
        var book = new RoutePlanBook("route-plans.json", new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);
        book.Record(
            new PlottedRoute("Sol", "Colonia", 100, 5, [new RouteWaypoint("Wolf 359", 8, 50, true), new RouteWaypoint("Colonia", 8, 0, false)]),
            "Sol to Colonia",
            Start.AddMinutes(-10),
            Fid);

        book.Apply(InTheOperation(), Fid);

        Assert.Null(book.Last(RoutePlanKind.Jump, Fid)?.Reached);
    }

    [Fact]
    public void AReminderForTheSystemDoesNotFireInsideAnOperation()
    {
        var bench = new ReminderBench();
        bench.Arm(Fid, JournalTrigger.ArrivalIn, "Sell the painite.", "Wolf 359");

        Assert.Empty(bench.Say(new CommanderGameState(new CommanderIdentity(Fid, "Fixture")), events: [.. InTheOperation()]));
    }

    [Fact]
    public void AnArriveTriggerIsNotSatisfiedByTheInstanceLocation()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 200 };

        Assert.DoesNotContain(Marked(), journalEvent => AdventureFold.Matches(trigger, journalEvent));
    }

    [Fact]
    public void AnAdventureStandingDoesNotMoveToTheInstance()
    {
        var standing = Marked().Where(journalEvent => journalEvent.Kind == "Location").Aggregate(
            new AdventureStanding { Adventure = AdventureFixtures.LanternRoute() },
            AdventureFold.Apply);

        Assert.NotEqual(200, standing.SystemAddress);
        Assert.NotEqual(77, standing.MarketId);
    }
}
