using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Engineers directory is a table; each header but Portrait sorts it.</summary>
public class AnEngineerTableSortsFromItsHeadersTests
{
    private const int StatusColumn = 2;
    private const int DistanceColumn = 6;

    private static EngineerEntry Entry(string name, EngineerReach reach, double? lightYears = null, int? rank = null) =>
        new()
        {
            Engineer = EngineerDirectory.ByName(name)!,
            Reach = reach,
            LightYears = lightYears,
            Standing = rank is { } grade ? new EngineerStanding(1, name, "Unlocked") { Rank = grade } : null,
        };

    private static IReadOnlyList<EngineerEntry> Roster() =>
    [
        Entry("Hera Tani", EngineerReach.WithinReach, 12),
        Entry("Elvira Martuuk", EngineerReach.WithinReach),
        Entry("Lei Cheung", EngineerReach.Unlocked, 500, 3),
        Entry("Felicity Farseer", EngineerReach.Locked, 300),
    ];

    private static string[] Sorted(SortOrder? order) =>
        [.. ColumnSort.Sorted(Roster(), EngineerDirectoryPage.Heads(), order).Select(entry => entry.Engineer.Name)];

    [Fact]
    public void StatusPutsReadyThenUnlockedThenNeedsAReferralAndKeepsTheDirectoryOrderWithinEach()
    {
        var shuffled = new[] { Roster()[3], Roster()[2], Roster()[0], Roster()[1] };

        var sorted = ColumnSort.Sorted(shuffled, EngineerDirectoryPage.Heads(), EngineerDirectoryPage.DefaultOrder);

        Assert.Equal(["Hera Tani", "Elvira Martuuk", "Lei Cheung", "Felicity Farseer"], sorted.Select(entry => entry.Engineer.Name));
    }

    [Fact]
    public void TheDefaultOrderIsStatusAscendingAndPortraitDoesNotSort()
    {
        Assert.Equal(new SortOrder(StatusColumn, SortDirection.Ascending), EngineerDirectoryPage.DefaultOrder);
        Assert.False(EngineerDirectoryPage.Heads()[0].Sortable);
        Assert.All(EngineerDirectoryPage.Heads().Skip(1), column => Assert.True(column.Sortable));
    }

    [Fact]
    public void DistanceListsNearestFirstAndAnUnknownDistanceLast()
    {
        Assert.Equal(
            ["Hera Tani", "Felicity Farseer", "Lei Cheung", "Elvira Martuuk"],
            Sorted(new SortOrder(DistanceColumn, SortDirection.Ascending)));
    }

    [Fact]
    public void ADistanceSortStillPutsAnUnknownDistanceLastWhenReversed()
    {
        Assert.Equal(
            ["Lei Cheung", "Felicity Farseer", "Hera Tani", "Elvira Martuuk"],
            Sorted(new SortOrder(DistanceColumn, SortDirection.Descending)));
    }

    [Fact]
    public void ThreePressesOnAHeaderSortReverseAndReturnToTheDefaultOrder()
    {
        var head = new SortHeader<EngineerEntry>("EngineersHead", EngineerDirectoryPage.Heads(), EngineerDirectoryPage.DefaultOrder);

        head.Press(DistanceColumn);
        Assert.Equal(new SortOrder(DistanceColumn, SortDirection.Ascending), head.Order);

        head.Press(DistanceColumn);
        Assert.Equal(new SortOrder(DistanceColumn, SortDirection.Descending), head.Order);

        head.Press(DistanceColumn);
        Assert.Null(head.Order);
        Assert.Equal(Roster().Select(entry => entry.Engineer.Name), head.Sort(Roster()).Select(entry => entry.Engineer.Name));
    }

    [Fact]
    public void AnUnlockedEngineerReadsItsGradeOrJustUnlockedWhenTheJournalGaveNoRank()
    {
        Assert.Equal("Unlocked at grade 3", EngineerDirectoryPage.StatusText(Entry("Lei Cheung", EngineerReach.Unlocked, rank: 3)));
        Assert.Equal("Unlocked", EngineerDirectoryPage.StatusText(Entry("Lei Cheung", EngineerReach.Unlocked)));
        Assert.Equal("Ready for Unlock", EngineerDirectoryPage.StatusText(Entry("Hera Tani", EngineerReach.WithinReach)));
        Assert.Equal("Needs a Referral", EngineerDirectoryPage.StatusText(Entry("Hera Tani", EngineerReach.Locked)));
    }

    [Fact]
    public void PlannedAndOpensSortWithEmptyCellsLast()
    {
        var planned = Roster().Select((entry, index) => entry with { Wanted = index == 1 ? 2 : index == 3 ? 5 : 0 }).ToList();
        var column = EngineerDirectoryPage.Heads().Select((head, index) => (head, index)).First(pair => pair.head.Label == "Planned").index;

        var sorted = ColumnSort.Sorted(planned, EngineerDirectoryPage.Heads(), new SortOrder(column, SortDirection.Descending));

        Assert.Equal(["Felicity Farseer", "Elvira Martuuk"], sorted.Take(2).Select(entry => entry.Engineer.Name));
    }
}
