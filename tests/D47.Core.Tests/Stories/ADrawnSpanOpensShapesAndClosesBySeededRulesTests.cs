using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>The seeded rules that open a span in the black from an Exploration or Exobiology draw, shape it, and settle its phases (#2249).</summary>
public sealed class ADrawnSpanOpensShapesAndClosesBySeededRulesTests
{
    private static readonly StarPosition Bubble = new(0, 10, 100);

    private static readonly SpanReach Reach = new(3000, null);

    private static readonly DrawnSpanGates Open = new() { ExplorationWeight = 100, Reach = Reach, At = Bubble };

    private static readonly DrawnSpan Out = new(
        DrawnSpanKind.Exploration, null, null, new StarPosition(0, 10, 2350), 750, Bubble, DrawnSpanPhase.Out, 1, 1);

    [Fact]
    public void EveryGateHoldingLetsTheSpanOpen() => Assert.True(Open.Hold);

    [Theory]
    [InlineData("rewrite or replacement")]
    [InlineData("before the beacon scan")]
    [InlineData("finale")]
    [InlineData("authored span")]
    [InlineData("drawn span open")]
    [InlineData("home target or heading")]
    [InlineData("no exploration")]
    [InlineData("no reach")]
    [InlineData("no position")]
    public void EachGateOnItsOwnStopsASpanFromOpening(string gate)
    {
        var shut = gate switch
        {
            "rewrite or replacement" => Open with { NewChapter = false },
            "before the beacon scan" => Open with { BeaconScanned = false },
            "finale" => Open with { Finale = true },
            "authored span" => Open with { AuthoredSpan = true },
            "drawn span open" => Open with { SpanOpen = true },
            "home target or heading" => Open with { Heading = true },
            "no exploration" => Open with { ExplorationWeight = 0 },
            "no reach" => Open with { Reach = null },
            _ => Open with { At = null },
        };

        Assert.False(shut.Hold);
        Assert.All(Enumerable.Range(1, 60), chapter => Assert.Null(DrawnSpans.Open("story", chapter, StoryPacing.OneYear, shut, previousClosed: false)));
    }

    [Fact]
    public void SomeChaptersOpenASpanWhenEveryGateHolds() =>
        Assert.Contains(Enumerable.Range(1, 60), chapter => DrawnSpans.Open("story", chapter, StoryPacing.OneYear, Open, previousClosed: false) is not null);

    [Fact]
    public void AnExplorationWeightOfZeroNeverOpensOne() =>
        Assert.All(Enumerable.Range(1, 200), chapter => Assert.Null(DrawnSpans.Open("story", chapter, StoryPacing.OneYear, Open with { ExplorationWeight = 0 }, previousClosed: false)));

    [Fact]
    public void TheSameSeedGivesTheSameResult() =>
        Assert.All(Enumerable.Range(1, 60), chapter =>
            Assert.Equal(
                DrawnSpans.Open("story", chapter, StoryPacing.OneYear, Open, previousClosed: false),
                DrawnSpans.Open("story", chapter, StoryPacing.OneYear, Open, previousClosed: false)));

    [Fact]
    public void TheRollIsSeededByTheStoryChapterAndBlack()
    {
        var chapters = Enumerable.Range(1, 60).ToList();
        var opened = chapters.Where(chapter => DrawnSpans.Open("story", chapter, StoryPacing.OneYear, Open, previousClosed: false) is not null).ToList();
        var under = chapters.Where(chapter => new Random32(ChapterDraw.BlackSeed("story", chapter)).NextDouble() < DrawnSpans.Chance(100, false)).ToList();

        Assert.Equal(under, opened);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 0.25)]
    [InlineData(75, 0.375)]
    [InlineData(100, 0.5)]
    public void TheChanceScalesWithTheExplorationWeightUpToAHalf(double weight, double chance) =>
        Assert.Equal(chance, DrawnSpans.Chance(weight, previousClosed: false), 9);

    [Theory]
    [InlineData(50, 0.0625)]
    [InlineData(100, 0.125)]
    public void AChapterAfterASpanClosedHasAQuarterOfTheChance(double weight, double chance) =>
        Assert.Equal(chance, DrawnSpans.Chance(weight, previousClosed: true), 9);

    [Fact]
    public void TheRecentFactorStopsChaptersTheFullChanceWouldOpen()
    {
        var chapters = Enumerable.Range(1, 200).ToList();
        var full = chapters.Count(chapter => DrawnSpans.Open("story", chapter, StoryPacing.OneYear, Open, previousClosed: false) is not null);
        var after = chapters.Count(chapter => DrawnSpans.Open("story", chapter, StoryPacing.OneYear, Open, previousClosed: true) is not null);

        Assert.InRange(after, 1, (full / 2) - 1);
        Assert.All(chapters, chapter =>
            Assert.True(DrawnSpans.Open("story", chapter, StoryPacing.OneYear, Open, previousClosed: true) is null
                || DrawnSpans.Open("story", chapter, StoryPacing.OneYear, Open, previousClosed: false) is not null));
    }

    [Theory]
    [InlineData("3-days", 0.99, 1, 0)]
    [InlineData("1-week", 0.99, 1, 0)]
    [InlineData("2-weeks", 0.99, 1, 0)]
    [InlineData("1-month", 0.69, 1, 1)]
    [InlineData("1-month", 0.7, 2, 1)]
    [InlineData("3-months", 0.99, 2, 2)]
    [InlineData("1-month", 0.49, 1, 0)]
    [InlineData("1-month", 0.5, 1, 1)]
    [InlineData("3-months", 0.84, 2, 1)]
    [InlineData("3-months", 0.85, 2, 2)]
    [InlineData("6-months", 0.49, 1, 0)]
    [InlineData("6-months", 0.5, 2, 1)]
    [InlineData("6-months", 0.84, 2, 1)]
    [InlineData("1-year", 0.85, 3, 2)]
    [InlineData("1-year", 0.99, 3, 2)]
    public void TheChaptersOutAndAtTheTargetFollowTheStoryLength(string length, double roll, int outward, int there)
    {
        var pacing = StoryPacing.Find(length)!;

        Assert.Equal(outward, DrawnSpans.Out(pacing, roll));
        Assert.Equal(there, DrawnSpans.There(pacing, roll));
    }

    [Fact]
    public void AnOpenedSpanTravelsChaptersOutTimesThreeQuartersOfTheReachWithinSixtyDegreesOfAway()
    {
        var spans = Enumerable.Range(1, 200)
            .Select(chapter => DrawnSpans.Open("story", chapter, StoryPacing.OneYear, Open, previousClosed: false))
            .OfType<DrawnSpan>()
            .ToList();

        Assert.NotEmpty(spans);
        Assert.All(spans, span =>
        {
            var far = Bubble.DistanceTo(span.TargetAt);
            var heading = Math.Atan2(span.TargetAt.X - Bubble.X, span.TargetAt.Z - Bubble.Z) * 180 / Math.PI;

            Assert.Contains(Math.Round(far / (DrawnSpans.OutShare * Reach.LightYears), 6), new[] { 1.0, 2.0, 3.0 });
            Assert.InRange(heading, -DrawnSpans.MostTurn, DrawnSpans.MostTurn);
            Assert.Equal(Bubble.Y, span.TargetAt.Y);
            Assert.Equal(750, span.Within);
            Assert.Equal(Bubble, span.StartAt);
            Assert.Equal(DrawnSpanPhase.Out, span.Phase);
            Assert.InRange(span.ThereLeft, 0, 2);
            Assert.Equal(DrawnSpanKind.Exploration, span.Kind);
            Assert.Null(span.Target);
            Assert.Null(span.TargetAddress);
        });
    }

    [Fact]
    public void AShortStorysSpanGoesOneChapterOutAndNoneThere() =>
        Assert.All(
            Enumerable.Range(1, 200).Select(chapter => DrawnSpans.Open("story", chapter, StoryPacing.OneWeek, Open, previousClosed: false)).OfType<DrawnSpan>(),
            span =>
            {
                Assert.Equal(DrawnSpans.OutShare * Reach.LightYears, Bubble.DistanceTo(span.TargetAt), 6);
                Assert.Equal(0, span.ThereLeft);
            });

    [Fact]
    public void TheTargetHeadsAwayFromSolTurnedByTheAngle()
    {
        var straight = DrawnSpans.Target(new StarPosition(100, 20, 0), 1000, 0);
        var turned = DrawnSpans.Target(new StarPosition(0, 20, 100), 1000, 60);

        Assert.Equal(1100, straight.X, 6);
        Assert.Equal(0, straight.Z, 6);
        Assert.Equal(20, straight.Y);
        Assert.Equal(1000 * Math.Sin(Math.PI / 3), turned.X, 6);
        Assert.Equal(100 + (1000 * Math.Cos(Math.PI / 3)), turned.Z, 6);
    }

    [Fact]
    public void NearerColoniaTheTargetHeadsAwayFromColonia()
    {
        var at = ExpeditionKit.Colonia with { X = ExpeditionKit.Colonia.X + 500 };
        var target = DrawnSpans.Target(at, 1000, 0);

        Assert.Equal(at.X + 1000, target.X, 6);
        Assert.Equal(at.Z, target.Z, 6);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0.5, 300, 0.5)]
    public void WithinALightYearOfSolTheHeadingIsTheAngleAlone(double x, double y, double z)
    {
        var at = new StarPosition(x, y, z);
        var target = DrawnSpans.Target(at, 1000, 90);

        Assert.Equal(x + 1000, target.X, 6);
        Assert.Equal(z, target.Z, 6);
        Assert.Equal(y, target.Y);
    }

    [Fact]
    public void OutStaysOutUntilTheCommanderIsWithinReachOfTheTarget() =>
        Assert.Equal(Out, DrawnSpans.Next(Out, new StarPosition(0, 10, 1500), ["a"], [], 100));

    [Fact]
    public void OutBecomesThereWithinReachOfTheTarget() =>
        Assert.Equal(Out with { Phase = DrawnSpanPhase.There }, DrawnSpans.Next(Out, new StarPosition(0, 10, 1700), ["a"], [], 100));

    [Fact]
    public void OutBecomesBackAtTheTargetWhenNoChaptersArePlayedThere() =>
        Assert.Equal(
            Out with { ThereLeft = 0, Phase = DrawnSpanPhase.Back },
            DrawnSpans.Next(Out with { ThereLeft = 0 }, new StarPosition(0, 10, 2000), ["a"], [], 100));

    [Fact]
    public void ThereCountsDownAChapterAtATimeAndThenBecomesBack()
    {
        var there = Out with { Phase = DrawnSpanPhase.There, ThereLeft = 2 };
        var target = there.TargetAt;

        var once = DrawnSpans.Next(there, target, ["a"], [], 100)!;
        var twice = DrawnSpans.Next(once, target, ["a"], [], 100)!;

        Assert.Equal(there with { ThereLeft = 1 }, once);
        Assert.Equal(there with { ThereLeft = 0, Phase = DrawnSpanPhase.Back }, twice);
    }

    [Fact]
    public void BackStaysOpenUntilTheCommanderIsWithinAThousandLightYearsOfTheStart()
    {
        var back = Out with { Phase = DrawnSpanPhase.Back };

        Assert.Equal(back, DrawnSpans.Next(back, new StarPosition(0, 10, 1200), ["a"], [], 100));
        Assert.Null(DrawnSpans.Next(back, new StarPosition(0, 10, 1050), ["a"], [], 100));
    }

    [Theory]
    [InlineData(DrawnSpanPhase.Out)]
    [InlineData(DrawnSpanPhase.There)]
    public void TwoChangedChaptersInARowTurnTheSpanBack(DrawnSpanPhase phase)
    {
        var span = Out with { Phase = phase, ThereLeft = 2 };

        Assert.Equal(span with { Phase = DrawnSpanPhase.Back }, DrawnSpans.Next(span, new StarPosition(0, 10, 2350), ["a", "b", "c"], ["b", "c"], 100));
        Assert.Equal(span.Phase, DrawnSpans.Next(span, new StarPosition(0, 10, 1500), ["a", "b", "c"], ["a", "c"], 100)!.Phase);
    }

    [Theory]
    [InlineData(DrawnSpanPhase.Out)]
    [InlineData(DrawnSpanPhase.There)]
    public void AnExplorationWeightOfZeroTurnsAnExplorationSpanBack(DrawnSpanPhase phase)
    {
        var span = Out with { Phase = phase, ThereLeft = 2 };

        Assert.Equal(DrawnSpanPhase.Back, DrawnSpans.Next(span, new StarPosition(0, 10, 1500), ["a"], [], 0)!.Phase);
        Assert.NotEqual(DrawnSpanPhase.Back, DrawnSpans.Next(span with { Kind = DrawnSpanKind.Sight }, new StarPosition(0, 10, 1500), ["a"], [], 0)!.Phase);
    }

    [Fact]
    public void ASpanWhoseTargetIsNearItsStartTurnsBackAndClosesInOneSettlement() =>
        Assert.Null(DrawnSpans.Next(Out with { TargetAt = new StarPosition(0, 10, 600), Within = 200, ThereLeft = 0 }, new StarPosition(0, 10, 650), ["a"], [], 100));

    [Fact]
    public void WithNoKnownPositionOutStaysOut() => Assert.Equal(Out, DrawnSpans.Next(Out, null, ["a"], [], 100));
}
