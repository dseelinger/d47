using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
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

    private static readonly AdventureStory Chapter = new("s", "S", "public", "hidden", 4, 10, 3) { Chosen = Chosen };

    private static readonly AdventureAsk ChosenAsk = new(Reach: AdventureReach.Anywhere, Story: Chapter);

    private const string Spine = """
        {"name": "The Long Way", "premise": "A way out.", "want": "To get there.",
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
    public async Task TheBeatsPromptSaysTheCommanderAskedToHeadForItAndListsItsPlaces()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        await Generator(provider, new Galaxy(), Here).GenerateAsync(ChosenAsk, Now, CancellationToken.None);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains($"The Commander asked to head for {Target}, 5000 light years away, and has not got there.", brief);
        Assert.Contains($"light years nearer {Target}", brief);
        Assert.Contains("Halfway House", brief);
        Assert.DoesNotContain("turned down", brief);
        Assert.DoesNotContain("outside the bubble", brief);
    }

    [Fact]
    public async Task ALastStopThreeQuartersOfAReachNearerPasses()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        var outcome = await Generator(provider, new Galaxy { FromTarget = 3500 }, Here).GenerateAsync(ChosenAsk, Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    [Fact]
    public async Task ALastStopShortOfThatIsRefusedWithTheCommandersRequest()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats), Saying(Repairs.Replacing(Beats, 1)), Saying(Repairs.Replacing(Beats, 1)));

        var outcome = await Generator(provider, new Galaxy { FromTarget = 4900 }, Here).GenerateAsync(ChosenAsk, Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains($"the Commander asked to head for {Target}", outcome.Refusal);
    }

    private static AdventureGenerator Generator(ILlmProvider provider, Galaxy galaxy, StarPosition at)
    {
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

    private sealed class Galaxy : IGalaxyService
    {
        public double FromTarget { get; init; } = 5000;

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            var named = string.Equals(query.ReferenceSystem, "Ossen's Lantern", StringComparison.OrdinalIgnoreCase)
                ? new List<SystemSummary> { new() { Name = "Ossen's Lantern", SystemAddress = AdventureFixtures.Lantern, Government = "Democracy", Distance = 5 } }
                : [];

            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, named.Count, named));
        }

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(to == Target ? FromTarget : 12);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult(query.ReferenceSystem, 1, [WaypointStation]));

        private static readonly StationSummary WaypointStation = new()
        {
            Name = "Halfway House", SystemName = "Waypoint", MarketId = 7, Type = "Coriolis Starport", LargePads = 2, Position = new StarPosition(1725, 0, 0),
        };

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
