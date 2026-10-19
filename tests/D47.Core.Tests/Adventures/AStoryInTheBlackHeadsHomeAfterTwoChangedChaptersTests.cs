using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

public sealed class AStoryInTheBlackHeadsHomeAfterTwoChangedChaptersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly StarPosition InTheBlack = new(3000, 0, 0);

    private static readonly AdventureStory Chapter = new("s", "S", "public", "hidden", 2, 10, 3) { Home = "Sol" };

    private static readonly AdventureAsk HomeAsk = new(Reach: AdventureReach.Anywhere, Story: Chapter);

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
    public async Task TheChapterHeadingHomeIsToldWhereItGoesAndHowFar()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        await Generator(provider, new Galaxy(), 3000).GenerateAsync(HomeAsk, Now, CancellationToken.None);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("3000 light years from Sol", brief);
        Assert.Contains("the story heads home to Sol", brief);
        Assert.Contains("at least 1500 light years nearer Sol", brief);
        Assert.DoesNotContain("outside the bubble", brief);
    }

    [Fact]
    public async Task ALastStopAReachNearerThanTheStartPasses()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        var outcome = await Generator(provider, new Galaxy { FromSol = 1500 }, 3000).GenerateAsync(HomeAsk, Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    [Fact]
    public async Task ALastStopLessThanAReachNearerIsRefusedWithTheTarget()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats), Saying(Repairs.Replacing(Beats, 1)), Saying(Repairs.Replacing(Beats, 1)));

        var outcome = await Generator(provider, new Galaxy { FromSol = 1600 }, 3000).GenerateAsync(HomeAsk, Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("its last stop is 1600 light years from it", outcome.Refusal);
        Assert.Contains("heading home", outcome.Refusal);
    }

    [Fact]
    public async Task ALastStopWithin1000LightYearsOfTheTargetPasses()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        var outcome = await Generator(provider, new Galaxy { FromSol = 900 }, 1900).GenerateAsync(HomeAsk, Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    [Fact]
    public async Task AFinaleChapterIsNotSentHome()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        await Generator(provider, new Galaxy(), 3000).GenerateAsync(new AdventureAsk(Story: Chapter with { FinaleChapter = 2 }), Now, CancellationToken.None);

        Assert.DoesNotContain("heads home", provider.Requests[1].Prompt.History[0].Text);
    }

    private static AdventureGenerator Generator(ILlmProvider provider, Galaxy galaxy, double lightYears)
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
            NullLogger.Instance);
    }

    private sealed class Galaxy : IGalaxyService
    {
        public double FromSol { get; init; } = 1400;

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            var named = string.Equals(query.ReferenceSystem, "Ossen's Lantern", StringComparison.OrdinalIgnoreCase)
                ? new List<SystemSummary> { new() { Name = "Ossen's Lantern", SystemAddress = AdventureFixtures.Lantern, Government = "Democracy", Distance = 5 } }
                : [];

            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, named.Count, named));
        }

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(to == "Sol" ? FromSol : 12);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult(query.ReferenceSystem, 0, []));

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, 0, []));

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
