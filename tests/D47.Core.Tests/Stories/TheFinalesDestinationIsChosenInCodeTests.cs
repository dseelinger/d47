using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.PlannerWorld;

namespace D47.Core.Tests.Stories;

/// <summary>
/// Finale chapter 1 is planned with a destination chosen in code: a landable body no nearer than half the limit and no farther than
/// the limit, in no system that needs a permit, drawn from the seed.
/// </summary>
public sealed class TheFinalesDestinationIsChosenInCodeTests
{
    // Reach 50 and four finale chapters: the limit is 750 light years, half of it 375, from x = 100.
    private static ChapterInputs First(int finaleChapters = 4) => Inputs(Bounty, Codex) with { FinaleChapter = 1, FinaleChapters = finaleChapters };

    private static ChapterPlaces With(params BodySummary[] destinations) => Places() with { Destinations = destinations };

    [Fact]
    public void TheSameSeedChoosesTheSameDestination()
    {
        var places = With(Body("Far A", "Fixture Far A", 501, 1, 500), Body("Far B", "Fixture Far B", 502, 1, 600), Body("Far C", "Fixture Far C", 503, 1, 700));

        var first = Planned(First(), places, 11).Destination;

        Assert.NotNull(first);
        Assert.Equal(first, Planned(First(), places, 11).Destination);
        Assert.Equal(first, Planned(First(), places, 11).Destination);
    }

    [Fact]
    public void TheDestinationIsLandableAndWithinTheLimit()
    {
        var places = With(
            Body("Past", "Fixture Past", 510, 1, 900),
            Body("Within", "Fixture Within", 511, 1, 700),
            Body("Rock", "Fixture Rock", 512, 1, 500) with { IsLandable = false });

        foreach (var seed in Enumerable.Range(1, 20).Select(seed => (uint)seed))
        {
            Assert.Equal("Within", Planned(First(), places, seed).Destination!.Body);
        }
    }

    [Fact]
    public void ABodyInASystemThatNeedsAPermitIsNotChosen()
    {
        var places = With(Body("Locked", "Sol", 520, 1, 500), Body("Open", "Fixture Open", 521, 1, 500));

        foreach (var seed in Enumerable.Range(1, 20).Select(seed => (uint)seed))
        {
            Assert.Equal("Fixture Open", Planned(First(), places, seed).Destination!.System);
        }
    }

    [Fact]
    public void ANearerBodyIsChosenOnlyWhenNoneStandsBetweenHalfTheLimitAndTheLimit()
    {
        var near = Body("Near", "Fixture Near", 530, 1, 200);

        Assert.Equal("Near", Planned(First(), With(near), 3).Destination!.Body);

        var far = Body("Far", "Fixture Far", 531, 1, 600);

        foreach (var seed in Enumerable.Range(1, 20).Select(seed => (uint)seed))
        {
            Assert.Equal("Far", Planned(First(), With(near, far), seed).Destination!.Body);
        }
    }

    [Fact]
    public void ThePlanCarriesTheDestinationAndALongStoryDoesNotLandOnItYet()
    {
        var plan = Planned(First(), With(Body("Far", "Fixture Far", 540, 1, 600)));

        Assert.Equal(new AdventureDestination(540, "Fixture Far", 1, "Far"), plan.Destination);
        Assert.DoesNotContain(plan.Objectives, objective => objective.Purpose is PlannedPurpose.Closer or PlannedPurpose.Destination);
    }

    [Fact]
    public void AOneChapterFinaleLandsOnTheDestinationItChose()
    {
        var plan = Planned(First(finaleChapters: 1), With(Body("Far", "Fixture Far", 541, 1, 200)));

        Assert.Equal(PlannedPurpose.Destination, plan.Objectives[^1].Purpose);
        Assert.Equal(plan.Destination!.Landing(), Assert.Single(plan.Objectives[^1].Steps));
    }

    [Fact]
    public void WithNoLandableBodyFoundTheChapterIsRefused()
    {
        var result = ChapterPlanner.Plan(First(), Places(), 7);

        Assert.False(result.Succeeded);
        Assert.Equal(ChapterRules.Finale, result.Refusal!.Rule);
        Assert.Contains("landable body", result.Refusal.Text);
    }

    [Fact]
    public void ABodyBeyondTheLimitAloneIsRefused()
    {
        var result = ChapterPlanner.Plan(First(), With(Body("Past", "Fixture Past", 550, 1, 2000)), 7);

        Assert.False(result.Succeeded);
        Assert.Equal(ChapterRules.Finale, result.Refusal!.Rule);
    }
}
