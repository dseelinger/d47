using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Knowledge;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

public sealed class ACarrierlessChapterEndsNearItsShipsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly AdventureStory Chapter = new("s", "S", "public", "hidden", 1, 10, 3, Slots: [PlannedChapterWorld.Bounty]);

    private const string BaseSystem = "Hale's Berth";

    private const long BaseMarket = 7001;

    private static RoundScriptedLlmProvider Provider() =>
        new(Saying(PlannedChapterWorld.Lines(ChapterPlanner.MostObjectives)), Saying(PlannedChapterWorld.NoObjections));

    private static void Succeeded(AdventureOutcome outcome) => Assert.True(outcome.Succeeded, outcome.Refusal);

    private static string Prompt(RoundScriptedLlmProvider provider) =>
        provider.Requests.First(request => request.ResponseSchema?.Name == "story_lines").Prompt.History[0].Text;

    private static StoredShip Ship(int id, string system, long? market, bool inTransit = false) =>
        new(id, "cobramkiii", null, system) { MarketId = market, InTransit = inTransit };

    [Fact]
    public void TheBaseIsTheStationWithTheMostStoredShips()
    {
        var found = ShipBases.Of([Ship(1, "Alpha", 1), Ship(2, "Beta", 2), Ship(3, "Beta", 2)], carrier: false, homeSystem: "Gamma");

        Assert.Equal(new ShipBase("Beta", 2, null, 2), found);
    }

    [Fact]
    public void ShipsInTransitAreNotCounted()
    {
        var found = ShipBases.Of([Ship(1, "Alpha", 1), Ship(2, "Beta", 2, inTransit: true), Ship(3, "Beta", 2, inTransit: true)], carrier: false, homeSystem: null);

        Assert.Equal("Alpha", found?.System);
    }

    [Fact]
    public void ATieGoesToTheStationNearerTheCommander()
    {
        var distances = new Dictionary<string, double> { ["Alpha"] = 300, ["Beta"] = 40 };

        var found = ShipBases.Of([Ship(1, "Alpha", 1), Ship(2, "Beta", 2)], carrier: false, homeSystem: null, system => distances[system]);

        Assert.Equal("Beta", found?.System);
    }

    [Fact]
    public void WithNoStoredShipTheBaseIsTheHomeSystemSetting()
    {
        var found = ShipBases.Of([Ship(1, "Alpha", 1, inTransit: true)], carrier: false, homeSystem: " Gamma ");

        Assert.Equal(new ShipBase("Gamma", null, null, 0), found);
        Assert.True(found!.IsHomeSystem);
    }

    [Fact]
    public void WithAFleetCarrierThereIsNoBase() =>
        Assert.Null(ShipBases.Of([Ship(1, "Alpha", 1)], carrier: true, homeSystem: "Gamma"));

    [Fact]
    public void WithNeitherStoredShipsNorAHomeSystemThereIsNoBase() =>
        Assert.Null(ShipBases.Of([], carrier: false, homeSystem: " "));

    [Fact]
    public void AnOrdinaryChapterAndARewriteAreHeldToTheBase()
    {
        Assert.True(ShipBases.Applies(new AdventureAsk(Story: Chapter)));
        Assert.True(ShipBases.Applies(new AdventureAsk(Story: Chapter, Rewrite: new AdventureRewrite(null!, 0))));
    }

    [Theory]
    [InlineData("beacon")]
    [InlineData("finale")]
    [InlineData("toward")]
    [InlineData("black")]
    [InlineData("home")]
    [InlineData("chosen")]
    [InlineData("asked")]
    [InlineData("short")]
    [InlineData("no story")]
    public void AnExemptChapterIsNotHeldToTheBase(string kind)
    {
        var heading = new AdventureHeading("Tavell's Reach", 5, new StarPosition(900, 0, 0));
        var ask = kind switch
        {
            "beacon" => new AdventureAsk(Story: Chapter with { Beacon = new AdventureBeacon(1, "B") }),
            "finale" => new AdventureAsk(Story: Chapter with { FinaleChapter = 2 }),
            "toward" => new AdventureAsk(Story: Chapter with { InTheBlack = true, Toward = "Colonia" }),
            "black" => new AdventureAsk(Story: Chapter with { InTheBlack = true }),
            "home" => new AdventureAsk(Story: Chapter with { Home = HomeLeg.Sol }),
            "chosen" => new AdventureAsk(Story: Chapter with { Chosen = heading }),
            "asked" => new AdventureAsk(Story: Chapter, Rewrite: new AdventureRewrite(null!, 0) { Heading = heading }),
            "short" => new AdventureAsk(Length: AdventureLength.Short, Story: Chapter),
            _ => new AdventureAsk(),
        };

        Assert.False(ShipBases.Applies(ask));
    }

    private const string TransferLine = "tell the Commander to transfer there the ships they will need";

    [Fact]
    public async Task AnExemptChapterFarFromTheBaseTellsTheWriterToExplainAndTransfer()
    {
        var provider = Provider();

        Succeeded(await Generator(provider, new Galaxy()).GenerateAsync(new AdventureAsk(Story: Chapter with { InTheBlack = true }), Now, CancellationToken.None));

        var brief = Prompt(provider);
        Assert.Contains("Its opening must say why the story stays away from Hale's Berth", brief);
        Assert.Contains(TransferLine, brief);
        Assert.Contains("Once the ships are moved, the base moves with them.", brief);
    }

    [Fact]
    public async Task AnExemptChapterWithinTheReachOfTheBaseIsNotTold()
    {
        var provider = Provider();

        Succeeded(await Generator(provider, new Galaxy { At = 480 }, lightYears: 480).GenerateAsync(new AdventureAsk(Story: Chapter with { InTheBlack = true }), Now, CancellationToken.None));

        Assert.DoesNotContain(TransferLine, Prompt(provider));
    }

    [Fact]
    public async Task AnExemptChapterWithAFleetCarrierIsNotTold()
    {
        var provider = Provider();

        Succeeded(await Generator(provider, new Galaxy(), carrier: true).GenerateAsync(new AdventureAsk(Story: Chapter with { InTheBlack = true }), Now, CancellationToken.None));

        Assert.DoesNotContain(TransferLine, Prompt(provider));
    }

    [Fact]
    public async Task AnExemptChapterWithNoBaseIsNotTold()
    {
        var provider = Provider();

        Succeeded(await Generator(provider, new Galaxy(), stored: false).GenerateAsync(new AdventureAsk(Story: Chapter with { InTheBlack = true }), Now, CancellationToken.None));

        Assert.DoesNotContain(TransferLine, Prompt(provider));
    }

    [Fact]
    public async Task AChapterHeldToTheBaseIsNotToldToTransfer()
    {
        var provider = Provider();

        Succeeded(await Generator(provider, new Galaxy()).GenerateAsync(new AdventureAsk(Story: Chapter), Now, CancellationToken.None));

        Assert.DoesNotContain(TransferLine, Prompt(provider));
    }

    [Fact]
    public void WithNoStoredShipTheStepIsAnArrivalInTheHomeSystem()
    {
        var inputs = PlannerWorld.Inputs(PlannerWorld.Bounty) with { Base = new ShipBase("Fixture Home", null, null, 0) };
        var places = PlannerWorld.Places() with { Base = new ChapterBase("Fixture Home", 70, null, null, new StarPosition(700, 0, 0)) };

        foreach (var seed in new uint[] { 1, 2, 3, 4, 5 })
        {
            var last = PlannerWorld.Planned(inputs, places, seed).Objectives[^1].Steps[^1];

            Assert.Equal(TriggerKind.Arrive, last.Kind);
            Assert.Equal("Fixture Home", last.System);
        }
    }

    [Fact]
    public void WhenTheBaseCannotBePlacedAChapterEndingElsewhereStillEndsAtIt()
    {
        var inputs = PlannerWorld.Inputs(PlannerWorld.Bounty) with { Base = new ShipBase("Fixture Home", 7000, "Home Port", 3) };
        var places = PlannerWorld.Places() with { Base = new ChapterBase("Fixture Home", 70, "Home Port", 7000, null) };

        foreach (var seed in new uint[] { 1, 2, 3, 4, 5 })
        {
            var last = PlannerWorld.Planned(inputs, places, seed).Objectives[^1].Steps[^1];

            Assert.Equal(TriggerKind.Dock, last.Kind);
            Assert.Equal(7000, last.MarketId);
        }
    }

    [Fact]
    public void TheSolBandMeasuresTheStopBeforeTheStepBackToTheBase()
    {
        var start = new StarPosition(1000, 0, 0);
        var inputs = PlannerWorld.Inputs(PlannerWorld.Bounty) with
        {
            Position = start,
            Band = ChapterBand.Of(start)!.Value,
            Reach = 300,
            Base = new ShipBase("Fixture Home", 7000, "Home Port", 3),
        };
        var places = new ChapterPlaces
        {
            Systems = [PlannerWorld.Place(PlannerWorld.Start, 1, 1000), PlannerWorld.Place("Fixture Out", 2, 980), PlannerWorld.Place("Fixture Mid", 3, 1060)],
            InBand = new ChapterArea(ChapterAreaState.Listed, [PlannerWorld.Station("Band Port", "Fixture Band", 4, 400, 800)], [PlannerWorld.Body("Fixture Band 1", "Fixture Band", 4, 1, 800)]),
            Base = new ChapterBase("Fixture Home", 70, "Home Port", 7000, new StarPosition(0, 0, 0)),
        };

        foreach (var seed in new uint[] { 1, 2, 3, 4, 5 })
        {
            var plan = PlannerWorld.Planned(inputs, places, seed);
            var steps = plan.Objectives[^1].Steps;

            Assert.Equal(PlannedPurpose.Band, plan.Objectives[^1].Purpose);
            Assert.Equal(7000, steps[^1].MarketId);
            Assert.Equal("Fixture Band", steps[^2].System);
        }
    }

    private static AdventureGenerator Generator(ILlmProvider provider, Galaxy galaxy, double lightYears = 0, bool carrier = false, bool stored = true, string? home = null)
    {
        var store = new GameStateStore();
        var lines = new List<string>
        {
            """{ "timestamp":"2026-10-01T11:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
            """{ "timestamp":"2026-10-01T11:00:01Z", "event":"Rank", "Combat":1, "Trade":7, "Explore":5, "Soldier":0, "Exobiologist":0, "Empire":1, "Federation":1, "CQC":0 }""",
            $$"""{ "timestamp":"2026-10-01T11:01:00Z", "event":"Location", "StarSystem":"Oppi", "SystemAddress":3382387380970, "StarPos":[{{lightYears}},0,0], "Docked":false }""",
        };

        if (stored)
        {
            lines.Add("""
                { "timestamp":"2026-10-01T11:01:01Z", "event":"StoredShips", "StationName":"Oppi Port", "MarketID":5001, "StarSystem":"Oppi", "ShipsHere":[], "ShipsRemote":[
                  { "ShipID":2, "ShipType":"cobramkiii", "StarSystem":"Hale's Berth", "ShipMarketID":7001, "TransferPrice":1000, "TransferTime":100, "Value":100000, "Hot":false },
                  { "ShipID":3, "ShipType":"cobramkiii", "StarSystem":"Hale's Berth", "ShipMarketID":7001, "TransferPrice":1000, "TransferTime":100, "Value":100000, "Hot":false },
                  { "ShipID":4, "ShipType":"cobramkiii", "StarSystem":"Oppi", "ShipMarketID":5001, "TransferPrice":1000, "TransferTime":100, "Value":100000, "Hot":false },
                  { "ShipID":5, "ShipType":"cobramkiii", "StarSystem":"Far Shore", "ShipMarketID":8001, "InTransit":true },
                  { "ShipID":6, "ShipType":"cobramkiii", "StarSystem":"Far Shore", "ShipMarketID":8001, "InTransit":true },
                  { "ShipID":7, "ShipType":"cobramkiii", "StarSystem":"Far Shore", "ShipMarketID":8001, "InTransit":true }
                ] }
                """.ReplaceLineEndings(" "));
        }

        if (carrier)
        {
            lines.Add("""{ "timestamp":"2026-10-01T11:01:02Z", "event":"CarrierStats", "CarrierID":3715429376, "Callsign":"XQZ-11A", "Name":"TEST CARRIER", "FuelLevel":800, "DockingAccess":"all" }""");
        }

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed), line);
            store.Apply(parsed!);
        }

        var state = store.Active;

        return new AdventureGenerator(
            () => provider,
            () => null,
            () => "You are the ship.",
            () => "core",
            () => null,
            () => state,
            () => galaxy,
            () => null,
            null,
            null,
            NullLogger.Instance)
        {
            HomeSystem = () => home,
        };
    }

    private sealed class Galaxy : StubGalaxyService, IGalaxyService
    {
        private static readonly StationSummary LanternHub = new()
        {
            Name = "Lantern Hub",
            SystemName = "Ossen's Lantern",
            MarketId = 9003,
            SystemAddress = AdventureFixtures.Lantern,
            Type = "Coriolis Starport",
            HasLargePad = true,
        };

        private static readonly StationSummary BerthStation = new()
        {
            Name = "Berth Station",
            SystemName = BaseSystem,
            MarketId = BaseMarket,
            SystemAddress = 7002,
            Type = "Coriolis Starport",
            HasLargePad = true,
        };

        /// <summary>Light years from any other system to the base.</summary>
        public double FromBase { get; init; } = 200;

        /// <summary>Where the Commander is along the x axis; stations to stop at lie beyond it.</summary>
        public double At { get; init; }

        /// <summary>Whether a position lookup knows the base's system.</summary>
        public bool PlacesBase { get; init; } = true;

        private static bool IsBase(string? system) => string.Equals(system, BaseSystem, StringComparison.OrdinalIgnoreCase);

        public Task<StarPosition?> PositionAsync(string system, CancellationToken cancellationToken) =>
            Task.FromResult<StarPosition?>(IsBase(system) && PlacesBase ? new StarPosition(500, 0, 0) : null);

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            var named = string.Equals(query.ReferenceSystem, "Ossen's Lantern", StringComparison.OrdinalIgnoreCase)
                ? new List<SystemSummary> { new() { Name = "Ossen's Lantern", SystemAddress = AdventureFixtures.Lantern, Government = "Democracy", Distance = 5 } }
                : IsBase(query.ReferenceSystem)
                    ? [new() { Name = BaseSystem, SystemAddress = 7002, Government = "Democracy", Distance = 0 }]
                    : [];

            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, named.Count, named));
        }

        public override Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(
                to == "Sol" ? (IsBase(from) ? 50 : 400)
                : IsBase(to) || IsBase(from) ? FromBase
                : 12);

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            IReadOnlyList<StationSummary> found = query switch
            {
                { StationName: "Lantern Hub" } => [LanternHub],
                { StationName: "Berth Station" } => [BerthStation],
                { StationName: null, ReferenceSystem: { } system } when IsBase(system) => [BerthStation],
                { ReferencePosition: not null } => [AChapterOutsideTheBubbleWorksTowardSolTests.Galaxy.HarbourPoint],
                _ => [.. Enumerable.Range(1, 6).Select(index => new StationSummary
                {
                    Name = $"Stop Port {index}", SystemName = $"Stop {index}", SystemAddress = 6000 + index, MarketId = 6100 + index,
                    Position = new StarPosition(At + (index * 5), 0, 0), Type = "Coriolis Starport", LargePads = 2,
                })],
            };

            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, found.Count, found));
        }

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, 0, []));
    }
}
