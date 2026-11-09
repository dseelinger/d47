using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.PlannerWorld;

namespace D47.Core.Tests.Stories;

/// <summary>A drawn span's chapters travel out toward its target and back to where it opened at the span reach R, and hold their travel to R.</summary>
public sealed class ASpanChapterTravelsTowardItsTargetTests
{
    private static readonly StarPosition Opened = new(0, 0, 100);

    private static readonly StarPosition Target = new(0, 0, 4675);

    private static readonly StarPosition Between = new(0, 0, 2000);

    private static readonly SpanReach R = new(3050, new SpanShip(7, "Diamondback Explorer", "Long Quiet", "Varati", Aboard: false));

    private static DrawnSpan Span(DrawnSpanPhase phase) =>
        new(DrawnSpanKind.Exploration, null, null, Target, 762.5, Opened, phase, 1, 2);

    private static AdventureAsk Ask(DrawnSpan? span, SpanReach? reach = null) =>
        new(AdventureReach.Anywhere, Story: PlannedChapterWorld.Story with { InTheBlack = true, Span = span, SpanReach = reach ?? R });

    [Fact]
    public void AChapterOutTravelsTowardTheTargetAndOneBackTowardWhereTheSpanOpened()
    {
        var outward = ChapterPull.Of(Ask(Span(DrawnSpanPhase.Out)), Between)!;
        var back = ChapterPull.Of(Ask(Span(DrawnSpanPhase.Back)), Between)!;

        Assert.True(outward is { Drawn: true, Back: false, Home: false, Arrived: false });
        Assert.Equal(Target, outward.There);
        Assert.Equal(762.5, outward.Within);

        Assert.True(back is { Drawn: true, Back: true, Home: false, Arrived: false });
        Assert.Equal(Opened, back.There);
        Assert.Equal(HomeLeg.Home, back.Within);
    }

    [Fact]
    public void AChapterAtTheTargetTravelsNoLeg() =>
        Assert.Null(ChapterPull.Of(Ask(Span(DrawnSpanPhase.There)), Target));

    [Fact]
    public void TheDrawnLegComesBeforeTheHomeLeg()
    {
        var ask = Ask(Span(DrawnSpanPhase.Out));

        Assert.True(ChapterPull.Of(ask with { Story = ask.Story! with { Home = "Sol" } }, Between) is { Drawn: true });
    }

    [Fact]
    public void ReachOfGivesRInASpanChapterOutAndBack()
    {
        Assert.Equal(3050, AdventureGenerator.ReachOf(Ask(Span(DrawnSpanPhase.Out)), Between, 30, carrier: false));
        Assert.Equal(3050, AdventureGenerator.ReachOf(Ask(Span(DrawnSpanPhase.Back)), Between, 30, carrier: false));
    }

    [Fact]
    public void AChapterAtTheTargetPlansAtTheSessionReachEvenInALongHaulStory()
    {
        var ask = Ask(Span(DrawnSpanPhase.There));

        Assert.Equal(
            AdventureGenerator.Radius(AdventureReach.Session, 30, carrier: false),
            AdventureGenerator.ReachOf(ask with { Story = ask.Story! with { LongHaul = true } }, Target, 30, carrier: false));
    }

    [Fact]
    public void AnAuthoredSpanWithADestinationAndTheHomeLegTravelAtR()
    {
        var authored = new AdventureAsk(AdventureReach.Anywhere, Story: PlannedChapterWorld.Story with { InTheBlack = true, Toward = "Colonia", SpanReach = R });
        var home = new AdventureAsk(AdventureReach.Anywhere, Story: PlannedChapterWorld.Story with { Home = "Sol", SpanReach = new SpanReach(5000, null) });

        Assert.Equal(3050, AdventureGenerator.ReachOf(authored, StarPosition.Origin, 30, carrier: false));
        Assert.Equal(5000, AdventureGenerator.ReachOf(home, Target, 30, carrier: true));
    }

    [Fact]
    public void WithNoKnownRAnAuthoredSpanKeepsTheLongHaulReach()
    {
        var authored = new AdventureAsk(AdventureReach.Anywhere, Story: PlannedChapterWorld.Story with { InTheBlack = true, Toward = "Colonia" });

        Assert.Equal(1500, AdventureGenerator.ReachOf(authored, StarPosition.Origin, 30, carrier: false));
    }

