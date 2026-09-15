using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Engineers;

/// <summary>Grouping and sorting the engineers a plan names, apart from anything that draws them (#195).</summary>
public class EngineersListedForAPlanTests
{
    /// <summary>Increased FSD Range grade 5, the worked example from the issue: three engineers, one unlocked.</summary>
    private static readonly string[] IncreasedFsdRange5 = ["Mel Brandon", "Felicity Farseer", "Elvira Martuuk"];

    private static EngineerProgressState Progress(params (string Name, int Rank)[] unlocked) => new()
    {
        TakenAt = DateTimeOffset.UtcNow,
        Standings =
        [
            .. unlocked.Select(entry => new EngineerStanding(
                EngineerDirectory.ByName(entry.Name)!.Id, entry.Name, "Unlocked") { Rank = entry.Rank }),
        ],
    };

    [Fact]
    public void UnknownEngineersAreDropped()
    {
        var groups = PlanEngineers.For(["nobody in the table"], grade: 5, progress: null, from: null);

        Assert.True(groups.IsEmpty);
    }

    [Fact]
    public void UnlockedAndLockedAreSplitByStanding()
    {
        var groups = PlanEngineers.For(
            IncreasedFsdRange5, grade: 5, Progress(("Elvira Martuuk", 5)), from: null);

        var unlocked = Assert.Single(groups.Unlocked);

        Assert.Equal("Elvira Martuuk", unlocked.Engineer.Name);
        Assert.Equal(5, unlocked.Held);

        Assert.Equal(
            ["Felicity Farseer", "Mel Brandon"],
            groups.Locked.Select(entry => entry.Engineer.Name));
    }

    /// <summary>Invited counts as Locked — only Unlocked opens the group.</summary>
    [Fact]
    public void AnInvitedEngineerIsStillLocked()
    {
        var invited = new EngineerProgressState
        {
            TakenAt = DateTimeOffset.UtcNow,
            Standings = [new EngineerStanding(
                EngineerDirectory.ByName("Elvira Martuuk")!.Id, "Elvira Martuuk", "Invited")],
        };

        var groups = PlanEngineers.For(IncreasedFsdRange5, grade: 5, invited, from: null);

        Assert.Empty(groups.Unlocked);
        Assert.Contains(groups.Locked, entry => entry.Engineer.Name == "Elvira Martuuk");
    }

    /// <summary>An engineer ranked below the planned grade is still Unlocked, holding what they hold.</summary>
    [Fact]
    public void AnUnlockedEngineerBelowThePlannedGradeStaysUnlocked()
    {
        var groups = PlanEngineers.For(
            IncreasedFsdRange5, grade: 5, Progress(("Elvira Martuuk", 3)), from: null);

        var unlocked = Assert.Single(groups.Unlocked);

        Assert.Equal(3, unlocked.Held);
        Assert.Equal(5, groups.Grade);
    }

    [Fact]
    public void RowsAreNearestFirst()
    {
        var here = StarPosition.Origin;

        var groups = PlanEngineers.For(
            IncreasedFsdRange5,
            grade: 5,
            Progress(("Elvira Martuuk", 5), ("Mel Brandon", 5), ("Felicity Farseer", 5)),
            here);

        var byDistance = groups.Unlocked
            .Select(entry => entry.Engineer.DistanceFrom(here))
            .ToList();

        Assert.Equal(byDistance.OrderBy(light => light), byDistance);
    }

    /// <summary>With no known position, rows fall back to name order rather than an arbitrary one.</summary>
    [Fact]
    public void WithNoKnownPositionRowsAreInNameOrder()
    {
        var groups = PlanEngineers.For(
            IncreasedFsdRange5,
            grade: 5,
            Progress(("Elvira Martuuk", 5), ("Mel Brandon", 5), ("Felicity Farseer", 5)),
            from: null);

        Assert.Equal(
            ["Elvira Martuuk", "Felicity Farseer", "Mel Brandon"],
            groups.Unlocked.Select(entry => entry.Engineer.Name));
    }

    /// <summary>Before EngineerProgress has ever been read, nothing is grouped at all.</summary>
    [Fact]
    public void BeforeProgressIsKnownNothingIsGrouped()
    {
        var groups = PlanEngineers.For(IncreasedFsdRange5, grade: 5, EngineerProgressState.Empty, from: null);

        Assert.False(groups.ProgressKnown);
        Assert.Empty(groups.Unlocked);
        Assert.Empty(groups.Locked);
        Assert.Equal(3, groups.All.Count);
    }

    [Fact]
    public void WithNoProgressAtAllNothingIsGroupedEither()
    {
        var groups = PlanEngineers.For(IncreasedFsdRange5, grade: 5, progress: null, from: null);

        Assert.False(groups.ProgressKnown);
        Assert.Equal(3, groups.All.Count);
    }
}
