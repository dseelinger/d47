using System.Globalization;
using D47.Core.Adventures;
using D47.Core.Checklists;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

public sealed class AChangedObjectiveHeadsForANamedSystemTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private const string HomeName = "Tavell's Reach";

    private static readonly StarPosition Here = new(100, 0, 0);

    private static readonly StarPosition HomeAt = new(1100, 0, 0);

    private const string GoHome = """{"home": true, "system": null}""";

    private const string NoPlace = """{"home": false, "system": null}""";

    private const string NothingObjected = """{"objections": []}""";

    private static string Beats(bool heading) => $$"""
        {"opening": "x", "reply": "Home it is.", "heading": {{(heading ? "true" : "false")}}, "beats": [
          {"title": "The Way Back", "function": "midpoint", "kind": "arrive", "reason": "A courier there flies the home run.", "system": "Waypoint", "station": null, "body": null, "career": null, "rank": null, "line": "Halfway."},
          {"title": "The Turn", "function": "all is lost", "kind": "rank", "system": null, "station": null, "body": null, "career": "Trader", "rank": 8, "line": "Turn."},
          {"title": "The End", "function": "finale", "kind": "rank", "system": null, "station": null, "body": null, "career": "Trader", "rank": 8, "line": "Done."}
        ]}
        """;

    private static AdventureAsk Changing(string remark, AdventureStory? story = null) =>
        new(Length: story is null ? AdventureLength.Short : AdventureLength.Evening, Story: story, Rewrite: new AdventureRewrite(AdventureFixtures.LanternRoute(), 2, Remark: remark));

    [Fact]
    public void AHeadingToANamedSystemArrivesOnlyInThatSystem()
    {
        var plain = Changing("Take me to Tavell's Reach");
        var ask = plain with { Rewrite = plain.Rewrite! with { Heading = new AdventureHeading(HomeName, AdventureFixtures.Home, HomeAt) } };

        var far = ChapterPull.Of(ask, Here)!;
        var near = ChapterPull.Of(ask, new StarPosition(1099, 0, 0))!;

        Assert.True(far.Asked);
        Assert.Equal(0, far.Within);
        Assert.False(far.Arrived);
        Assert.False(near.Arrived);
        Assert.True(ChapterPull.Of(ask, HomeAt)!.Arrived);
    }

    [Fact]
    public async Task GoingHomeFromFarAwayNamesTheHomeSystemAndListsPlacesOnTheWay()
    {
        var provider = new RoundScriptedLlmProvider(Saying(GoHome), Saying(Beats(heading: true)), Saying(NothingObjected));
        var galaxy = new Galaxy();

        await Generator(provider, galaxy, Here, home: HomeName).GenerateAsync(Changing("I want to go home"), Now, CancellationToken.None);

        var step = provider.Requests[0].Prompt.History[0].Text;
        Assert.Equal("story_heading", provider.Requests[0].ResponseSchema?.Name);
        Assert.Contains($"- Home: {HomeName}.", step);
        Assert.Contains("The Commander now says: \"I want to go home\"", step);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains($"The Commander has asked to go to {HomeName}, 1000 light years away.", brief);
        Assert.Contains($"at least 60 light years nearer {HomeName}", brief);
        Assert.Contains($"or in {HomeName} itself", brief);
        Assert.Contains("Halfway House", brief);
        Assert.Contains("\"heading\": boolean", brief);
        Assert.Contains("\"heading\"", provider.Requests[1].ResponseSchema!.SchemaJson);
        Assert.Equal(1, galaxy.PositionedSearches);
    }

    [Fact]
    public async Task AHeadingAnswerWhoseLastStopIsNotNearerEnoughIsRefusedThroughRepair()
    {
        var provider = new RoundScriptedLlmProvider(
            Saying(GoHome), Saying(Beats(heading: true)), Saying(Repairs.ReplacingAfter(2, Beats(heading: true), 1)), Saying(Repairs.ReplacingAfter(2, Beats(heading: true), 1)));

        var outcome = await Generator(provider, new Galaxy { FromTarget = 990 }, Here, home: HomeName).GenerateAsync(Changing("I want to go home"), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("its last stop is 990 light years from it", outcome.Refusal);
        Assert.Contains($"the new objectives head for {HomeName}", outcome.Refusal);
        Assert.Contains($"or in {HomeName} itself", outcome.Refusal);
        Assert.Contains("its last stop is 990 light years from it", provider.Requests[2].Prompt.History[0].Text);
        Assert.Null(outcome.Heading);
    }

    [Fact]
    public async Task AHeadingAnswerThatMeetsTheRuleStandsAndReportsTheDestination()
    {
        var provider = new RoundScriptedLlmProvider(Saying(GoHome), Saying(Beats(heading: true)), Saying(NothingObjected));

        var outcome = await Generator(provider, new Galaxy { FromTarget = 900 }, Here, home: HomeName).GenerateAsync(Changing("I want to go home"), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(new AdventureHeading(HomeName, AdventureFixtures.Home, HomeAt), outcome.Heading);
    }

    [Fact]
    public async Task AnAnswerThatDoesNotHeadThereIsHeldToNoLegAndReportsNoDestination()
    {
        var provider = new RoundScriptedLlmProvider(Saying(GoHome), Saying(Beats(heading: false)), Saying(NothingObjected));

        var outcome = await Generator(provider, new Galaxy { FromTarget = 1000 }, Here, home: HomeName).GenerateAsync(Changing("I want to go home"), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Null(outcome.Heading);
    }

    [Fact]
    public async Task ASystemTheGalaxySearchDoesNotKnowIsSaidAndNothingTravels()
    {
        var provider = new RoundScriptedLlmProvider(Saying("""{"home": false, "system": "Nowhere Real"}"""), Saying(Beats(heading: false)), Saying(NothingObjected));
        var galaxy = new Galaxy();

        var outcome = await Generator(provider, galaxy, Here, home: HomeName).GenerateAsync(Changing("Take me to Nowhere Real"), Now, CancellationToken.None);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("The Commander asked to go to \"Nowhere Real\", which the galaxy search does not know as a star system", brief);
        Assert.DoesNotContain("\"heading\": boolean", brief);
        Assert.Equal(0, galaxy.PositionedSearches);
        Assert.Null(outcome.Heading);
    }

    [Fact]
    public async Task ARemarkThatNamesNoPlaceRewritesAsBefore()
    {
        var provider = new RoundScriptedLlmProvider(Saying(NoPlace), Saying(Beats(heading: false)), Saying(NothingObjected));
        var galaxy = new Galaxy();

        var outcome = await Generator(provider, galaxy, Here, home: HomeName).GenerateAsync(Changing("Less combat, please"), Now, CancellationToken.None);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.DoesNotContain("has asked to go", brief);
        Assert.DoesNotContain("\"heading\"", brief);
        Assert.Equal(0, galaxy.PositionedSearches);
        Assert.Null(outcome.Heading);
    }

    [Fact]
    public async Task WithNoHomeSystemSetHomeIsTheNearerOfSolAndColonia()
    {
        var at = TowardColonia(2000);
        var provider = new RoundScriptedLlmProvider(Saying(GoHome), Saying(Beats(heading: true)), Saying(NothingObjected));

        await Generator(provider, new Galaxy(), at, home: null).GenerateAsync(Changing("I want to go home"), Now, CancellationToken.None);

        Assert.Contains("- Home: they have set no home system.", provider.Requests[0].Prompt.History[0].Text);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("has set no home system, so home is Colonia, the nearer of Sol and Colonia, 2000 light years away", brief);
        Assert.Contains("or within 1000 light years of Colonia", brief);
    }

    public static TheoryData<string, string> BoundChapters => new()
    {
        { "beacon", "this chapter must end at the Guardian beacon in Synuefe XO-P c22-17" },
        { "finale", "this is a finale chapter" },
        { "toward", "this part of the story travels toward Colonia, in the black" },
    };

    [Theory]
    [MemberData(nameof(BoundChapters))]
    public async Task AChapterBoundElsewhereSearchesNothingForTheRemarkAndSaysWhatItMustDo(string kind, string said)
    {
        var story = new AdventureStory("s", "S", "public", "hidden", 2, 10, 3);
        story = kind switch
        {
            "beacon" => story with { Beacon = new AdventureBeacon(123, "Synuefe XO-P c22-17") },
            "finale" => story with { FinaleChapter = 2 },
            _ => story with { InTheBlack = true, Toward = "Colonia" },
        };

        var provider = new RoundScriptedLlmProvider(Saying(Beats(heading: false)), Saying(NothingObjected));
        var galaxy = new Galaxy();

        await Generator(provider, galaxy, Here, home: HomeName).GenerateAsync(Changing("Take me home", story), Now, CancellationToken.None);

        Assert.Equal("story_beats", provider.Requests[0].ResponseSchema?.Name);
        Assert.Contains($"If the Commander asks to go somewhere, the new objectives cannot take them there: {said}", provider.Requests[0].Prompt.History[0].Text);
        Assert.Equal(0, galaxy.PositionedSearches);
    }

    private static StarPosition TowardColonia(double lightYears)
    {
        var colonia = ExpeditionKit.Colonia;
        var scale = lightYears / colonia.DistanceTo(StarPosition.Origin);

        return new StarPosition(colonia.X * (1 - scale), colonia.Y * (1 - scale), colonia.Z * (1 - scale));
    }

    private static AdventureGenerator Generator(ILlmProvider provider, Galaxy galaxy, StarPosition at, string? home)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-10-29T11:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """{ "timestamp":"2026-10-29T11:00:01Z", "event":"Rank", "Combat":1, "Trade":7, "Explore":5, "Soldier":0, "Exobiologist":0, "Empire":1, "Federation":1, "CQC":0 }""",
                     $$"""{ "timestamp":"2026-10-29T11:01:00Z", "event":"Location", "StarSystem":"Oppi", "SystemAddress":3382387380970, "StarPos":[{{Coordinate(at.X)}},{{Coordinate(at.Y)}},{{Coordinate(at.Z)}}], "Docked":false }""",
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
            HomeSystem = () => home,
        };
    }

    private static string Coordinate(double value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed class Galaxy : IGalaxyService
    {
        /// <summary>Light years from any stop to the heading's system.</summary>
        public double FromTarget { get; init; } = 900;

        /// <summary>Station searches made around a point rather than a system: the places on the way to a target.</summary>
        public int PositionedSearches { get; private set; }

        private static readonly Dictionary<string, (long Address, StarPosition Position)> Known = new(StringComparer.OrdinalIgnoreCase)
        {
            [HomeName] = (AdventureFixtures.Home, HomeAt),
            ["Colonia"] = (3238296097059, ExpeditionKit.Colonia),
            ["Waypoint"] = (AdventureFixtures.Lantern, new StarPosition(160, 0, 0)),
        };

        private static readonly StationSummary WaypointStation = new()
        {
            Name = "Halfway House", SystemName = "Waypoint", MarketId = 7, Type = "Coriolis Starport", LargePads = 2, Position = new StarPosition(160, 0, 0),
        };

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            var named = query.ReferenceSystem is { } name && Known.TryGetValue(name, out var known)
                ? new List<SystemSummary> { new() { Name = Known.Keys.First(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase)), SystemAddress = known.Address, Government = "Democracy", Distance = 0 } }
                : [];

            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, named.Count, named));
        }

        public Task<StarPosition?> PositionAsync(string system, CancellationToken cancellationToken) =>
            Task.FromResult<StarPosition?>(Known.TryGetValue(system, out var known) ? known.Position : null);

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(to is HomeName or "Colonia" ? FromTarget : 12);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            if (query.ReferencePosition is null)
            {
                return Task.FromResult(new StationSearchResult(query.ReferenceSystem, 0, []));
            }

            PositionedSearches++;
            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, 1, [WaypointStation]));
        }

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
