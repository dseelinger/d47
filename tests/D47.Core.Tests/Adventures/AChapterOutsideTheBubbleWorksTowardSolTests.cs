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
    public async Task ALastStopThatCannotBeMeasuredIsRefusedWithTheReason()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats), Saying(Repairs.Replacing(Beats, 1)), Saying(Repairs.Replacing(Beats, 1)));

        var outcome = await Generator(provider, new Galaxy { Down = true }, 588).GenerateAsync(new AdventureAsk(Story: Chapter), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("could not be measured against Sol: the service is down", outcome.Refusal);
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
        public double FromSol { get; init; } = 400;

        public bool Down { get; init; }

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
