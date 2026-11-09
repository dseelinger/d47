using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Knowledge;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>After the beacon scan, a story's chapter may be drawn as a sightseeing chapter, asked for with the one sight it may name; a sight beyond the session reach opens a span.</summary>
public sealed class AStoryDrawsSightseeingChaptersTests
{
    private sealed class Places(Func<IReadOnlyList<NotablePlace>> near) : INotablePlacesService
    {
        public Task<IReadOnlyList<NotablePlace>> NearAsync(StarPosition from, double radiusLightYears, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(near());
    }

    /// <summary>A galaxy with an open station around every point searched.</summary>
    private sealed class Waypoints : StubGalaxyService
    {
        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            IReadOnlyList<StationSummary> found = [new StationSummary { Name = "Port", SystemName = "Near", SystemAddress = 8, MarketId = 8_000, Distance = 5, HasLargePad = true, Type = "Coriolis Starport", Position = query.ReferencePosition ?? StarPosition.Origin }];

            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, found.Count, found));
        }

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, 0, []));
    }

    private static readonly StationSummary Outpost = new()
    {
        Name = "Outpost", SystemName = "Far Reach", SystemAddress = 9_000_001, MarketId = 9_100_001, Distance = 2592, HasLargePad = true, Type = "Coriolis Starport", Position = new StarPosition(2692, 0, 0),
    };

    private static NotablePlace Sight(string name, string system, double x) => new(name, "Nebulae", system, null, new StarPosition(x, 0, 0));

    private static bool Rolls(string id) => ChapterDraw.Sightsee(CareerWeights.From(null, CareerStatistics.Empty), false, ChapterDraw.SightSeed(id, 1));

    private static string StoryThat(bool sightsees) =>
        Enumerable.Range(0, 200).Select(index => $"sight-story-{index}").First(id => Rolls(id) == sightsees);

    private static StoryFixtures AfterTheScan(string id, double x, long credits = 300_000_000, bool scanned = true, IReadOnlyList<StationSummary>? stations = null)
    {
        var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(Lines()), RoundScriptedLlmProvider.Saying(NothingObjected),
                RoundScriptedLlmProvider.Saying(NextLines), RoundScriptedLlmProvider.Saying(NothingObjected)),
            secret: Secret with { Id = id },
            card: Card with { Id = id, Length = StoryPacing.ThreeMonths.Key },
            stations: stations);

        if (scanned)
        {
            fixtures.Stories.Save("F1", new Story
            {
                Id = Other.Id,
                Title = Other.Title,
                PublicLayer = Other.Describe(),
                Length = StoryPacing.OneYear.Key,
                State = StoryState.Finished,
                PickedAt = Now.AddDays(-200),
                BeaconScanAt = Now.AddDays(-199),
                BeaconSystems = [BeaconAddress],
                StoppedAt = Now.AddDays(-1),
            });
        }

        var state = Loaded(credits);

        state.Apply(AdventureFixtures.Event(
            $$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"FSDJump", "StarSystem":"Here", "SystemAddress":7, "StarPos":[{{x}},0,0] }"""));
        fixtures.Director.Game = () => state;
        return fixtures;
    }

    private static async Task<AdventureAsk> Asked(StoryFixtures fixtures, string id)
    {
        await fixtures.Director.PickAsync("F1", id, Now, CancellationToken.None);

        return fixtures.Asks[0];
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ADrawnChapterIsAskedForAnywhereWithOneOfItsSights()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 100);

        fixtures.Director.Places = () => new Places(() => [Sight("Veil", "Alpha", 300), Sight("Cave", "Beta", 400)]);

        var ask = await Asked(fixtures, id);

        Assert.Equal(AdventureReach.Anywhere, ask.Reach);
        Assert.True(ask.Story!.LongHaul);
        Assert.Single(ask.Story.Sights!);
        Assert.Contains(ask.Story.Sights![0].Name, new[] { "Veil", "Cave" });
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AChapterTheDrawLeavesOutIsNotSightseeing()
    {
        var id = StoryThat(sightsees: false);
        using var fixtures = AfterTheScan(id, 100);

        fixtures.Director.Places = () => new Places(() => [Sight("Veil", "Alpha", 300)]);

        Assert.Null((await Asked(fixtures, id)).Story!.Sights);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task WithNotablePlacesOffNoChapterIsSightseeing()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 100);

        fixtures.Director.Places = () => null;

        Assert.Null((await Asked(fixtures, id)).Story!.Sights);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AnUnreachableCatalogueLeavesTheChapterWrittenWithoutSights()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 100);

        fixtures.Director.Places = () => new Places(() => throw new GalaxyUnavailableException("The catalogue is down."));

        var ask = await Asked(fixtures, id);

        Assert.Null(ask.Story!.Sights);
        Assert.NotEmpty(fixtures.Stories.Current("F1")!.Chapters);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task OutsideTheBubbleOnlySightsInTheSolBandAreOffered()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 1000);

        fixtures.Director.Places = () => new Places(() => [Sight("Near", "Alpha", 100), Sight("InBand", "Beta", 800), Sight("Far", "Gamma", 950)]);

        Assert.Equal(["InBand"], (await Asked(fixtures, id)).Story!.Sights!.Select(sight => sight.Name));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AStoryThatIsNotLongHaulSightsees()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 100, credits: 1_000);

        fixtures.Director.Places = () => new Places(() => [Sight("Veil", "Alpha", 300)]);

        var ask = await Asked(fixtures, id);

        Assert.Equal(["Veil"], ask.Story!.Sights!.Select(sight => sight.Name));
        Assert.Equal(AdventureReach.Session, ask.Reach);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AChapterBeforeTheBeaconScanNeverSightsees()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 100, scanned: false);

        fixtures.Director.Places = () => new Places(() => [Sight("Veil", "Alpha", 300)]);

        Assert.Null((await Asked(fixtures, id)).Story!.Sights);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ASightWithinTheSessionReachOpensNoSpan()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 100);

        fixtures.Director.Places = () => new Places(() => [Sight("Veil", "Alpha", 300)]);

        await Asked(fixtures, id);

        Assert.Null(fixtures.Stories.Current("F1")!.DrawnSpan);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ASightUpToThirtyThreeJumpsAwayIsAnOrdinarySightseeingChapter()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 100);

        fixtures.Director.Places = () => new Places(() => [Sight("Veil", "Alpha", 1000)]);

        var ask = await Asked(fixtures, id);

        Assert.Equal(["Veil"], ask.Story!.Sights!.Select(sight => sight.Name));
        Assert.Null(fixtures.Stories.Current("F1")!.DrawnSpan);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ASightBeyondTheSessionReachOpensASightSpanWithoutNamingTheSightYet()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 100, stations: [Outpost]);

        fixtures.Director.Places = () => new Places(() => [Sight("Veil", "Alpha", 3000)]);
        fixtures.Director.Galaxy = () => new Waypoints();

        var ask = await Asked(fixtures, id);
        var span = fixtures.Stories.Current("F1")!.DrawnSpan!;

        Assert.Equal(DrawnSpanKind.Sight, span.Kind);
        Assert.Equal(DrawnSpanPhase.Out, span.Phase);
        Assert.Equal("Alpha", span.Target);
        Assert.Equal(new StarPosition(3000, 0, 0), span.TargetAt);
        Assert.Equal(new StarPosition(100, 0, 0), span.StartAt);
        Assert.Equal(1, span.Opened);
        Assert.Null(ask.Story!.Sights);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ASightOneSpanChapterAwayIsTheObjectiveOfTheChapterThatOpensTheSpan()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 100);

        fixtures.Director.Places = () => new Places(() => [Sight("Veil", "Alpha", 2000)]);
        fixtures.Director.Galaxy = () => new Waypoints();

        var ask = await Asked(fixtures, id);

        Assert.Equal(["Veil"], ask.Story!.Sights!.Select(sight => sight.Name));
        Assert.Equal(DrawnSpanKind.Sight, ask.Story.Span!.Kind);
        Assert.Equal(1, ask.Story.Span.Opened);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ASightBeyondTheSpanLengthIsNotOffered()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 100);

        fixtures.Director.Places = () => new Places(() => [Sight("Veil", "Alpha", 5000)]);

        var ask = await Asked(fixtures, id);

        Assert.Null(fixtures.Stories.Current("F1")!.DrawnSpan);
        Assert.Null(ask.Story!.Sights);
    }

    private static async Task<AdventureAsk> FirstChapterAsk(string id, DrawnSpan? span, double x, double sightAt = 3000)
    {
        using var fixtures = AfterTheScan(id, x);

        fixtures.Director.Places = () => new Places(() => [Sight("Veil", "Alpha", sightAt)]);
        fixtures.Director.Galaxy = () => new Waypoints();
        fixtures.Stories.Save("F1", new Story
        {
            Id = id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            Length = StoryPacing.ThreeMonths.Key,
            PickedAt = Now.AddDays(-30),
            BeaconScanAt = Now.AddDays(-20),
            DrawnSpan = span,
        });
        await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None);

        return fixtures.Asks[^1];
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task NoChapterRollsForASightWhileASpanIsOpen()
    {
        var id = StoryThat(sightsees: true);
        var open = new DrawnSpan(DrawnSpanKind.Exploration, null, null, new StarPosition(0, 0, 4000), 750, new StarPosition(100, 0, 0), DrawnSpanPhase.Out, 0, 0);

        Assert.NotNull((await FirstChapterAsk(id, null, 100, 300)).Story!.Sights);
        Assert.Null((await FirstChapterAsk(id, open, 100, 300)).Story!.Sights);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheChapterThatReachesASightSpansSightIsOfferedItWithoutARoll()
    {
        var toward = new DrawnSpan(DrawnSpanKind.Sight, "Alpha", null, new StarPosition(3000, 0, 0), DrawnSpans.SightArrival, new StarPosition(100, 0, 0), DrawnSpanPhase.Out, 0, 0);
        var ask = await FirstChapterAsk(StoryThat(sightsees: false), toward, 1500);

        Assert.Equal(["Veil"], ask.Story!.Sights!.Select(sight => sight.Name));
        Assert.Equal(DrawnSpanKind.Sight, ask.Story.Span!.Kind);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AChapterOutThatCannotReachTheSightTravelsWithoutIt()
    {
        var toward = new DrawnSpan(DrawnSpanKind.Sight, "Alpha", null, new StarPosition(3000, 0, 0), DrawnSpans.SightArrival, new StarPosition(100, 0, 0), DrawnSpanPhase.Out, 0, 0);

        Assert.Null((await FirstChapterAsk(StoryThat(sightsees: false), toward, 600)).Story!.Sights);
    }
}
