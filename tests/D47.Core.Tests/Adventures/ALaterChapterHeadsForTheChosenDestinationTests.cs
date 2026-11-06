using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using D47.Core.Tests.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

public sealed class ALaterChapterHeadsForTheChosenDestinationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private const string Target = "Tau Ceti";

    private static readonly StarPosition Here = new(300, 0, 0);

    private static readonly StarPosition There = new(5300, 0, 0);

    private static readonly AdventureHeading Chosen = new(Target, 1234, There);

    private static readonly AdventureStory Chapter = new("s", "S", "public", "hidden", 4, 10, 3, Slots: [PlannedChapterWorld.Bounty]) { Chosen = Chosen };

    private static readonly AdventureAsk ChosenAsk = new(Reach: AdventureReach.Anywhere, Story: Chapter);

    [Fact]
    public void AChosenDestinationIsALegTheCommanderAskedFor()
    {
        var leg = ChapterPull.Of(ChosenAsk, Here)!;

        Assert.Equal(Target, leg.Target);
        Assert.True(leg.Chosen);
        Assert.False(leg.Asked);
        Assert.False(leg.Arrived);
    }

    [Fact]
    public void AChosenDestinationComesBeforeTheHomeLegAndTheBand()
    {
        var withHome = ChosenAsk with { Story = Chapter with { Home = "Sol" } };

        Assert.Equal(Target, ChapterPull.Of(withHome, Here)!.Target);
        Assert.False(ChapterBand.Applies(ChosenAsk));
        Assert.Null(ChapterBand.Of(new StarPosition(3000, 0, 0), ChosenAsk));
    }

    [Fact]
    public void ABeaconChapterAndAFinaleChapterHaveNoChosenLeg()
    {
        Assert.Null(ChapterPull.Of(ChosenAsk with { Story = Chapter with { Beacon = new AdventureBeacon(1, "B") } }, Here));
        Assert.Null(ChapterPull.Of(ChosenAsk with { Story = Chapter with { FinaleChapter = 2 } }, Here));
    }

    [Fact]
    public async Task TheChapterPromptSaysTheCommanderAskedToHeadForIt()
    {
        var provider = new RoundScriptedLlmProvider(Saying(PlannedChapterWorld.Lines(ChapterPlanner.MostObjectives)), Saying(PlannedChapterWorld.NoObjections));

        var outcome = await Generator(provider, new Galaxy(), Here).GenerateAsync(ChosenAsk, Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var brief = provider.Requests.First(request => request.ResponseSchema?.Name == "story_lines").Prompt.History[0].Text;
        Assert.Contains($"The Commander asked to head for {Target}, 5000 light years away, and has not got there.", brief);
        Assert.DoesNotContain("turned down", brief);
        Assert.DoesNotContain("outside the bubble", brief);
    }

    private static AdventureGenerator Generator(ILlmProvider provider, Galaxy galaxy, StarPosition at)
    {
        galaxy.At = at;
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-10-30T11:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """{ "timestamp":"2026-10-30T11:00:01Z", "event":"Rank", "Combat":1, "Trade":7, "Explore":5, "Soldier":0, "Exobiologist":0, "Empire":1, "Federation":1, "CQC":0 }""",
                     $$"""{ "timestamp":"2026-10-30T11:01:00Z", "event":"Location", "StarSystem":"Oppi", "SystemAddress":3382387380970, "StarPos":[{{at.X}},0,0], "Docked":false }""",
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
            NullLogger.Instance);
    }

    private sealed class Galaxy : StubGalaxyService
    {
        public double FromTarget { get; init; } = 5000;

        /// <summary>Where the Commander is; stations to stop at lie toward the target from it.</summary>
        public StarPosition At { get; set; }

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            var named = string.Equals(query.ReferenceSystem, "Ossen's Lantern", StringComparison.OrdinalIgnoreCase)
                ? new List<SystemSummary> { new() { Name = "Ossen's Lantern", SystemAddress = AdventureFixtures.Lantern, Government = "Democracy", Distance = 5 } }
                : [];

            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, named.Count, named));
        }

        public override Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(to == Target ? FromTarget : 12);

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            IReadOnlyList<StationSummary> found = query.ReferencePosition is { } waypoint
                ? [WaypointStation with { Position = waypoint }]
                : [.. Enumerable.Range(1, 6).Select(index => new StationSummary
                {
                    Name = $"Stop Port {index}", SystemName = $"Stop {index}", SystemAddress = 6000 + index, MarketId = 6100 + index,
                    Position = new StarPosition(At.X + (index * 10), 0, 0), Type = "Coriolis Starport", LargePads = 2,
                })];

            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, found.Count, found));
        }

        private static readonly StationSummary WaypointStation = new()
        {
            Name = "Halfway House", SystemName = "Waypoint", MarketId = 7, Type = "Coriolis Starport", LargePads = 2, Position = new StarPosition(1725, 0, 0),
        };

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, 0, []));
    }
}
