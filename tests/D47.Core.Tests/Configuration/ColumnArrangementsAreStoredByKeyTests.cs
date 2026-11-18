using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class ColumnArrangementsAreStoredByKeyTests
{
    private readonly MemoryFileSystem _files = new();

    private static readonly ArrangedColumn[] Columns =
    [
        new("name", Required: true),
        new("plan"),
        new("current"),
        new("cost", ShownByDefault: false),
    ];

    private ViewStateStore Store() => new(new AppPaths("C:/d47-arrange"), _files, NullLogger<ViewStateStore>.Instance);

    [Fact]
    public void AFileWrittenBeforeArrangementsLoadsWithNone()
    {
        var state = JsonSerializer.Deserialize<ViewState>("{\"columnWidths\":{}}");

        Assert.Empty(state!.ColumnArrangements);
    }

    [Fact]
    public void AnArrangementIsReadBackInOrderAndForgettingItLeavesTheOthers()
    {
        var store = Store();
        store.Save(new ViewState().WithArrangement("fleet", ["plan", "name"]).WithArrangement("fleet@headset", ["name"]));

        var loaded = store.Load();
        Assert.Equal(["plan", "name"], loaded.ColumnArrangements["fleet"]);

        var forgotten = loaded.WithoutArrangement("fleet");
        Assert.False(forgotten.ColumnArrangements.ContainsKey("fleet"));
        Assert.Equal(["name"], forgotten.ColumnArrangements["fleet@headset"]);
    }

    [Fact]
    public void TheHeadsetKeepsItsOwnArrangementId()
    {
        Assert.Equal("fleet", ColumnArrangement.IdFor("fleet", headset: false));
        Assert.Equal("fleet@headset", ColumnArrangement.IdFor("fleet", headset: true));
    }

    [Fact]
    public void NothingStoredShowsTheDefaultColumns()
    {
        Assert.Equal(["name", "plan", "current"], ColumnArrangement.Shown(Columns, null));
        Assert.Equal(["name", "plan", "current"], ColumnArrangement.Shown(Columns, []));
    }

    [Fact]
    public void StoredKeysAreShownInStoredOrder()
    {
        Assert.Equal(["cost", "name", "plan"], ColumnArrangement.Shown(Columns, ["cost", "name", "plan"]));
    }

    [Fact]
    public void AKeyTheTableCannotShowIsSkippedAndStaysStored()
    {
        Assert.Equal(["name", "plan"], ColumnArrangement.Shown(Columns, ["gone", "name", "plan"]));

        var edited = ColumnArrangement.Added(Columns, ["gone", "name", "plan"], "cost");
        Assert.Equal(["name", "plan", "cost", "gone"], edited);
    }

    [Fact]
    public void ARequiredColumnTheStoredListLacksIsPlacedAfterItsDefaultPredecessor()
    {
        var columns = new ArrangedColumn[] { new("a"), new("b", Required: true), new("c"), new("d", Required: true) };

        Assert.Equal(["c", "d", "a", "b"], ColumnArrangement.Shown(columns, ["c", "a"]));
        Assert.Equal(["b", "c", "d"], ColumnArrangement.Shown(columns, ["c"]));
    }

    [Fact]
    public void ARequiredColumnWithNoShownPredecessorGoesFirst()
    {
        Assert.Equal(["name", "current"], ColumnArrangement.Shown(Columns, ["current"]));
    }

    [Fact]
    public void AnEmptyResultIsTheDefaultColumns()
    {
        var optional = new ArrangedColumn[] { new("a"), new("b") };

        Assert.Equal(["a", "b"], ColumnArrangement.Shown(optional, ["gone"]));
    }

    [Fact]
    public void MovedSwapsWithTheNearestShownColumnOnThatSide()
    {
        Assert.Equal(["name", "current", "plan"], ColumnArrangement.Moved(Columns, null, "plan", left: false));
        Assert.Equal(["plan", "name", "current"], ColumnArrangement.Moved(Columns, null, "plan", left: true));
    }

    [Fact]
    public void MovedAtTheEdgeChangesNothing()
    {
        Assert.Equal(["name", "plan", "current"], ColumnArrangement.Moved(Columns, null, "name", left: true));
        Assert.Equal(["name", "plan", "current"], ColumnArrangement.Moved(Columns, null, "current", left: false));
    }

    [Fact]
    public void AddedPutsTheKeyLast()
    {
        Assert.Equal(["name", "plan", "current", "cost"], ColumnArrangement.Added(Columns, null, "cost"));
        Assert.Equal(["name", "plan", "current"], ColumnArrangement.Added(Columns, null, "plan"));
    }

    [Fact]
    public void RemovedKeepsARequiredColumnAndTheLastShownColumn()
    {
        Assert.Equal(["name", "plan", "current"], ColumnArrangement.Removed(Columns, null, "name"));
        Assert.Equal(["name", "current"], ColumnArrangement.Removed(Columns, null, "plan"));

        var optional = new ArrangedColumn[] { new("a"), new("b") };
        Assert.Equal(["b"], ColumnArrangement.Removed(optional, ["b"], "b"));
    }

    [Fact]
    public void IsDefaultIsTrueOnlyForTheDefaultColumnsInOrder()
    {
        Assert.True(ColumnArrangement.IsDefault(Columns, null));
        Assert.True(ColumnArrangement.IsDefault(Columns, ["name", "plan", "current"]));
        Assert.False(ColumnArrangement.IsDefault(Columns, ["name", "current", "plan"]));
        Assert.False(ColumnArrangement.IsDefault(Columns, ["name", "plan"]));
    }
}
