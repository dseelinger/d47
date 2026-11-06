using System.Globalization;
using D47.Core.Adventures;
using D47.Core.Checklists;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Knowledge;
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

    private const string GoHome = """{"home": true, "system": null, "activity": null, "count": null, "reply": "Home it is."}""";

    private const string NoPlace = """{"home": false, "system": null, "activity": null, "count": null, "reply": "Name a place or something to do."}""";

    private const string NothingObjected = """{"objections": []}""";

    private static string GoTo(string system) => $$"""{"home": false, "system": "{{system}}", "activity": null, "count": null, "reply": "Off we go."}""";

    private static string Hunt(string count) => $$"""{"home": false, "system": null, "activity": "bounty", "count": {{count}}, "reply": "Bounties it is."}""";

    /// <summary>The new objectives' words, as a short story's plain lines.</summary>
    private static readonly string Words = Stories.PlannedChapterWorld.NewWords(speakers: false);

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
    public async Task GoingHomeFromFarAwayEndsTheNewObjectivesInTheHomeSystemInOneStep()
    {
        var provider = new RoundScriptedLlmProvider(Saying(GoHome), Saying(Words), Saying(NothingObjected));

        var outcome = await Generator(provider, new Galaxy(), Here, home: HomeName).GenerateAsync(Changing("I want to go home"), Now, CancellationToken.None);

        var step = provider.Requests[0].Prompt.History[0].Text;
        Assert.Equal("story_heading", provider.Requests[0].ResponseSchema?.Name);
        Assert.Contains($"- Home: {HomeName}.", step);
        Assert.Contains("- bounty: collect bounties", step);
        Assert.Contains("The Commander now says: \"I want to go home\"", step);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(["story_heading", "story_line_repairs", "story_fidelity", "story_plain"], provider.Requests.Select(request => request.ResponseSchema?.Name));
        Assert.Contains($"They asked to go to {HomeName}, and the last new objective arrives there.", provider.Requests[1].Prompt.History[0].Text);

        var last = outcome.Draft!.Beats[^1];
        Assert.Equal(TriggerKind.Arrive, Assert.Single(last.Steps).Trigger.Kind);
        Assert.Equal(HomeName, last.Trigger.System);
        Assert.Equal("Home it is.", outcome.Reply);
    }

    [Fact]
    public async Task AHomeSystemTypedInLowerCaseIsFoundInTheGameSpelling()
    {
        var provider = new RoundScriptedLlmProvider(Saying(GoHome), Saying(Words), Saying(NothingObjected));
        var generator = Generator(provider, new Galaxy { ExactCaseOnly = true }, Here, home: "tavell's reach");
        generator.Systems = () => new Names();

        var outcome = await generator.GenerateAsync(Changing("I want to go home"), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(HomeName, outcome.Draft!.Beats[^1].Trigger.System);
        Assert.Equal(new AdventureHeading(HomeName, AdventureFixtures.Home, HomeAt) { AskedHome = true }, outcome.Heading);
    }

    [Fact]
    public async Task TheAskedSystemIsArrivedInHoweverLongTheLastHop()
    {
        var provider = new RoundScriptedLlmProvider(Saying(GoTo(HomeName)), Saying(Words), Saying(NothingObjected));

        var outcome = await Generator(provider, new Galaxy(), Here, home: null).GenerateAsync(Changing("Take me to Tavell's Reach"), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(new AdventureHeading(HomeName, AdventureFixtures.Home, HomeAt), outcome.Heading);
        Assert.Equal(HomeName, outcome.Draft!.Beats[^1].Trigger.System);
        Assert.True(Here.DistanceTo(HomeAt) > 100);
    }

    [Fact]
    public async Task ASystemTheGalaxySearchDoesNotKnowIsSaidAndNothingIsPlanned()
    {
        var provider = new RoundScriptedLlmProvider(Saying(GoTo("Nowhere Real")));
        var galaxy = new Galaxy();

        var outcome = await Generator(provider, galaxy, Here, home: HomeName).GenerateAsync(Changing("Take me to Nowhere Real"), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Equal("I could not find Nowhere Real. Name a system the galaxy search knows, or an activity.", outcome.Refusal);
        Assert.Single(provider.Requests);
        Assert.Equal(0, galaxy.PositionedSearches);
        Assert.Null(outcome.Heading);
    }

    [Fact]
    public async Task ASystemTheGalaxySearchAnswersWith400IsSaidToBeUnknown()
    {
        var provider = new RoundScriptedLlmProvider(Saying(GoTo("Alfi")));

        var outcome = await Generator(provider, new Galaxy { ExactCaseOnly = true }, Here, home: HomeName).GenerateAsync(Changing("Take me to Alfi"), Now, CancellationToken.None);

        Assert.Equal("I could not find Alfi. Name a system the galaxy search knows, or an activity.", outcome.Refusal);
        Assert.Null(outcome.Heading);
    }

    [Fact]
    public async Task AGalaxySearchThatIsDownIsNotSaidToKnowNoSuchSystem()
    {
        var provider = new RoundScriptedLlmProvider(Saying(GoTo("Waypoint")));

        var outcome = await Generator(provider, new Galaxy { Down = true }, Here, home: HomeName).GenerateAsync(Changing("Take me to Waypoint"), Now, CancellationToken.None);

        Assert.DoesNotContain("could not find", outcome.Refusal);
        Assert.Contains(outcome.Notes, note => note.Contains("could not look up Waypoint", StringComparison.Ordinal));
        Assert.Null(outcome.Heading);
    }

    [Fact]
    public async Task ARemarkThatNamesNoPlaceAndNoActivityPlansNothingAndSaysTheReply()
    {
        var provider = new RoundScriptedLlmProvider(Saying(NoPlace));
        var galaxy = new Galaxy();

        var outcome = await Generator(provider, galaxy, Here, home: HomeName).GenerateAsync(Changing("Less combat, please"), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Equal("Name a place or something to do.", outcome.Refusal);
        Assert.Single(provider.Requests);
        Assert.Equal(0, galaxy.PositionedSearches);
    }

    [Fact]
    public async Task AnActivityWithTheCountTheCommanderSaidIsPlannedWithThatCount()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Hunt("12")), Saying(Words), Saying(NothingObjected));

        var outcome = await Generator(provider, new Galaxy(), Here, home: HomeName).GenerateAsync(Changing("Let me hunt twelve bounties"), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Null(outcome.Heading);
        Assert.Contains(outcome.Draft!.Beats.Skip(2), beat => beat.Trigger is { Kind: TriggerKind.Bounty, Count: 12 });
    }

    [Fact]
    public async Task AnActivityWithNoCountSaidTakesACountFromItsRow()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Hunt("null")), Saying(Words), Saying(NothingObjected));
        var row = ChapterActivities.Find(TriggerKind.Bounty, null, null)!;

        var outcome = await Generator(provider, new Galaxy(), Here, home: HomeName).GenerateAsync(Changing("Let me hunt bounties"), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var count = Assert.Single(outcome.Draft!.Beats.Skip(2), beat => beat.Trigger.Kind == TriggerKind.Bounty).Trigger.Count!.Value;
        Assert.InRange(count, row.Min, row.Max);
    }

    [Fact]
    public async Task WithNoHomeSystemSetHomeIsTheNearerOfSolAndColonia()
    {
        var at = TowardColonia(2000);
        var provider = new RoundScriptedLlmProvider(Saying(GoHome), Saying(Words), Saying(NothingObjected));
        var galaxy = new Galaxy();

        await Generator(provider, galaxy, at, home: null).GenerateAsync(Changing("I want to go home"), Now, CancellationToken.None);

        Assert.Contains("- Home: they have set no home system.", provider.Requests[0].Prompt.History[0].Text);
        Assert.True(galaxy.PositionedSearches > 0);
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

        var provider = new RoundScriptedLlmProvider(Saying(NoPlace));
        var galaxy = new Galaxy();

        await Generator(provider, galaxy, Here, home: HomeName).GenerateAsync(Changing("Take me home", story), Now, CancellationToken.None);

        Assert.Equal("story_heading", provider.Requests[0].ResponseSchema?.Name);
        Assert.Contains("This chapter cannot travel somewhere new", provider.Requests[0].Prompt.History[0].Text);
        Assert.DoesNotContain("- Home:", provider.Requests[0].Prompt.History[0].Text);
        Assert.Equal(0, galaxy.PositionedSearches);
        Assert.NotEmpty(said);
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

    /// <summary>A system name search that matches names in any case, as the real one does.</summary>
    private sealed class Names : IStarSystemService
    {
        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>(
                string.Equals(typed, HomeName, StringComparison.OrdinalIgnoreCase) ? [new SystemNameMatch(HomeName, AdventureFixtures.Home, HomeAt)] : []);

        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Galaxy : StubGalaxyService, IGalaxyService
    {
        /// <summary>Refuses a reference system not spelt in its exact case, as the real galaxy search does.</summary>
        public bool ExactCaseOnly { get; init; }

        /// <summary>Fails every position lookup as an outage.</summary>
        public bool Down { get; init; }

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

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            if (ExactCaseOnly && query.ReferenceSystem is { } spelt && !Known.Keys.Contains(spelt, StringComparer.Ordinal))
            {
                throw new GalaxyUnavailableException("The galaxy search refused that request.");
            }

            var named = query.ReferenceSystem is { } name && Known.TryGetValue(name, out var known)
                ? new List<SystemSummary> { new() { Name = Known.Keys.First(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase)), SystemAddress = known.Address, Government = "Democracy", Distance = 0 } }
                : [];

            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, named.Count, named));
        }

        public Task<StarPosition?> PositionAsync(string system, CancellationToken cancellationToken) =>
            Down
                ? throw new GalaxyUnavailableException("The galaxy search did not answer.")
                : Task.FromResult<StarPosition?>(Known.TryGetValue(system, out var known) ? known.Position : null);

        public override Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(to is HomeName or "Colonia" ? FromTarget : 12);

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            if (query.ReferencePosition is null)
            {
                return Task.FromResult(new StationSearchResult(query.ReferenceSystem, 0, []));
            }

            PositionedSearches++;
            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, 1, [WaypointStation]));
        }

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, 0, []));
    }
}
