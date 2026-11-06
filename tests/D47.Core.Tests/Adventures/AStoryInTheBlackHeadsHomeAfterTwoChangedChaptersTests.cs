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

public sealed class AStoryInTheBlackHeadsHomeAfterTwoChangedChaptersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly StarPosition InTheBlack = new(3000, 0, 0);

    private static readonly AdventureStory Chapter = new("s", "S", "public", "hidden", 2, 10, 3, Slots: [PlannedChapterWorld.Bounty]) { Home = "Sol" };

    private static readonly AdventureAsk HomeAsk = new(Reach: AdventureReach.Anywhere, Story: Chapter);

    private static RoundScriptedLlmProvider Provider() =>
        new(Saying(PlannedChapterWorld.Lines(ChapterPlanner.MostObjectives)), Saying(PlannedChapterWorld.NoObjections));

    private static string Prompt(RoundScriptedLlmProvider provider) =>
        provider.Requests.First(request => request.ResponseSchema?.Name == "story_lines").Prompt.History[0].Text;

    [Fact]
    public void ACommander3000LightYearsFromSolAndColoniaHeadsForSol() => Assert.Equal("Sol", HomeLeg.TargetFrom(InTheBlack));

    [Fact]
    public void ACommanderNearerColoniaThanSolHeadsForColonia() =>
        Assert.Equal("Colonia", HomeLeg.TargetFrom(new StarPosition(-9530.5 + 3000, -910.28125, 19808.125)));

    [Fact]
    public void ACommanderWithin1000LightYearsOfColoniaHeadsForSol() =>
        Assert.Equal("Sol", HomeLeg.TargetFrom(new StarPosition(-9530.5 + 500, -910.28125, 19808.125)));

    [Fact]
    public void ACommanderWithin1000LightYearsOfSolHasNoTarget() => Assert.Null(HomeLeg.TargetFrom(new StarPosition(800, 0, 0)));

    [Fact]
    public void TwoChangedChaptersInARowSetTheTarget() =>
        Assert.Equal("Sol", HomeLeg.Next(null, InTheBlack, ["a", "b", "c"], ["b", "c"], excluded: false));

    [Fact]
    public void OneChangedChapterSetsNoTarget() =>
        Assert.Null(HomeLeg.Next(null, InTheBlack, ["a", "b", "c"], ["c"], excluded: false));

    [Fact]
    public void TwoChangedChaptersWithAnUnchangedOneBetweenSetNoTarget() =>
        Assert.Null(HomeLeg.Next(null, InTheBlack, ["a", "b", "c"], ["a", "c"], excluded: false));

    [Fact]
    public void TwoChangedChaptersWithinTheBubbleSetNoTarget() =>
        Assert.Null(HomeLeg.Next(null, new StarPosition(800, 0, 0), ["a", "b"], ["a", "b"], excluded: false));

    [Fact]
    public void AFinaleOrBeaconChapterSetsNoTarget() =>
        Assert.Null(HomeLeg.Next(null, InTheBlack, ["a", "b"], ["a", "b"], excluded: true));

    [Fact]
    public void TheTargetIsKeptWhileFarAndClearedWithin1000LightYearsOfIt()
    {
        Assert.Equal("Sol", HomeLeg.Next("Sol", InTheBlack, ["a"], [], excluded: false));
        Assert.Null(HomeLeg.Next("Sol", new StarPosition(900, 0, 0), ["a"], [], excluded: false));
    }

    [Fact]
    public async Task TheChapterHeadingHomeIsToldHowFarItIsFromSol()
    {
        var provider = Provider();

        var outcome = await Generator(provider, new Galaxy(), 3000).GenerateAsync(HomeAsk, Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var brief = Prompt(provider);
        Assert.Contains("3000 light years from Sol", brief);
        Assert.Contains("the story heads home to Sol", brief);
        Assert.DoesNotContain("outside the bubble", brief);
    }

    [Fact]
    public async Task AFinaleChapterIsNotSentHome()
    {
        var provider = Provider();

        var outcome = await Generator(provider, new Galaxy(), 3000).GenerateAsync(new AdventureAsk(Story: Chapter with { FinaleChapter = 2 }), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.DoesNotContain("heads home", Prompt(provider));
    }

    private static AdventureGenerator Generator(ILlmProvider provider, Galaxy galaxy, double lightYears)
    {
        galaxy.At = lightYears;

        return AdventureGeneratorTests.Generator(provider, galaxy, Now, $"{lightYears},0,0");
    }

    internal sealed class Galaxy : StubGalaxyService
    {
        public double FromSol { get; init; } = 1400;

        /// <summary>Where the Commander is along the x axis; stations to stop at lie toward Sol from it.</summary>
        public double At { get; set; }

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            var named = string.Equals(query.ReferenceSystem, "Ossen's Lantern", StringComparison.OrdinalIgnoreCase)
                ? new List<SystemSummary> { new() { Name = "Ossen's Lantern", SystemAddress = AdventureFixtures.Lantern, Government = "Democracy", Distance = 5 } }
                : [];

            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, named.Count, named));
        }

        public override Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(to == "Sol" ? FromSol : 12);

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            IReadOnlyList<StationSummary> found = query.ReferencePosition is { } waypoint
                ? [WaypointStation with { Position = waypoint }]
                : [.. Enumerable.Range(1, 6).Select(index => new StationSummary
                {
                    Name = $"Stop Port {index}", SystemName = $"Stop {index}", SystemAddress = 6000 + index, MarketId = 6100 + index,
                    Position = new StarPosition(At - (index * 10), 0, 0), Type = "Coriolis Starport", LargePads = 2,
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
