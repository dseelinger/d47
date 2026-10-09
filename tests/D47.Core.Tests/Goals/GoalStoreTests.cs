using D47.Core.Storage;
using D47.Core.Goals;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Goals;

/// <summary>The arcs on disk.</summary>
public class GoalStoreTests
{
    private static readonly DateTimeOffset Now = new(3311, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly MemoryFileSystem _files = new();

    private const string GoalsFile = "goals.json";

    [Fact]
    public void AMineRoundTripsThroughTheFile()
    {
        var store = Store();

        store.Record([Mine("F1", new GoalMark { Key = "rank.combat", Have = 4, Started = Now.AddDays(-90), AsOf = Now })]);

        var reread = Store();
        reread.Poll();

        var mark = reread.MineFor("F1")?.For("rank.combat");

        Assert.Equal(4, mark?.Have);
        Assert.Equal(Now.AddDays(-90), mark?.Started);
        Assert.Empty(reread.Problems);
    }

    [Fact]
    public void TwoCommandersAreKeptApartInOneFile()
    {
        var store = Store();

        store.Record(
        [
            Mine("F1", new GoalMark { Key = "rank.trade", Have = 10 }),
            Mine("F2", new GoalMark { Key = "rank.trade", Have = 4_000 }),
        ]);

        Assert.Equal(10, store.MineFor("F1")?.For("rank.trade")?.Have);
        Assert.Equal(4_000, store.MineFor("F2")?.For("rank.trade")?.Have);
    }

    /// <summary>The decision that has to outlive a recomputation.</summary>
    [Fact]
    public void SettingAnArcAsideSurvivesAReMine()
    {
        var store = Store();

        store.SetAside("F1", "rank.soldier", aside: true);
        store.Record([Mine("F1", new GoalMark { Key = "rank.soldier", Have = 0 })]);

        Assert.Contains("rank.soldier", store.SetAsideBy("F1"), StringComparer.Ordinal);

        // And emptying everything mined leaves it standing, for the same reason.
        store.Empty();
        Assert.Contains("rank.soldier", store.SetAsideBy("F1"), StringComparer.Ordinal);
    }

    [Fact]
    public void BringingAnArcBackIsTheSameActInTheOtherDirection()
    {
        var store = Store();

        Assert.True(store.SetAside("F1", "rank.soldier", aside: true));
        Assert.False(store.SetAside("F1", "rank.soldier", aside: true));
        Assert.True(store.SetAside("F1", "rank.soldier", aside: false));
        Assert.Empty(store.SetAsideBy("F1"));
    }

    [Fact]
    public void AnAuthoredArcSurvivesAReMineAndReadsBackWithItsDate()
    {
        var store = Store();

        Assert.Null(store.Author("F1", Authored("mine.see-the-galaxy", "See the galaxy")));
        store.Record([Mine("F1", new GoalMark { Key = "rank.trade", Have = 3 })]);

        var reread = Store();
        reread.Poll();

        var arc = Assert.Single(reread.AuthoredBy("F1"));

        Assert.Equal("See the galaxy", arc.Name);
        Assert.Equal(GoalKind.Authored, arc.Kind);
        Assert.Equal(Now, arc.Written);
    }

    [Fact]
    public void AGoalCannotTakeTheKeyOfOneThatShips()
    {
        var store = Store();

        Assert.NotNull(store.Author("F1", Authored(GoalCatalogue.Engineers, "Mine, honestly")));
    }

    [Fact]
    public void AGoalCannotBeAuthoredTwice()
    {
        var store = Store();

        Assert.Null(store.Author("F1", Authored("mine.one", "One")));
        Assert.NotNull(store.Author("F1", Authored("mine.one", "One again")));
    }

    [Fact]
    public void ForgettingRemovesAnAuthoredArcAndReportsWhenThereWasNothingToRemove()
    {
        var store = Store();

        store.Author("F1", Authored("mine.one", "One"));

        Assert.True(store.Forget("F1", "mine.one"));
        Assert.False(store.Forget("F1", "mine.one"));
        Assert.Empty(store.AuthoredBy("F1"));
    }

    /// <summary>
    /// The file is meant to be hand-editable, so a line that cannot be read back is reported rather
    /// than silently dropped — the contract every store in this repo has.
    /// </summary>
    [Fact]
    public void ABadGoalIsRefusedByNameAndTheRestAreKept()
    {
        _files.WriteText(GoalsFile, """
            {
              "commanders": [
                {
                  "frontierId": "F1",
                  "goals": [
                    { "key": "mine.good", "name": "A real one" },
                    { "key": "mine.bad" }
                  ]
                }
              ]
            }
            """);

        var store = new GoalStore(GoalsFile, _files, NullLogger<GoalStore>.Instance);
        store.Poll();

        Assert.Single(store.AuthoredBy("F1"));
        Assert.Contains(store.Problems, problem => problem.Where == "mine.bad");
    }

    /// <summary>
    /// A record carrying only a person's own goals is a real state — nothing has been walked yet — and
    /// it must not read back as a mining run that found nothing.
    /// </summary>
    [Fact]
    public void GoalsWithoutAMineDoNotInventOne()
    {
        var store = Store();

        store.Author("F1", Authored("mine.one", "One"));

        var reread = Store();
        reread.Poll();

        Assert.Null(reread.MineFor("F1"));
        Assert.Single(reread.AuthoredBy("F1"));
    }

    private GoalStore Store() =>
        new(GoalsFile, _files, NullLogger<GoalStore>.Instance);

    private static GoalArc Authored(string key, string name) =>
        new() { Key = key, Name = name, Done = "Yours to call finished.", Kind = GoalKind.Authored, Written = Now };

    private static GoalMine Mine(string fid, params GoalMark[] marks) =>
        new() { FrontierId = fid, MinedAt = Now, Journals = 12, Marks = marks };
}
