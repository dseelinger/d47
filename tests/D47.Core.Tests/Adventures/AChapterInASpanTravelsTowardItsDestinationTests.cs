using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Checklists;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

public sealed class AChapterInASpanTravelsTowardItsDestinationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly StarPosition Colonia = ExpeditionKit.Colonia;

    private static readonly AdventureStory InASpan = new("s", "S", "public", "hidden", 2, 10, 3) { InTheBlack = true, Toward = "Colonia" };

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
    public async Task AChapterIsToldWhereItGoesAndOfferedAPlaceOnTheWay()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        await Generator(provider, new Galaxy(), Out(2000)).GenerateAsync(new AdventureAsk(Reach: AdventureReach.Anywhere, Story: InASpan), Now, CancellationToken.None);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("2000 light years from Colonia", brief);
        Assert.Contains("at least 1125 light years nearer Colonia", brief);
        Assert.Contains("Halfway House", brief);
        Assert.DoesNotContain("may be far from the bubble", brief);
    }

    [Fact]
    public async Task ALastStopFarEnoughTowardTheDestinationPasses()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        var outcome = await Generator(provider, new Galaxy { FromColonia = 875 }, Out(2000)).GenerateAsync(new AdventureAsk(Reach: AdventureReach.Anywhere, Story: InASpan), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    [Fact]
    public async Task ALastStopNotFarEnoughTowardTheDestinationIsRefused()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats), Saying(Repairs.Replacing(Beats, 1)), Saying(Repairs.Replacing(Beats, 1)));

        var outcome = await Generator(provider, new Galaxy { FromColonia = 1500 }, Out(2000)).GenerateAsync(new AdventureAsk(Reach: AdventureReach.Anywhere, Story: InASpan), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("its last stop is 1500 light years from it", outcome.Refusal);
        Assert.Contains("heading for Colonia", outcome.Refusal);
    }

    [Fact]
    public async Task WhenTheWaypointSearchFindsNothingTheChapterIsNotRefusedAndSaysWhy()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        var outcome = await Generator(provider, new Galaxy { FromColonia = 1900, NoPlaces = true }, Out(2000)).GenerateAsync(new AdventureAsk(Reach: AdventureReach.Anywhere, Story: InASpan), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains(outcome.Notes, note => note.Contains("No station or landable body was found near the point on the way to Colonia"));
        Assert.Contains("is not held to a distance from Colonia", provider.Requests[1].Prompt.History[0].Text);
    }

    [Fact]
    public async Task AChapterThatHasArrivedIsToldSoAndIsNotRefused()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        var outcome = await Generator(provider, new Galaxy { FromColonia = 900 }, Out(800)).GenerateAsync(new AdventureAsk(Reach: AdventureReach.Anywhere, Story: InASpan), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains("has arrived at Colonia", provider.Requests[1].Prompt.History[0].Text);
    }

    [Fact]
    public async Task ASpanWithNoDestinationIsToldItMayBeFarFromTheBubbleAndNotRefused()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats));

        var outcome = await Generator(provider, new Galaxy { FromColonia = 1900 }, Out(2000))
            .GenerateAsync(new AdventureAsk(Reach: AdventureReach.Anywhere, Story: InASpan with { Toward = null }), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains("may be far from the bubble", provider.Requests[1].Prompt.History[0].Text);
    }

    private static StarPosition Out(double lightYears)
    {
        var scale = lightYears / Colonia.DistanceTo(StarPosition.Origin);

        return new StarPosition(Colonia.X * (1 - scale), Colonia.Y * (1 - scale), Colonia.Z * (1 - scale));
    }

    private static AdventureGenerator Generator(ILlmProvider provider, Galaxy galaxy, StarPosition at)
    {
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
            NullLogger.Instance);
    }

    private sealed class Galaxy : IGalaxyService
    {
        public double FromColonia { get; init; } = 1400;

        public bool NoPlaces { get; init; }

        private static readonly StationSummary WaypointStation = new()
        {
            Name = "Halfway House", SystemName = "Waypoint", MarketId = 7, Type = "Coriolis Starport", LargePads = 2, Position = new StarPosition(-1000, -100, 2000),
        };

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            var named = string.Equals(query.ReferenceSystem, "Ossen's Lantern", StringComparison.OrdinalIgnoreCase)
                ? new List<SystemSummary> { new() { Name = "Ossen's Lantern", SystemAddress = AdventureFixtures.Lantern, Government = "Democracy", Distance = 5 } }
                : [];

            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, named.Count, named));
        }

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(to == "Colonia" ? FromColonia : 12);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult(query.ReferenceSystem, NoPlaces || query.ReferencePosition is null ? 0 : 1, NoPlaces || query.ReferencePosition is null ? [] : [WaypointStation]));

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
