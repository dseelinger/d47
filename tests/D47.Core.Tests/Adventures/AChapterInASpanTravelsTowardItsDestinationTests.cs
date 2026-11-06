using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Checklists;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

public sealed class AChapterInASpanTravelsTowardItsDestinationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly StarPosition Colonia = ExpeditionKit.Colonia;

    private static readonly AdventureStory InASpan = new("s", "S", "public", "hidden", 2, 10, 3, Slots: [PlannedChapterWorld.Bounty]) { InTheBlack = true, Toward = "Colonia" };

    private static RoundScriptedLlmProvider Provider() =>
        new(Saying(PlannedChapterWorld.Lines(ChapterPlanner.MostObjectives)), Saying(PlannedChapterWorld.NoObjections));

    private static string Prompt(RoundScriptedLlmProvider provider) =>
        provider.Requests.First(request => request.ResponseSchema?.Name == "story_lines").Prompt.History[0].Text;

    [Fact]
    public void TheWaypointIs85PercentOfTheReachAlongTheLine()
    {
        var waypoint = ChapterPull.Waypoint(new StarPosition(3000, 0, 0), StarPosition.Origin, 1500);

        Assert.Equal(1725, waypoint.X, 6);
        Assert.Equal(0, waypoint.Y, 6);
    }

    [Fact]
    public void TheWaypointIsTheTargetWhenTheTargetIsNearer() =>
        Assert.Equal(StarPosition.Origin, ChapterPull.Waypoint(new StarPosition(1000, 0, 0), StarPosition.Origin, 1500));

    [Fact]
    public void ALastStopMeetsTheRuleAtThreeQuartersOfTheReachNearerOrWithinTheArrivalRange()
    {
        Assert.True(ChapterPull.Meets(3000, 1875, 1500, 1000));
        Assert.False(ChapterPull.Meets(3000, 1876, 1500, 1000));
        Assert.True(ChapterPull.Meets(3000, 1000, 1500, 1000));
        Assert.True(ChapterPull.Meets(1200, 1000, 1500, 1000));
    }

    [Fact]
    public void ASpanWithADestinationPullsAChapterFarFromItAndNotOneAtIt()
    {
        var ask = new AdventureAsk(Story: InASpan);

        Assert.False(ChapterPull.Of(ask, Out(2000))!.Arrived);
        Assert.True(ChapterPull.Of(ask, Out(800))!.Arrived);
    }

    [Fact]
    public void ASpanWithNoDestinationPullsNothing() =>
        Assert.Null(ChapterPull.Of(new AdventureAsk(Story: InASpan with { Toward = null }), Out(2000)));

    [Fact]
    public async Task AChapterIsToldHowFarItIsFromWhereItGoes()
    {
        var provider = Provider();

        var outcome = await Generator(provider, new Galaxy(), Out(2000)).GenerateAsync(new AdventureAsk(Reach: AdventureReach.Anywhere, Story: InASpan), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var brief = Prompt(provider);
        Assert.Contains("2000 light years from Colonia", brief);
        Assert.DoesNotContain("may be far from the bubble", brief);
    }

    [Fact]
    public async Task WhenTheWaypointSearchFindsNothingTheChapterIsNotRefusedAndTheLogSaysWhy()
    {
        var provider = Provider();

        var logger = new RecordingLogger();
        var outcome = await Generator(provider, new Galaxy { FromColonia = 1900, NoPlaces = true }, Out(2000), logger).GenerateAsync(new AdventureAsk(Reach: AdventureReach.Anywhere, Story: InASpan), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains(logger.Lines, line => line.Contains("No station or landable body was found near the point on the way to Colonia", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AChapterThatHasArrivedIsToldSoAndIsNotRefused()
    {
        var provider = Provider();

        var outcome = await Generator(provider, new Galaxy { FromColonia = 900 }, Out(800)).GenerateAsync(new AdventureAsk(Reach: AdventureReach.Anywhere, Story: InASpan), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains("has arrived at Colonia", Prompt(provider));
    }

    [Fact]
    public async Task ASpanWithNoDestinationIsToldItMayBeFarFromTheBubbleAndNotRefused()
    {
        var provider = Provider();

        var outcome = await Generator(provider, new Galaxy { FromColonia = 1900 }, Out(2000))
            .GenerateAsync(new AdventureAsk(Reach: AdventureReach.Anywhere, Story: InASpan with { Toward = null }), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains("may be far from the bubble", Prompt(provider));
    }

    private static StarPosition Out(double lightYears)
    {
        var scale = lightYears / Colonia.DistanceTo(StarPosition.Origin);

        return new StarPosition(Colonia.X * (1 - scale), Colonia.Y * (1 - scale), Colonia.Z * (1 - scale));
    }

    private static AdventureGenerator Generator(ILlmProvider provider, Galaxy galaxy, StarPosition at, ILogger? logger = null)
    {
        galaxy.Here = at;
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-10-01T11:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """{ "timestamp":"2026-10-01T11:00:01Z", "event":"Rank", "Combat":1, "Trade":7, "Explore":5, "Soldier":0, "Exobiologist":0, "Empire":1, "Federation":1, "CQC":0 }""",
                     $$"""{ "timestamp":"2026-10-01T11:01:00Z", "event":"Location", "StarSystem":"Oppi", "SystemAddress":3382387380970, "StarPos":[{{at.X.ToString(System.Globalization.CultureInfo.InvariantCulture)}},{{at.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}},{{at.Z.ToString(System.Globalization.CultureInfo.InvariantCulture)}}], "Docked":false }""",
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
            logger ?? NullLogger.Instance);
    }

    private sealed class Galaxy : StubGalaxyService
    {
        public double FromColonia { get; init; } = 1400;

        public bool NoPlaces { get; init; }

        /// <summary>Where the Commander is; stations to stop at lie around it.</summary>
        public StarPosition Here { get; set; }

        private static readonly StationSummary WaypointStation = new()
        {
            Name = "Halfway House", SystemName = "Waypoint", MarketId = 7, Type = "Coriolis Starport", LargePads = 2, Position = new StarPosition(-1000, -100, 2000),
        };

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            var named = string.Equals(query.ReferenceSystem, "Ossen's Lantern", StringComparison.OrdinalIgnoreCase)
                ? new List<SystemSummary> { new() { Name = "Ossen's Lantern", SystemAddress = AdventureFixtures.Lantern, Government = "Democracy", Distance = 5 } }
                : [];

            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, named.Count, named));
        }

        public override Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(to == "Colonia" ? FromColonia : 12);

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            IReadOnlyList<StationSummary> found = query.ReferencePosition is null
                ? [.. Enumerable.Range(1, 6).Select(index => new StationSummary
                {
                    Name = $"Stop Port {index}", SystemName = $"Stop {index}", SystemAddress = 6000 + index, MarketId = 6100 + index,
                    Position = new StarPosition(Here.X + (index * 10), Here.Y, Here.Z), Type = "Coriolis Starport", LargePads = 2,
                })]
                : NoPlaces ? [] : [WaypointStation with { Position = query.ReferencePosition }];

            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, found.Count, found));
        }

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, 0, []));
    }
}
