using D47.Core.Adventures;
using D47.Core.Goals;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Goals;

/// <summary>A Goals story whose card lists <c>goalArcs</c> reads only those arcs for its pick, clues, finale, steps and page note (#1747).</summary>
public sealed class AGoalsStoryWorksOnlyItsListedArcsTests
{
    private static readonly IReadOnlyList<string> Careers = ["rank.combat", "rank.trade", "rank.explore", "rank.soldier", "rank.exobiologist"];

    private static GoalStanding At(string key, double fraction) => new()
    {
        Arc = GoalCatalogue.Find(key)!,
        Have = (long)Math.Round(fraction * 1000),
        Need = 1000,
        Source = GoalSource.Live,
        IsDone = fraction >= 1,
    };

    /// <summary>Every built-in arc: the five careers at <paramref name="careers"/>, the rest at <paramref name="others"/>.</summary>
    private static List<GoalStanding> Standings(double careers, double others) =>
        [.. GoalCatalogue.Every.Select(arc => At(arc.Key, Careers.Contains(arc.Key) ? careers : others))];

    private static readonly Story Picked = new()
    {
        Id = Id,
        Title = Card.Title,
        PublicLayer = Card.Describe(),
        PickedAt = Now,
        BeaconScanAt = Now,
        GoalsAtPick = 0.4,
    };

    private static CommanderGameState Unpledged()
    {
        var state = Loaded(1_000_000_000);
        state.Apply(AdventureFixtures.Event("""{ "timestamp":"2026-09-30T12:00:00Z", "event":"PowerplayLeave", "Power":"Aisling Duval" }"""));
        return state;
    }

    [Fact]
    public void WithoutAListEveryArcIsRead()
    {
        var standings = Standings(0.5, 0.1);

        Assert.Same(standings, GoalClimb.Listed(standings, null));
    }

    [Fact]
    public void AListKeepsOnlyItsArcsInCatalogueOrder()
    {
        var listed = GoalClimb.Listed(Standings(0.5, 0.1), ["rank.trade", "Rank.Combat"]);

        Assert.Equal(["rank.combat", "rank.trade"], listed.Select(standing => standing.Arc.Key));
    }

    [Fact]
    public void AnUnlistedUnfinishedArcHoldsNoClue()
    {
        var listed = GoalClimb.Listed(Standings(1, 0), Careers);
        var picked = Picked with { GoalsAtPick = 0.6 };

        Assert.Equal(1, GoalClimb.Overall(listed));
        Assert.Equal(new StoryClueDue(Id, 0), StoryClues.Due(picked, Now.AddDays(30), () => GoalClimb.Overall(listed)));
        Assert.Null(StoryClues.Due(picked, Now.AddDays(30), () => GoalClimb.Overall(Standings(1, 0))));
    }

    [Fact]
    public void AnUnlistedUnfinishedArcDoesNotHoldTheFinale()
    {
        var standings = Standings(1, 0);

        Assert.True(GoalClimb.AllDone(GoalClimb.Listed(standings, Careers)));
        Assert.False(GoalClimb.AllDone(standings));
    }

    [Fact]
    public void AnUnlistedArcIsNeverTheChaptersStep()
    {
        var step = GoalClimb.Next(GoalClimb.Listed(Standings(0.5, 0), Careers), Loaded(1_000_000_000), null);

        Assert.NotNull(step);
        Assert.Contains(step.Key, Careers);
    }

    [Fact]
    public void AnUnpledgedCommanderGetsNoPledgeStepWithoutPowerplayListed()
    {
        var unpledged = Unpledged();
        var standings = Standings(0.5, 0)
            .Select(standing => standing.Arc.Key == GoalCatalogue.Powerplay
                ? GoalEvaluator.Evaluate(standing.Arc, unpledged, null)
                : standing)
            .ToList();

        var step = GoalClimb.Next(GoalClimb.Listed(standings, Careers), unpledged, null);

        Assert.NotEqual(GoalCatalogue.Powerplay, step?.Key);
        Assert.NotEqual(TriggerKind.Pledge, step?.Objective?.Kind);
        Assert.Equal(TriggerKind.Pledge, GoalClimb.Next(GoalClimb.Listed(standings, [.. Careers, GoalCatalogue.Powerplay]), unpledged, null)?.Objective?.Kind);
    }

    [Fact]
    public void TheGoalsPageNoteCountsOnlyTheListedArcs()
    {
        var listed = GoalClimb.Listed(Standings(1, 0), ["rank.combat", "rank.trade"]);

        Assert.DoesNotContain("waits", StoryGoal.Of([Picked with { CluesGiven = 14 }], _ => null, () => listed)!.Note, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, new[] { "rank.combat" }, "goalArcs is set on a card that is not a Goals story.")]
    [InlineData(true, new string[0], "goalArcs is empty.")]
    [InlineData(true, new[] { "rank.combat", "Rank.Combat" }, "goalArcs names rank.combat more than once.")]
    [InlineData(true, new[] { "rank.cqc" }, "goalArcs names rank.cqc, which is not a Goals arc.")]
    public void ABadListIsAFault(bool goals, string[] arcs, string fault) =>
        Assert.Contains($"{Id}: {fault}", StoryCatalog.Faults(Card with { Goals = goals, GoalArcs = arcs }, Secret));

    [Fact]
    public void AGoodListIsNoFault()
    {
        var faults = StoryCatalog.Faults(Card with { Goals = true, GoalArcs = Careers }, Secret);

        Assert.DoesNotContain(faults, fault => fault.Contains("goalArcs", StringComparison.Ordinal));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheDirectorReadsOnlyTheListedArcs()
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(NextSpine), RoundScriptedLlmProvider.Saying(NextBeats), RoundScriptedLlmProvider.Saying(NothingObjected)),
            card: Card with { Length = "2-weeks", Goals = true, GoalArcs = Careers });
        IReadOnlyList<GoalStanding> goals = Standings(0.5, 0);
        fixtures.Director.Goals = () => goals;

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;
        var day = story.BeaconScanAt!.Value.AddDays(story.Pacing.ClueDays[0]);

        Assert.Equal(0.5, story.GoalsAtPick);
        Assert.Equal(Careers, fixtures.Director.GoalsOf(Id).Select(standing => standing.Arc.Key));
        Assert.Null(fixtures.Director.ClueDue("F1", day));

        goals = Standings(1, 0);

        Assert.Equal(new StoryClueDue(Id, 0), fixtures.Director.ClueDue("F1", day));
    }
}
