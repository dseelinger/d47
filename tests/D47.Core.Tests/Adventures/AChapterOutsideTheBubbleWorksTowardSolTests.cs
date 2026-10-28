using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

public sealed class AChapterOutsideTheBubbleWorksTowardSolTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly AdventureStory Chapter = new("s", "S", "public", "hidden", 1, 10, 3);

    private const string Spine = """
        {"name": "The Long Way", "premise": "A way back.", "want": "To get home.",
         "stake": "Whether the ship lasts.", "turn": "The road is shorter.", "ending": "It is done."}
        """;

    private const string Beats = """
        {"opening": "A road.", "reply": "Here it is.", "beats": [
          {"title": "The Arrival", "function": "setup", "kind": "arrive", "reason": "Someone there knows the way.", "system": "Ossen's Lantern", "station": null, "body": null, "career": null, "rank": null, "line": "Here."},
          {"title": "The Turn", "function": "turn", "kind": "rank", "system": null, "station": null, "body": null, "career": "Trader", "rank": 8, "line": "Turn."},
          {"title": "The End", "function": "resolution", "kind": "rank", "system": null, "station": null, "body": null, "career": "Trader", "rank": 8, "line": "Done."}
        ]}
        """;

    private const string Docking = """
        {"opening": "A road.", "reply": "Here it is.", "beats": [
          {"title": "The Arrival", "function": "setup", "kind": "dock", "reason": "Someone there knows the way.", "system": "Ossen's Lantern", "station": "Lantern Hub", "body": null, "career": null, "rank": null, "line": "Here."},
          {"title": "The Turn", "function": "turn", "kind": "rank", "system": null, "station": null, "body": null, "career": "Trader", "rank": 8, "line": "Turn."},
          {"title": "The End", "function": "resolution", "kind": "rank", "system": null, "station": null, "body": null, "career": "Trader", "rank": 8, "line": "Done."}
        ]}
        """;

    private const string NoPlace = """
        {"opening": "A road.", "reply": "Here it is.", "beats": [
          {"title": "The Turn", "function": "turn", "kind": "rank", "system": null, "station": null, "body": null, "career": "Trader", "rank": 8, "line": "Turn."}
        ]}
        """;

    [Fact]
    public void AChapter588LightYearsOutHasTheBandFrom288To488() =>
        Assert.Equal(new ChapterBand.Range(288, 488), ChapterBand.Of(588));

    [Theory]
    [InlineData(450)]
    [InlineData(500)]
    public void AChapterInsideTheBubbleHasNoBand(double lightYears) => Assert.Null(ChapterBand.Of(lightYears));

    [Fact]
    public void AChapter1200LightYearsOutHasTheBandFrom900To1100() =>
        Assert.Equal(new ChapterBand.Range(900, 1100), ChapterBand.Of(1200));

    [Theory]
    [InlineData("beacon")]
    [InlineData("finale")]
    [InlineData("rewrite")]
    [InlineData("short")]
    public void ABeaconFinaleRewriteOrShortStoryHasNoBand(string kind)
    {
        var ask = kind switch
        {
            "beacon" => new AdventureAsk(Story: Chapter with { Beacon = new AdventureBeacon(1, "B") }),
            "finale" => new AdventureAsk(Story: Chapter with { FinaleChapter = 2 }),
            "rewrite" => new AdventureAsk(Story: Chapter, Rewrite: new AdventureRewrite(null!, 0)),
            _ => new AdventureAsk(Length: AdventureLength.Short),
        };

        Assert.Null(ChapterBand.Of(new StarPosition(588, 0, 0), ask));
        Assert.NotNull(ChapterBand.Of(new StarPosition(588, 0, 0), new AdventureAsk(Story: Chapter)));
    }

    [Fact]
    public async Task TheObjectivesPromptGivesTheBandFor588LightYearsOut()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        await Generator(provider, new Galaxy(), 588).GenerateAsync(new AdventureAsk(Story: Chapter), Now, CancellationToken.None);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("588 light years from Sol", brief);
        Assert.Contains("between 288 and 488 light years from Sol", brief);
    }

    [Fact]
    public async Task AChapter300LightYearsOutHasNoBandInItsPrompt()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        await Generator(provider, new Galaxy(), 300).GenerateAsync(new AdventureAsk(Story: Chapter), Now, CancellationToken.None);

        Assert.DoesNotContain("light years from Sol", provider.Requests[1].Prompt.History[0].Text);
    }

    [Fact]
    public async Task ALastStopAt400LightYearsFromSolPasses()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        var outcome = await Generator(provider, new Galaxy { FromSol = 400 }, 588).GenerateAsync(new AdventureAsk(Story: Chapter), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    [Fact]
    public async Task ALastStopAt550LightYearsFromSolIsRefusedWithTheBand()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats), Saying(Repairs.Replacing(Beats, 1)), Saying(Repairs.Replacing(Beats, 1)));

        var outcome = await Generator(provider, new Galaxy { FromSol = 550 }, 588).GenerateAsync(new AdventureAsk(Story: Chapter), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("its last stop is 550 light years from Sol", outcome.Refusal);
        Assert.Contains("between 288 and 488 light years from Sol", outcome.Refusal);
    }

    [Fact]
    public async Task AChapterWithNoPlaceObjectiveIsRefused()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(NoPlace), Saying(NoPlace), Saying(NoPlace));

        var outcome = await Generator(provider, new Galaxy { FromSol = 400 }, 588).GenerateAsync(new AdventureAsk(Story: Chapter), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("its last stop is 588 light years from Sol", outcome.Refusal);
    }

    [Fact]
    public async Task ALastStopThatCannotBeMeasuredBecauseTheSearchIsDownStopsTheWriteWithTheReason()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));
        var time = new ManualTime();

        var task = Generator(provider, new Galaxy { Down = true }, 588, time).GenerateAsync(new AdventureAsk(Story: Chapter), Now, CancellationToken.None);

        for (var pass = 0; pass < 200 && !task.IsCompleted; pass++)
        {
            time.Advance(AdventureResolver.OutageRetryDelay);
            await Task.WhenAny(task, Task.Delay(10, TestContext.Current.CancellationToken));
        }

        var outcome = await task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.False(outcome.Succeeded);
        Assert.False(outcome.Redraftable);
        Assert.Contains("galaxy search is unavailable", outcome.Refusal);
        Assert.Contains("the service is down", outcome.Refusal);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task TheObjectivesPromptListsTheInBandPlacesWithTheirDistancesFromTheirPositions()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        await Generator(provider, new Galaxy(), 588).GenerateAsync(new AdventureAsk(Story: Chapter), Now, CancellationToken.None);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("Real places inside the band, from the galaxy search: each system is between 288 and 488 light years from Sol", brief);
        Assert.Contains("- Velan's Rest (438 ly from Sol, 150 ly from the Commander): stations Harbour Point (large pad); landable bodies Velan's Rest 2 a", brief);
        Assert.Contains("The places listed as inside the band meet that rule.", brief);
    }

    [Fact]
    public async Task TheInBandSearchIsCentred150LightYearsTowardSolWithin50()
    {
        var galaxy = new Galaxy();

        await Generator(new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats)), galaxy, 588).GenerateAsync(new AdventureAsk(Story: Chapter), Now, CancellationToken.None);

        Assert.Equal([(new StarPosition(438, 0, 0), 50.0), (new StarPosition(438, 0, 0), 50.0)], galaxy.Around);
    }

    [Fact]
    public async Task APlaceOutsideTheBandOrNotFixedIsNotListedAsInsideIt()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));
        var galaxy = new Galaxy
        {
            InBandStations =
            [
                Galaxy.HarbourPoint with { Name = "Far Point", SystemName = "Far Reach", Position = new StarPosition(500, 0, 0) },
                Galaxy.HarbourPoint with { Name = "Fleet Carrier K2X", Type = "Drake-Class Carrier" },
            ],
            InBandBodies = [],
        };

        await Generator(provider, galaxy, 588).GenerateAsync(new AdventureAsk(Story: Chapter), Now, CancellationToken.None);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.DoesNotContain("Far Point", brief);
        Assert.DoesNotContain("K2X", brief);
        Assert.DoesNotContain("Real places inside the band", brief);
    }

    [Fact]
    public async Task WhenTheFirstSearchFindsNothingTheSecondIsCentred200TowardSolWithin100()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));
        var galaxy = new Galaxy { OnlyRadius = 100 };

        await Generator(provider, galaxy, 588).GenerateAsync(new AdventureAsk(Story: Chapter), Now, CancellationToken.None);

        Assert.Equal(
            [(new StarPosition(438, 0, 0), 50.0), (new StarPosition(438, 0, 0), 50.0), (new StarPosition(388, 0, 0), 100.0), (new StarPosition(388, 0, 0), 100.0)],
            galaxy.Around);
        Assert.Contains("- Velan's Rest (438 ly from Sol, 150 ly from the Commander)", provider.Requests[1].Prompt.History[0].Text);
    }

    [Fact]
    public async Task WithNoPlaceInsideTheBandTheChapterIsNotHeldToIt()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        var outcome = await Generator(provider, new Galaxy { FromSol = 550, InBandStations = [], InBandBodies = [] }, 588)
            .GenerateAsync(new AdventureAsk(Story: Chapter), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains("No station or landable body between 288 and 488 light years from Sol was found within reach, so this chapter is not held to a distance from Sol.", provider.Requests[1].Prompt.History[0].Text);
        Assert.Contains(outcome.Notes, note => note.Contains("so the chapter was not held to that band", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenTheInBandSearchIsDownTheBandStillApplies()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats), Saying(Repairs.Replacing(Beats, 1)), Saying(Repairs.Replacing(Beats, 1)));

        var outcome = await Generator(provider, new Galaxy { FromSol = 550, InBandDown = true }, 588)
            .GenerateAsync(new AdventureAsk(Reach: AdventureReach.Session, Story: Chapter), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("between 288 and 488 light years from Sol", outcome.Refusal);
        Assert.DoesNotContain("Move objective", outcome.Refusal);
        Assert.Contains(outcome.Notes, note => note.Contains("could not list the places inside the band toward Sol", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ABandRefusalOfAnArriveMovesItToAnInBandSystemWithinReach()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats), Saying(Repairs.Replacing(Beats, 1)), Saying(Repairs.Replacing(Beats, 1)));

        var outcome = await Generator(provider, new Galaxy { FromSol = 550 }, 588)
            .GenerateAsync(new AdventureAsk(Reach: AdventureReach.Session, Story: Chapter), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("its last stop must be between 288 and 488 light years from Sol. Move objective 1 to Velan's Rest; rewrite its lines for that place.", outcome.Refusal);
    }

    [Fact]
    public async Task ABandRefusalOfADockMovesItToAnInBandStation()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Docking), Saying(Repairs.Replacing(Docking, 1)), Saying(Repairs.Replacing(Docking, 1)));

        var outcome = await Generator(provider, new Galaxy { FromSol = 550 }, 588)
            .GenerateAsync(new AdventureAsk(Reach: AdventureReach.Session, Story: Chapter), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("Move objective 1 to Harbour Point in Velan's Rest; rewrite its lines for that place.", outcome.Refusal);
    }

    [Fact]
    public async Task ABandRefusalNamesNoPlaceBeyondTheReach()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats), Saying(Repairs.Replacing(Beats, 1)), Saying(Repairs.Replacing(Beats, 1)));

        var outcome = await Generator(provider, new Galaxy { FromSol = 550 }, 588)
            .GenerateAsync(new AdventureAsk(Reach: AdventureReach.NearHere, Story: Chapter), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("between 288 and 488 light years from Sol", outcome.Refusal);
        Assert.DoesNotContain("Move objective", outcome.Refusal);
    }

    [Theory]
    [InlineData("bubble")]
    [InlineData("black")]
    public async Task AChapterInTheBubbleOrInTheBlackMakesNoInBandSearch(string where)
    {
        var galaxy = new Galaxy();
        var ask = new AdventureAsk(Story: where == "black" ? Chapter with { InTheBlack = true } : Chapter);

        await Generator(new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats)), galaxy, where == "black" ? 588 : 300).GenerateAsync(ask, Now, CancellationToken.None);

        Assert.Empty(galaxy.Around);
    }

    private static AdventureGenerator Generator(ILlmProvider provider, Galaxy galaxy, double lightYears, TimeProvider? time = null)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-10-01T11:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """{ "timestamp":"2026-10-01T11:00:01Z", "event":"Rank", "Combat":1, "Trade":7, "Explore":5, "Soldier":0, "Exobiologist":0, "Empire":1, "Federation":1, "CQC":0 }""",
                     $$"""{ "timestamp":"2026-10-01T11:01:00Z", "event":"Location", "StarSystem":"Oppi", "SystemAddress":3382387380970, "StarPos":[{{lightYears}},0,0], "Docked":false }""",
                 })
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
            Time = time ?? TimeProvider.System,
        };
    }

    private sealed class Galaxy : IGalaxyService
    {
        public static readonly StationSummary HarbourPoint = new()
        {
            Name = "Harbour Point",
            SystemName = "Velan's Rest",
            MarketId = 9001,
            SystemAddress = 9002,
            Type = "Coriolis Starport",
            HasLargePad = true,
            Distance = 3,
            Position = new StarPosition(438, 0, 0),
        };

        private static readonly StationSummary LanternHub = new()
        {
            Name = "Lantern Hub",
            SystemName = "Ossen's Lantern",
            MarketId = 9003,
            SystemAddress = AdventureFixtures.Lantern,
            Type = "Coriolis Starport",
            HasLargePad = true,
        };

        public double FromSol { get; init; } = 400;

        public bool Down { get; init; }

        public IReadOnlyList<StationSummary> InBandStations { get; init; } = [HarbourPoint];

        public IReadOnlyList<BodySummary> InBandBodies { get; init; } =
        [
            new() { Name = "Velan's Rest 2 a", SystemName = "Velan's Rest", BodyId = 7, SystemAddress = 9002, IsLandable = true, Distance = 3, Position = new StarPosition(438, 0, 0) },
        ];

        /// <summary>The only radius a position-centred search finds anything at; null for every radius.</summary>
        public double? OnlyRadius { get; init; }

        public bool InBandDown { get; init; }

        /// <summary>The centre, rounded, and radius of each position-centred search, in order.</summary>
        public List<(StarPosition At, double Radius)> Around { get; } = [];

        private bool Answers(StarPosition at, double radius)
        {
            Around.Add((new StarPosition(Math.Round(at.X, 6), Math.Round(at.Y, 6), Math.Round(at.Z, 6)), radius));

            return InBandDown
                ? throw new GalaxyUnavailableException("the search is down")
                : OnlyRadius is null || OnlyRadius == radius;
        }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            var named = string.Equals(query.ReferenceSystem, "Ossen's Lantern", StringComparison.OrdinalIgnoreCase)
                ? new List<SystemSummary> { new() { Name = "Ossen's Lantern", SystemAddress = AdventureFixtures.Lantern, Government = "Democracy", Distance = 5 } }
                : [];

            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, named.Count, named));
        }

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Down && to == "Sol"
                ? throw new GalaxyUnavailableException("the service is down")
                : Task.FromResult<double?>(to == "Sol" ? FromSol : 12);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            IReadOnlyList<StationSummary> found = query.ReferencePosition is { } at
                ? Answers(at, query.MaxDistance) ? InBandStations : []
                : query is { StationName: "Lantern Hub" } ? [LanternHub] : [];

            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, found.Count, found));
        }

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            IReadOnlyList<BodySummary> found = query.ReferencePosition is { } at && Answers(at, query.MaxDistance) ? InBandBodies : [];

            return Task.FromResult(new BodySearchResult(query.ReferenceSystem, found.Count, found));
        }

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
