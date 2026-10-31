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
    public void AMalformedFileIsReportedAndKeepsWhatWasLoaded()
    {
        var store = Store();

        store.Record([Mine("F1", new GoalMark { Key = "rank.trade", Have = 3 })]);
        store.SetAside("F1", "rank.soldier", aside: true);

        _files.WriteText(GoalsFile, "{ not json");
        store.Poll();

        Assert.NotEmpty(store.Problems);
        Assert.Equal(3, store.MineFor("F1")?.For("rank.trade")?.Have);
        Assert.Contains("rank.soldier", store.SetAsideBy("F1"), StringComparer.Ordinal);
    }

    [Fact]
    public void AFileWithOnlySetAsideEntriesLoadsThemAndAGoalsArrayIsDroppedOnSave()
    {
        _files.WriteText(GoalsFile, """
            {
              "commanders": [
                {
                  "frontierId": "F1",
                  "goals": [ { "key": "mine.old", "name": "An old one" } ],
                  "setAside": [ "rank.soldier" ]
                }
              ]
            }
            """);

        var store = Store();
        store.Poll();

        Assert.Empty(store.Problems);
        Assert.Contains("rank.soldier", store.SetAsideBy("F1"), StringComparer.Ordinal);

        store.SetAside("F1", "rank.trade", aside: true);

        var saved = _files.ReadText(GoalsFile);

        Assert.DoesNotContain("goals", saved, StringComparison.Ordinal);
        Assert.Contains("rank.soldier", saved, StringComparison.Ordinal);
        Assert.Contains("rank.trade", saved, StringComparison.Ordinal);
    }

    private GoalStore Store() =>
        new(GoalsFile, _files, NullLogger<GoalStore>.Instance);

    private static GoalMine Mine(string fid, params GoalMark[] marks) =>
        new() { FrontierId = fid, MinedAt = Now, Journals = 12, Marks = marks };
}
