using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A sight beyond the session reach opens a span of kind Sight: how far it may lie, how many chapters it takes, where it heads and how it moves (#2251).</summary>
public sealed class ASightSpanIsShapedByItsSightTests
{
    private const double R = 3000;

    private static readonly StarPosition Start = new(0, 0, 0);

    private static NotablePlace Sight(string system, double x, long? address = null) => new("Veil", "Nebulae", system, address, new StarPosition(x, 0, 0));

    [Fact]
    public void ASessionFliesThirtyThreeJumpsOfTheBestRange() =>
        Assert.Equal(33 * 30.5, new SpanReach(3050, null).SessionLightYears, 6);

    [Fact]
    public void TheSightsOfASpanLieWithinTheMostChaptersOutOfTheReach()
    {
        Assert.Equal(0.75 * R, DrawnSpans.SightRadius(StoryPacing.OneWeek, R));
        Assert.Equal(2 * 0.75 * R, DrawnSpans.SightRadius(StoryPacing.OneMonth, R));
        Assert.Equal(3 * 0.75 * R, DrawnSpans.SightRadius(StoryPacing.OneYear, R));
    }

    [Theory]
    [InlineData(1000, 1)]
    [InlineData(2250, 1)]
    [InlineData(2251, 2)]
    [InlineData(4500, 2)]
    [InlineData(4501, 3)]
    public void KIsTheDistanceOverThreeQuartersOfTheReachRoundedUp(double distance, int chapters) =>
        Assert.Equal(chapters, DrawnSpans.ChaptersOut(distance, R));

    [Fact]
    public void ASightSpanHeadsForTheSightsSystemFromWhereItOpened()
    {
        var span = DrawnSpans.OpenSight("story", 4, StoryPacing.ThreeMonths, Sight("Alpha", 3000, 77), Start);

        Assert.Equal(DrawnSpanKind.Sight, span.Kind);
        Assert.Equal(DrawnSpanPhase.Out, span.Phase);
        Assert.Equal("Alpha", span.Target);
        Assert.Equal(77, span.TargetAddress);
        Assert.Equal(new StarPosition(3000, 0, 0), span.TargetAt);
        Assert.Equal(Start, span.StartAt);
        Assert.Equal(DrawnSpans.SightArrival, span.Within);
        Assert.Equal(4, span.Opened);
    }

    [Fact]
    public void TheChaptersAtTheSightFollowTheLengthTableAndTheSeed()
    {
        var short1 = Enumerable.Range(0, 200).Select(chapter => DrawnSpans.OpenSight("story", chapter, StoryPacing.OneWeek, Sight("A", 3000), Start).ThereLeft);
        var long1 = Enumerable.Range(0, 200).Select(chapter => DrawnSpans.OpenSight("story", chapter, StoryPacing.OneYear, Sight("A", 3000), Start).ThereLeft).ToList();

        Assert.All(short1, left => Assert.Equal(0, left));
        Assert.Equal([0, 1, 2], long1.Distinct().Order());
        Assert.Equal(long1, Enumerable.Range(0, 200).Select(chapter => DrawnSpans.OpenSight("story", chapter, StoryPacing.OneYear, Sight("A", 3000), Start).ThereLeft));
    }

    [Fact]
    public void ChangingTheSightObjectiveMovesTheTargetOfASightSpan()
    {
        var span = DrawnSpans.OpenSight("story", 1, StoryPacing.OneYear, Sight("Alpha", 3000, 1), Start);

        var moved = DrawnSpans.MoveSight(span, Sight("Beta", 2000, 2));

        Assert.Equal("Beta", moved.Target);
        Assert.Equal(2, moved.TargetAddress);
        Assert.Equal(new StarPosition(2000, 0, 0), moved.TargetAt);
        Assert.Equal(span.Phase, moved.Phase);
        Assert.Equal(span.StartAt, moved.StartAt);
    }

    [Fact]
    public void AnExplorationSpanDoesNotMoveWithASight()
    {
        var span = new DrawnSpan(DrawnSpanKind.Exploration, null, null, new StarPosition(0, 0, 4000), 750, Start, DrawnSpanPhase.Out, 0, 1);

        Assert.Equal(span, DrawnSpans.MoveSight(span, Sight("Beta", 2000)));
    }
}
