using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The two stores that already hold the answer, joined.</summary>
public class AnUnreadLoadoutFallsBackToTheRememberedOneTests
{
    private const string Scoop = """{"Slot":"Slot03_Size6","Item":"int_fuelscoop_size6_class5","On":true,"Health":1.0}""";

    private const string CargoRack = """{"Slot":"Slot03_Size6","Item":"int_cargorack_size6_class6","On":true,"Health":1.0}""";

    private const string Collector = """{"Slot":"Slot04_Size4","Item":"int_dronecontrol_collection_size3_class3","On":true,"Health":1.0}""";

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>What d47 watched the Commander sitting in, before the session under test.</summary>
    private static ShipLoadouts Remembered(int shipId, params string[] modules) =>
        ShipLoadouts.Empty.Remember(
            ShipLoadout.Unknown.Apply(Parse(
                $$"""
                {"timestamp":"2025-07-01T10:00:00Z","event":"Loadout","Ship":"Anaconda","ShipID":{{shipId}},
                 "MaxJumpRange":50,"FuelCapacity":{"Main":32},"Modules":[{{string.Join(",", modules)}}]}
                """)),
            DateTimeOffset.Parse("2025-07-01T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// The session <c>Journal.2025-07-07T155535.01.log</c> is: the Commander identified, the game
    /// loaded naming a <c>ShipID</c>, an arrival — and no <c>Loadout</c> behind any of it.
    /// </summary>
    private static CommanderGameState Session(
        ShipLoadouts? remembered = null,
        int shipId = 5,
        params string[] extra)
    {
        var store = new GameStateStore
        {
            RestoreLoadouts = _ => remembered,
        };

        store.Apply(Parse("""{"timestamp":"2025-07-07T15:55:35Z","event":"Commander","FID":"F337","Name":"Fixture"}"""));
        store.Apply(Parse(
            $$"""
            {"timestamp":"2025-07-07T15:55:36Z","event":"LoadGame","FID":"F337","Commander":"Fixture",
             "Ship":"Anaconda","ShipID":{{shipId}},"Credits":100}
            """));

        foreach (var json in extra)
        {
            store.Apply(Parse(json));
        }

        return store.Active!;
    }

    // ---- The join ----------------------------------------------------------------------------

    /// <summary>
    /// The accepted-when case: <c>Ship</c> unknown, a remembered loadout for the <c>ShipID</c>
    /// <c>LoadGame</c> named, and the question answered from memory rather than dropped.
    /// </summary>
    [Fact]
    public void TheRememberedLoadoutAnswersWhatIsFittedWhenTheJournalHasNot()
    {
        var state = Session(Remembered(5, Scoop));

        Assert.False(state.Ship.IsKnown);
        Assert.True(state.FlownShip.IsKnown);
        Assert.True(state.FlownShip.Fitted(ShipLoadout.FuelScoop));
    }

    /// <summary>Positively no scoop is an answer too, and it is the one #323 and #324 act on.</summary>
    [Fact]
    public void ARememberedLoadoutCanAlsoShowTheModuleIsNotFitted() =>
        Assert.False(Session(Remembered(5, CargoRack)).FlownShip.Fitted(ShipLoadout.FuelScoop));

    /// <summary>
    /// The id from <c>LoadGame</c> picks the ship, so a Commander who logged in to one ship is not
    /// answered about the one they were flying yesterday.
    /// </summary>
    [Fact]
    public void TheShipTheGameLoadedIntoIsTheOneAnswered()
    {
        var scooper = Remembered(5, Scoop);
        var both = scooper with
        {
            Ships = new Dictionary<int, RememberedShip>(scooper.Ships)
            {
                [9] = Remembered(9, CargoRack).Ships[9],
            },
        };

        Assert.True(Session(both, shipId: 5).FlownShip.Fitted(ShipLoadout.FuelScoop));
        Assert.False(Session(both, shipId: 9).FlownShip.Fitted(ShipLoadout.FuelScoop));
    }

    /// <summary>
    /// The last resort: no <c>ShipID</c> has been named at all, and the ship the Commander last sat in
    /// is the honest answer to "what is fitted on my ship".
    /// </summary>
    [Fact]
    public void TheShipLastSatInAnswersWhenNoIdHasBeenNamed()
    {
        var remembered = Remembered(5, Scoop);

        var store = new GameStateStore { RestoreLoadouts = _ => remembered };
        store.Apply(Parse("""{"timestamp":"2025-07-07T15:55:35Z","event":"Commander","FID":"F337","Name":"Fixture"}"""));

        Assert.True(store.Active!.FlownShip.Fitted(ShipLoadout.FuelScoop));
    }

    /// <summary>The live loadout wins wherever there is one.</summary>
    [Fact]
    public void TheLiveLoadoutOutranksTheRememberedOne()
    {
        var state = Session(
            Remembered(5, Scoop),
            5,
            $$"""{"timestamp":"2025-07-07T16:00:00Z","event":"Loadout","Ship":"Anaconda","ShipID":5,"Modules":[{{CargoRack}}]}""");

        Assert.True(state.Ship.IsKnown);
        Assert.False(state.FlownShip.Fitted(ShipLoadout.FuelScoop));
    }

    /// <summary>A LoadGame does not overwrite the id of a loadout already in hand.</summary>
    [Fact]
    public void ALoadGameTakesTheIdOnlyWhileTheLoadoutIsUnknown()
    {
        var state = Session(
            null,
            5,
            $$"""{"timestamp":"2025-07-07T16:00:00Z","event":"Loadout","Ship":"Anaconda","ShipID":5,"Modules":[{{Scoop}}]}""",
            """{"timestamp":"2025-07-07T17:00:00Z","event":"LoadGame","FID":"F337","Commander":"Fixture","Ship":"Anaconda","ShipID":9,"Credits":100}""");

        Assert.Equal(5, state.Ship.ShipId);
        Assert.True(state.Loadouts.Ships.ContainsKey(5));
        Assert.False(state.Loadouts.Ships.ContainsKey(9));
    }

    // ---- The three callouts on that session ---------------------------------------------------

    private static NavRoute Route(params (string System, string Class, double X)[] hops) => new()
    {
        Hops = [.. hops.Select(hop => new RouteHop(hop.System, hop.Class) { Position = (hop.X, 0, 0) })],
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private static CalloutContext Context(CommanderGameState state, GameStatus status, NavRoute route, params JournalEvent[] events) =>
        new(DateTimeOffset.UnixEpoch, IsPriming: false, state, status, route, events);

    /// <summary>The route progress clause, on the session where <c>Loadout</c> never arrived.</summary>
    [Theory]
    [InlineData(Scoop, true)]
    [InlineData(CargoRack, false)]
    public void TheRouteClauseReadsTheRememberedLoadout(string module, bool said)
    {
        var jump = """{"timestamp":"2025-07-07T15:56:00Z","event":"FSDJump","StarSystem":"A","JumpDist":10}""";
        var state = Session(Remembered(5, module), 5, jump);

        var route = Route(("A", "K", 0), ("B", "K", 10));

        var progress = new RouteCallout { EveryNJumps = 1 }
            .Examine(Context(state, GameStatus.Unknown, route, Parse(jump)))
            .Single(announcement => announcement.Key == "route.progress");

        Assert.Equal(said, progress.Text.Contains("scoopable", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The strand family, on the same session.</summary>
    [Theory]
    [InlineData(Scoop, true)]
    [InlineData(CargoRack, false)]
    public void TheStrandWarningReadsTheRememberedLoadout(string module, bool warned)
    {
        var jump = """{"timestamp":"2025-07-07T15:56:00Z","event":"FSDJump","StarSystem":"Here","JumpDist":40}""";
        var state = Session(Remembered(5, module), 5, jump);

        var route = Route(("Here", "K", 0), ("Dead End", "T", 40), ("Far", "K", 120));

        var status = new GameStatus
        {
            Flags = StatusFlags.InMainShip,
            FuelMain = 30,
            ReadAt = DateTimeOffset.UnixEpoch,
        };

        Assert.Equal(
            warned,
            new FuelCallout()
                .Examine(Context(state, status, route, Parse(jump)))
                .Any(announcement => announcement.Key.StartsWith("fuel.route", StringComparison.Ordinal)));
    }

    /// <summary>The emissions callout, on the same session.</summary>
    [Theory]
    [InlineData(Collector, true)]
    [InlineData(CargoRack, false)]
    public void TheEmissionsLineReadsTheRememberedLoadout(string module, bool said)
    {
        const string Arrival =
            """
            {"timestamp":"2025-07-07T15:56:00Z","event":"FSDJump","StarSystem":"Deciat","Population":5000000,
             "SystemAllegiance":"Independent","SystemFaction":{"Name":"Ruling party"},
             "Factions":[{"Name":"Ruling party","Allegiance":"Independent","FactionState":"Boom",
             "ActiveStates":[{"State":"Boom"}]}]}
            """;

        var state = Session(Remembered(5, module), 5, Arrival);

        Assert.Equal(
            said,
            new EmissionCallout()
                .Examine(Context(state, GameStatus.Unknown, NavRoute.None, Parse(Arrival)))
                .Any(announcement => announcement.Key == EmissionCallout.Key));
    }

    /// <summary>The tool surface reaches the same join.</summary>
    [Fact]
    public async Task TheEngineeringToolReadsTheRememberedLoadout()
    {
        var state = Session(Remembered(5, CargoRack));

        var registry = Capabilities.CapabilityRegistry.Build(
            [Capabilities.Builtin.EngineeringCapability.Create(() => state)]);

        var answered = await registry.InvokeAsync(
            "get_module_engineering",
            new Capabilities.ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

        Assert.False(answered.IsError);
        Assert.DoesNotContain("no loadout yet", answered.Content, StringComparison.Ordinal);
        Assert.Contains("Anaconda", answered.Content, StringComparison.Ordinal);
        Assert.Contains("last saw", answered.Content, StringComparison.Ordinal);
    }

    // ---- Saying so when there is still nothing -------------------------------------------------

    /// <summary>
    /// A silent null branch is how this went unnoticed for months, so what is left of it says so: the
    /// ship id, and the journal event that led to the question.
    /// </summary>
    [Fact]
    public void NoLoadoutAnywhereIsWarnedAboutOnceASession()
    {
        var logger = new RecordingLogger();
        var jump = """{"timestamp":"2025-07-07T15:56:00Z","event":"FSDJump","StarSystem":"A","JumpDist":10}""";

        // Nothing remembered and nothing read: a ship d47 has never seen.
        var state = Session(null, 5, jump);

        var route = Route(("A", "K", 0), ("B", "K", 10));

        var callout = new RouteCallout(logger) { EveryNJumps = 1 };

        callout.Examine(Context(state, GameStatus.Unknown, route, Parse(jump))).ToList();
        callout.Examine(Context(state, GameStatus.Unknown, route, Parse(jump))).ToList();

        var warning = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Warning, warning.Level);

        // The ship it could not describe, and what asked — not merely which callout was running.
        Assert.Contains("5", warning.Message, StringComparison.Ordinal);
        Assert.Contains("FSDJump", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Once per game session rather than once per process: re-entering the game is a new LoadGame, and
    /// a Commander who has just restarted Elite has a right to the warning again.
    /// </summary>
    [Fact]
    public void AFreshLoadGameEarnsTheWarningAgain()
    {
        var logger = new RecordingLogger();
        var jump = """{"timestamp":"2025-07-07T15:56:00Z","event":"FSDJump","StarSystem":"A","JumpDist":10}""";
        var state = Session(null, 5, jump);

        var route = Route(("A", "K", 0), ("B", "K", 10));

        var callout = new RouteCallout(logger) { EveryNJumps = 1 };

        callout.Examine(Context(state, GameStatus.Unknown, route, Parse(jump))).ToList();

        state.Apply(Parse(
            """
            {"timestamp":"2025-07-07T18:00:00Z","event":"LoadGame","FID":"F337","Commander":"Fixture",
             "Ship":"Anaconda","ShipID":5,"Credits":100}
            """));

        callout.Examine(Context(state, GameStatus.Unknown, route, Parse(jump))).ToList();

        Assert.Equal(2, logger.Entries.Count);
    }

    /// <summary>
    /// And nothing is said where the fallback answered: the warning is about silence, not about the
    /// loadout having come from memory.
    /// </summary>
    [Fact]
    public void ARememberedAnswerIsNotWarnedAbout()
    {
        var logger = new RecordingLogger();
        var jump = """{"timestamp":"2025-07-07T15:56:00Z","event":"FSDJump","StarSystem":"A","JumpDist":10}""";
        var state = Session(Remembered(5, Scoop), 5, jump);

        var route = Route(("A", "K", 0), ("B", "K", 10));

        new RouteCallout(logger) { EveryNJumps = 1 }
            .Examine(Context(state, GameStatus.Unknown, route, Parse(jump)))
            .ToList();

        Assert.Empty(logger.Entries);
    }
}