    [Fact]
    public void ASpanChapterHoldsItsTravelToR()
    {
        var state = PlannedChapterWorld.State();
        Assert.Equal(3050, ChapterInputs.Of(state, Ask(Span(DrawnSpanPhase.Out))).Travel);
        Assert.Equal(3050, ChapterInputs.Of(state, Ask(Span(DrawnSpanPhase.There))).Travel);
        Assert.Null(ChapterInputs.Of(state, new AdventureAsk(AdventureReach.Anywhere, Story: PlannedChapterWorld.Story with { InTheBlack = true, Toward = "Colonia", SpanReach = R })).Travel);
    }

    [Fact]
    public void AStopThatTakesTheChaptersTravelPastRIsNotPlanned()
    {
        const double most = 30;
        var unheld = 0;

        foreach (var seed in Enumerable.Range(1, 60).Select(seed => (uint)seed))
        {
            Assert.True(Travelled(Planned(Inputs(Bounty) with { Travel = most }, Places(), seed)) <= most, $"seed {seed} travels past {most} light years");

            unheld += Travelled(Planned(Inputs(Bounty), Places(), seed)) > most ? 1 : 0;
        }

        Assert.True(unheld > 0, "no plan without the limit travels past it, so the limit was not tested");
    }

    [Fact]
    public async Task NotablePlacesForATravellingChapterAreSearchedAroundTheWaypointNearestFirst()
    {
        var places = new Recording();
        var state = PlannedChapterWorld.State();
        var generator = new AdventureGenerator(
            () => null, () => null, () => "You are the ship.", () => "core", () => null, () => state, () => new PlannedChapterWorld.Galaxy(), () => places,
            null, null, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        var here = new StarPosition(100, 0, 0);
        var waypoint = ChapterPull.Waypoint(here, Target, R.LightYears);

        var found = await generator.ChapterPlacesAsync(Ask(Span(DrawnSpanPhase.Out)), TestContext.Current.CancellationToken);

        Assert.Equal(waypoint, places.From);
        Assert.Equal(ChapterPull.PlacesWithin * R.LightYears, places.Radius);
        Assert.Equal(AdventureGenerator.MostNotable, found.Notable.Count);
        Assert.Equal([.. found.Notable.OrderBy(place => place.DistanceFrom(waypoint))], found.Notable);
        Assert.Equal("Nearest", found.Notable[0].Name);
    }

    [Fact]
    public void TheWriterIsToldRAndTheStoredShipThatGivesIt()
    {
        var described = AdventureGenerator.Facts.Of(PlannedChapterWorld.State(), Ask(Span(DrawnSpanPhase.Out))).Describe();

        Assert.Contains("about 3050 light years, a hundred jumps of the best jump range in the Commander's fleet", described);
        Assert.Contains("the most the chapter's hops may add up to", described);
        Assert.Contains("the Diamondback Explorer \"Long Quiet\"'s, which is stored in Varati", described);
    }

    [Fact]
    public void TheWriterIsNotToldOfAShipWhenTheShipAboardGivesR()
    {
        var aboard = new SpanReach(3050, new SpanShip(7, "Diamondback Explorer", "Long Quiet", "Varati", Aboard: true));

        Assert.DoesNotContain("which is stored", AdventureGenerator.Facts.Of(PlannedChapterWorld.State(), Ask(Span(DrawnSpanPhase.Out), aboard)).Describe());
    }

    /// <summary>The light years from the start through each step that puts the Commander in another system.</summary>
    private static double Travelled(ChapterPlan plan)
    {
        var places = Places();
        var at = Here;
        var travelled = 0.0;

        foreach (var step in Placed(plan))
        {
            var next = places.System(step.System)!.Position!.Value;
            travelled += at.DistanceTo(next);
            at = next;
        }

        return travelled;
    }

    private sealed class Recording : INotablePlacesService
    {
        public StarPosition? From { get; private set; }

        public double Radius { get; private set; }

        public Task<IReadOnlyList<NotablePlace>> NearAsync(StarPosition from, double radiusLightYears, int limit, CancellationToken cancellationToken)
        {
            From = from;
            Radius = radiusLightYears;

            // Listed best-rated first, as the catalogue does: the nearest is last.
            IReadOnlyList<NotablePlace> listed =
            [
                .. Enumerable.Range(1, 15).Select(index => new NotablePlace($"Place {index}", "Landmark", $"Notable {index}", 8000 + index, new StarPosition(from.X + 20 + (index * 10), from.Y, from.Z)) { Rating = 100 - index }),
                new NotablePlace("Nearest", "Landmark", "Notable Nearest", 8100, new StarPosition(from.X + 1, from.Y, from.Z)) { Rating = 1 },
            ];

            return Task.FromResult<IReadOnlyList<NotablePlace>>([.. listed.Take(limit)]);
        }
    }
}
