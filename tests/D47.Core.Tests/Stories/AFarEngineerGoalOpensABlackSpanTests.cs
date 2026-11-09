using D47.Core.Adventures;
using D47.Core.Engineers;
using D47.Core.Goals;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>An engineers step for an engineer out at Colonia, more than 1,000 light years away, opens a span in a story of three months or longer (#2252).</summary>
public sealed class AFarEngineerGoalOpensABlackSpanTests
{
    private static readonly StarPosition Bubble = new(0, 0, 100);

    private static readonly Engineer Far = new() { Id = 900001, Name = "Far One", System = "Distant", Position = new StarPosition(-9500, -900, 19800) };

    private static readonly Engineer Near = new() { Id = 900002, Name = "Near One", System = "Close", Position = new StarPosition(40, 0, 30) };

    private static readonly DrawnSpan Out = DrawnSpans.OpenEngineer(2, StoryPacing.ThreeMonths, Far, Bubble)!;

    private static EngineerReport Route(Engineer engineer) =>
        new() { Route = [new UnlockCandidate { Engineer = engineer, Chain = UnlockChain.Done }] };

    private static List<GoalStanding> Standings() =>
    [
        .. GoalCatalogue.Every.Select(arc => new GoalStanding
        {
            Arc = arc,
            Have = arc.Key == GoalCatalogue.Engineers ? 100 : 500,
            Need = 1000,
            Source = GoalSource.Live,
        }),
    ];

    private static CommanderGameState StateAt(double x, double y, double z)
    {
        var state = Loaded(1_000_000_000);

        state.Apply(AdventureFixtures.Event(
            $$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"FSDJump", "StarSystem":"Here", "SystemAddress":7, "StarPos":[{{x}},{{y}},{{z}}] }"""));
        return state;
    }

    [Theory]
    [InlineData("3-months", true)]
    [InlineData("6-months", true)]
    [InlineData("1-year", true)]
    [InlineData("1-month", false)]
    [InlineData("2-weeks", false)]
    [InlineData("1-week", false)]
    [InlineData("3-days", false)]
    public void TheSpanOpensForAFarEngineerInAStoryOfThreeMonthsOrLonger(string length, bool opens)
    {
        var span = DrawnSpans.OpenEngineer(2, StoryPacing.Find(length)!, Far, Bubble);

        Assert.Equal(opens, span is not null);

        if (span is not null)
        {
            Assert.Equal(DrawnSpanKind.Engineers, span.Kind);
            Assert.Equal("Distant", span.Target);
            Assert.Equal(Far.Position, span.TargetAt);
            Assert.Equal(1000, span.Within);
            Assert.Equal(Bubble, span.StartAt);
            Assert.Equal(DrawnSpanPhase.Out, span.Phase);
            Assert.Equal(2, span.Opened);
        }
    }

    [Fact]
    public void NoSpanOpensForABubbleEngineerAnEngineerWithoutAPositionOrOneWithinAThousandLightYears()
    {
        Assert.Null(DrawnSpans.OpenEngineer(2, StoryPacing.ThreeMonths, Near, Bubble));
        Assert.Null(DrawnSpans.OpenEngineer(2, StoryPacing.ThreeMonths, Far with { Position = null }, Bubble));
        Assert.Null(DrawnSpans.OpenEngineer(2, StoryPacing.ThreeMonths, Far, new StarPosition(-9500, -900, 19000)));
        Assert.NotNull(DrawnSpans.OpenEngineer(2, StoryPacing.ThreeMonths, Far, new StarPosition(-9500, -900, 18700)));
    }

    [Fact]
    public void AnExplorationWeightOfZeroDoesNotStopTheSpan()
    {
        var gates = new DrawnSpanGates { ExplorationWeight = 0, Reach = new SpanReach(3000, null), At = Bubble };

        Assert.False(gates.Hold);
        Assert.True(gates.HoldsWithoutWeight);
        Assert.False((gates with { SpanOpen = true }).HoldsWithoutWeight);

        var there = Out with { Phase = DrawnSpanPhase.There };
        Assert.Equal(there, DrawnSpans.Next(there, Far.Position, ["a", "b"], [], 0, engineerFar: true));
    }

    [Fact]
    public void ThereHoldsWhileTheNextEngineerIsFarAndMovesToBackAfter()
    {
        var arrived = DrawnSpans.Next(Out, Far.Position, ["a", "b"], [], 50, engineerFar: true)!;
        Assert.Equal(DrawnSpanPhase.There, arrived.Phase);

        var staying = DrawnSpans.Next(arrived, Far.Position, ["a", "b", "c"], [], 50, engineerFar: true)!;
        Assert.Equal(DrawnSpanPhase.There, staying.Phase);

        var leaving = DrawnSpans.Next(staying, Far.Position, ["a", "b", "c", "d"], [], 50, engineerFar: false)!;
        Assert.Equal(DrawnSpanPhase.Back, leaving.Phase);
    }

    [Fact]
    public void ArrivingWithNoFarEngineerNextGoesStraightBack() =>
        Assert.Equal(DrawnSpanPhase.Back, DrawnSpans.Next(Out, Far.Position, ["a", "b"], [], 50, engineerFar: false)!.Phase);

    [Fact]
    public void BackClosesWithinAThousandLightYearsOfWhereTheSpanOpened()
    {
        var back = Out with { Phase = DrawnSpanPhase.Back };

        Assert.NotNull(DrawnSpans.Next(back, Far.Position, ["a", "b"], [], 50));
        Assert.Null(DrawnSpans.Next(back, new StarPosition(0, 0, 900), ["a", "b"], [], 50));
    }

    [Fact]
    public void TheChaptersOutAreNotCapped()
    {
        var distance = Far.Position!.Value.DistanceTo(Bubble);

        Assert.True(DrawnSpans.ChaptersOut(distance, 1000) > DrawnSpans.MostOut(StoryPacing.OneYear));
        Assert.Equal((int)Math.Ceiling(distance / (0.75 * 3000)), DrawnSpans.ChaptersOut(distance, 3000));
    }

    [Fact]
    public void AFarEngineerMoreThanAThousandLightYearsAwayIsNotCarriedPlainly()
    {
        var report = Route(Far);
        var state = StateAt(0, 0, 100);

        Assert.Contains(GoalClimb.Steps(Standings(), state, report), step => step.Objective?.Engineer == "Far One");
        Assert.DoesNotContain(GoalClimb.Steps(Standings(), state, report, null, EngineerSteps.NotFarAway), step => step.Objective?.Kind == TriggerKind.Engineer);
        Assert.DoesNotContain(GoalClimb.Steps(Standings(), state, report, null, EngineerSteps.None), step => step.Objective?.Kind == TriggerKind.Engineer);
    }

    [Fact]
    public void AFarEngineerWithinAThousandLightYearsIsCarriedAsBefore()
    {
        var state = StateAt(-9500, -900, 19500);

        Assert.Contains(GoalClimb.Steps(Standings(), state, Route(Far), null, EngineerSteps.NotFarAway), step => step.Objective?.Engineer == "Far One");
    }

    [Fact]
    public void ABubbleEngineerIsCarriedWhereverTheCommanderIs()
    {
        var state = StateAt(-9500, -900, 19500);

        Assert.Contains(GoalClimb.Steps(Standings(), state, Route(Near), null, EngineerSteps.NotFarAway), step => step.Objective?.Engineer == "Near One");
    }

    [Fact]
    public void TheFarEngineerIsNamedOnlyWhileTheirStepWouldBeOffered()
    {
        var state = StateAt(0, 0, 100);
        var all = GoalClimb.Steps(Standings(), state, Route(Far));

        Assert.Equal("Far One", GoalClimb.FarEngineer(all, Route(Far), Bubble)?.Name);
        Assert.Null(GoalClimb.FarEngineer(all, Route(Far), new StarPosition(-9500, -900, 19500)));
        Assert.Null(GoalClimb.FarEngineer(GoalClimb.Steps(Standings(), state, Route(Near)), Route(Near), Bubble));
        Assert.Null(GoalClimb.FarEngineer([], Route(Far), Bubble));
        Assert.Null(GoalClimb.FarEngineer(all, null, Bubble));
    }
}
