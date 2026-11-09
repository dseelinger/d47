using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Knowledge;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A span the draw opens puts its chapters in the black, survives a rewrite and a restart, and holds the finale until it closes (#2249).</summary>
public sealed class ADrawnSpanPutsChaptersInTheBlackUntilItClosesTests
{
    private static readonly StarPosition Bubble = new(0, 0, 100);

    private static readonly DrawnSpan Away = new(
        DrawnSpanKind.Exploration, null, null, new StarPosition(0, 0, 4675), 762.5, Bubble, DrawnSpanPhase.Out, 1, 0);

    [Fact]
    public async Task TheSecondChapterOfTheFixtureStoryOpensASpanAndIsWrittenInTheBlack()
    {
        using var fixtures = At(Bubble, new RoundScriptedLlmProvider(Saying(Lines()), Saying(NothingObjected), Saying(NextLines), Saying(NothingObjected)));

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None));
        var first = fixtures.Stories.Current("F1")!;
        Assert.Null(first.DrawnSpan);
        Assert.False(fixtures.Asks[^1].Story!.InTheBlack);

        fixtures.Finish("F1", first.CurrentChapter!, Now);
        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(3), CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;
        var span = story.DrawnSpan!;
        Assert.Equal(2, story.Chapters.Count);
        Assert.True(fixtures.Asks[^1].Story!.InTheBlack);
        Assert.Equal(2, span.Opened);
        Assert.Equal(Bubble, span.StartAt);
        Assert.Equal(DrawnSpanPhase.Out, span.Phase);
        Assert.Equal(DrawnSpans.ArrivalShare * 3050, span.Within, 6);
        Assert.True(DrawnSpans.Explores(fixtures.Asks[^1].Story!.Slots));
    }

    [Fact]
    public async Task TheChapterWrittenAsASpanClosesHasAQuarterOfTheChance()
    {
        using var fixtures = At(Bubble, new RoundScriptedLlmProvider(Saying(Lines()), Saying(NothingObjected), Saying(NextLines), Saying(NothingObjected)));

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None));
        var first = fixtures.Stories.Current("F1")!;
        fixtures.Finish("F1", first.CurrentChapter!, Now);
        fixtures.Stories.Save("F1", fixtures.Stories.Current("F1")! with { DrawnSpan = Away with { Phase = DrawnSpanPhase.Back, Opened = 1 } });

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(3), CancellationToken.None));

        // Chapter 2 of the fixture story rolls between the reduced chance and the full one, so it opens a span only without the factor.
        var story = fixtures.Stories.Current("F1")!;
        Assert.Equal(2, story.Chapters.Count);
        Assert.Null(story.DrawnSpan);
        Assert.Equal(1, story.SpanClosedAt);
        Assert.False(fixtures.Asks[^1].Story!.InTheBlack);
    }

    [Fact]
    public async Task ARewriteKeepsTheSpanAndIsStillInTheBlack()
    {
        using var fixtures = At(Bubble, new RoundScriptedLlmProvider(Saying(Lines()), Saying(NothingObjected), Saying(NextLines), Saying(NothingObjected)));

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None));
        var written = fixtures.Stories.Current("F1")!;
        fixtures.Stories.Save("F1", written with { DrawnSpan = Away with { Opened = 1 }, Rewrite = new StoryRewrite(written.CurrentChapter!, Now.AddMinutes(5), []) });

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddMinutes(6), CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;
        Assert.Single(story.Chapters);
        Assert.NotEqual(written.CurrentChapter, story.CurrentChapter);
        Assert.Equal(Away with { Opened = 1 }, story.DrawnSpan);
        Assert.True(fixtures.Asks[^1].Story!.InTheBlack);
    }

    [Fact]
    public async Task TheFinaleWaitsWhileADrawnSpanIsOpen()
    {
        using var fixtures = At(new StarPosition(0, 0, 3000), new RoundScriptedLlmProvider(Saying(NextLines)), Away with { Phase = DrawnSpanPhase.Back }, clues: 14);

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(361), CancellationToken.None));

        var asked = Assert.Single(fixtures.Asks).Story!;
        Assert.Null(asked.FinaleChapter);
        Assert.True(asked.InTheBlack);
        Assert.Null(fixtures.Stories.Current("F1")!.FinaleFrom);
        Assert.NotNull(fixtures.Stories.Current("F1")!.DrawnSpan);
    }

    [Fact]
    public async Task TheFinaleStartsOnceTheSpanClosesNearItsStart()
    {
        using var fixtures = At(new StarPosition(0, 0, 900), Finale(), Away with { Phase = DrawnSpanPhase.Back }, clues: 14);

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(361), CancellationToken.None));

        Assert.Equal(1, Assert.Single(fixtures.Asks).Story!.FinaleChapter);
        var story = fixtures.Stories.Current("F1")!;
        Assert.Null(story.DrawnSpan);
        Assert.Equal(0, story.SpanClosedAt);
    }

    [Fact]
    public async Task AChapterAfterTheSpansFirstDrawsItsFirstSlotFromExplorationOrExobiology()
    {
        using var limited = At(new StarPosition(0, 0, 3000), Finale(), Away);
        using var free = At(new StarPosition(0, 0, 3000), Finale());
        var rescuer = Explorer with { Exploration = 1, SearchAndRescue = 100 };
        limited.Director.Ratings = () => rescuer;
        free.Director.Ratings = () => rescuer;

        await limited.Director.WriteNextAsync("F1", Now, CancellationToken.None);
        await free.Director.WriteNextAsync("F1", Now, CancellationToken.None);

        Assert.True(Career(limited.Asks[0].Story!.Slots![0]) is D47.Core.Stories.Career.Exploration or D47.Core.Stories.Career.Exobiology);
        Assert.Equal(D47.Core.Stories.Career.SearchAndRescue, Career(free.Asks[0].Story!.Slots![0]));
    }

    [Fact]
    public void TheSpanAndWhereTheLastClosedSurviveARestart()
    {
        using var fixtures = At(Bubble, new RoundScriptedLlmProvider(), Away, closedAt: 3);

        var reopened = new StoryFixtures(new RoundScriptedLlmProvider(), reopening: fixtures);
        var story = reopened.Stories.Current("F1")!;

        Assert.Equal(Away, story.DrawnSpan);
        Assert.Equal(3, story.SpanClosedAt);
    }

    [Fact]
    public async Task ASpanDoesNotOpenWhenTheGalaxySearchFindsNoPlaceAroundItsFirstWaypoint()
    {
        async Task<DrawnSpan?> SecondChapter(bool placesOnTheWay)
        {
            using var fixtures = At(Bubble, new RoundScriptedLlmProvider(Saying(Lines()), Saying(NothingObjected), Saying(NextLines), Saying(NothingObjected)));
            fixtures.Director.Galaxy = () => new Waypoints(placesOnTheWay);

            Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None));
            fixtures.Finish("F1", fixtures.Stories.Current("F1")!.CurrentChapter!, Now);
            Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(3), CancellationToken.None));

            return fixtures.Stories.Current("F1")!.DrawnSpan;
        }

        Assert.NotNull(await SecondChapter(placesOnTheWay: true));
        Assert.Null(await SecondChapter(placesOnTheWay: false));
    }

    [Fact]
    public async Task ALaterChapterOutTurnsBackWhenTheGalaxySearchFindsNoPlaceOnTheWay()
    {
        using var fixtures = At(new StarPosition(0, 0, 1500), Finale(), Away);
        fixtures.Director.Galaxy = () => new Waypoints(placesOnTheWay: false);

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None));

        Assert.Equal(DrawnSpanPhase.Back, fixtures.Stories.Current("F1")!.DrawnSpan!.Phase);
        Assert.Equal(DrawnSpanPhase.Back, Assert.Single(fixtures.Asks).Story!.Span!.Phase);
    }

    [Fact]
    public async Task ALaterChapterOutStaysOutWhenAPlaceLiesOnTheWay()
    {
        using var fixtures = At(new StarPosition(0, 0, 1500), Finale(), Away);
        fixtures.Director.Galaxy = () => new Waypoints(placesOnTheWay: true);

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None));

        Assert.Equal(DrawnSpanPhase.Out, fixtures.Stories.Current("F1")!.DrawnSpan!.Phase);
    }

    private static D47.Core.Stories.Career? Career(DrawnActivity slot) => ChapterActivities.Find(slot.Kind, slot.Family, slot.Organic)?.Career;

    private static RoundScriptedLlmProvider Finale() => new(Saying(NextLines), Saying(NothingObjected));

    /// <summary>
    /// Stations 0.85 of the fixture's reach of 3,050 light years out from the bubble, every 5 degrees across the headings a drawn span
    /// may take, so a chapter out has a last stop toward any target.
    /// </summary>
    private static IReadOnlyList<StationSummary> OnTheWayOut() =>
    [
        .. Enumerable.Range(-13, 27).Select(step =>
        {
            var heading = step * 5 * Math.PI / 180;
            var away = 0.85 * 3050;

            return new StationSummary
            {
                Name = $"Outpost {step + 13}",
                SystemName = $"Far Reach {step + 13}",
                SystemAddress = 9_000_000 + step,
                MarketId = 9_100_000 + step,
                Distance = away,
                HasLargePad = true,
                Type = "Coriolis Starport",
                Position = new StarPosition(Bubble.X + (away * Math.Sin(heading)), Bubble.Y, Bubble.Z + (away * Math.Cos(heading))),
            };
        }),
    ];

    /// <summary>A galaxy with an open station beside the Commander for every named-system search, and one at the point searched around only when <c>placesOnTheWay</c>.</summary>
    private sealed class Waypoints(bool placesOnTheWay) : StubGalaxyService
    {
        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            IReadOnlyList<StationSummary> found = query.ReferencePosition is { } centre
                ? placesOnTheWay ? [Port with { Position = centre }] : []
                : [Port];

            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, found.Count, found));
        }

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, 0, []));

        private static readonly StationSummary Port = new()
        {
            Name = "Near Port", SystemName = "Near", SystemAddress = 8, MarketId = 8_000, Distance = 5, HasLargePad = true, Type = "Coriolis Starport", Position = Bubble,
        };
    }

    private static StoryFixtures At(StarPosition at, RoundScriptedLlmProvider provider, DrawnSpan? span = null, int clues = 0, int? closedAt = null)
    {
        var fixtures = new StoryFixtures(provider, stations: OnTheWayOut());
        var game = Loaded(1_000_000, cargo: 4);

        game.Apply(AdventureFixtures.Event(
            $$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"Location", "StarSystem":"Somewhere", "StarPos":[{{at.X}},{{at.Y}},{{at.Z}}], "SystemAddress":7, "Docked":false }"""));

        fixtures.Director.Game = () => game;
        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now,
            BeaconScanAt = Now,
            CluesGiven = clues,
            DrawnSpan = span,
            SpanClosedAt = closedAt,
        });

        return fixtures;
    }
}
