using D47.Core.Checklists;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// The Commander's own order (list.md Phase 25, "The checklist leaves its window").
/// <para>
/// <b>One order across every scope.</b> What gets reordered on a whim is everything being worked
/// on, not one ship's share of it — so scope is a label and a filter and never a partition, and
/// moving up means past whatever is above regardless of which list it would have been in.
/// </para>
/// </summary>
public class ChecklistOrderTests
{
    private static ChecklistDocument Three()
    {
        var document = ChecklistDocument.For("fid");

        document = document.AddNote(ChecklistScope.Universal, "buy limpets").Document;
        document = document.AddNote(ChecklistScope.Ship(12), "fit a fuel scoop").Document;
        document = document.AddNote(ChecklistScope.Universal, "sell the cargo").Document;

        return document;
    }

    private static IEnumerable<string> Order(ChecklistDocument document) =>
        document.Items.Where(item => item.IsLive).Select(item => item.Text);

    [Fact]
    public void MovingUpCrossesScopes()
    {
        var document = Three();
        var scoop = document.Items[1].Id;

        var change = document.Move(scoop, -1);

        Assert.True(change.Changed);
        Assert.Equal(["fit a fuel scoop", "buy limpets", "sell the cargo"], Order(change.Document));
    }

    [Fact]
    public void MovingDownDoesTheSame()
    {
        var document = Three();
        var limpets = document.Items[0].Id;

        var change = document.Move(limpets, 1);

        Assert.Equal(["fit a fuel scoop", "buy limpets", "sell the cargo"], Order(change.Document));
    }

    /// <summary>An end is an end, and it says so rather than silently doing nothing.</summary>
    [Fact]
    public void AnItemAtAnEndSaysSo()
    {
        var document = Three();

        var top = document.Move(document.Items[0].Id, -1);
        var bottom = document.Move(document.Items[2].Id, 1);

        Assert.False(top.Changed);
        Assert.Contains("already at the top", top.Report, StringComparison.Ordinal);

        Assert.False(bottom.Changed);
        Assert.Contains("already at the bottom", bottom.Report, StringComparison.Ordinal);
    }

    /// <summary>A step past the end lands on the end rather than being refused.</summary>
    [Fact]
    public void AStepPastTheEndClamps()
    {
        var document = Three();

        var change = document.Move(document.Items[0].Id, 9);

        Assert.True(change.Changed);
        Assert.Equal(["fit a fuel scoop", "sell the cargo", "buy limpets"], Order(change.Document));
    }

    /// <summary>
    /// Tombstones stay exactly where they are. This is a reordering of the Commander's work, not
    /// of their history — the record of what a plan dropped has no position to be moved to.
    /// </summary>
    [Fact]
    public void TombstonesDoNotMoveAndAreNotSteppedOnto()
    {
        var document = Three();

        // A tombstone in the middle, as a revision leaves one.
        var items = document.Items.ToList();
        items.Insert(1, items[1] with { Tombstone = ChecklistTombstone.Abandoned, Key = "gone" });
        document = document with { Items = items };

        var before = document.Items.Select(item => item.Tombstone).ToList();

        var change = document.Move(document.Items[0].Id, 1);

        Assert.True(change.Changed);

        // The live items swapped; the tombstone is still the second entry.
        Assert.Equal(["fit a fuel scoop", "buy limpets", "sell the cargo"], Order(change.Document));
        Assert.Equal(before, change.Document.Items.Select(item => item.Tombstone));
    }

    /// <summary>A record of what a plan dropped is not something to work on, and says why.</summary>
    [Fact]
    public void ATombstoneCannotBeMoved()
    {
        var document = Three();

        var items = document.Items.ToList();
        items[0] = items[0] with { Tombstone = ChecklistTombstone.Abandoned };
        document = document with { Items = items };

        var change = document.Move(document.Items[0].Id, 1);

        Assert.False(change.Changed);
        Assert.Contains("dropped by a later version", change.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void AnItemThatIsNotThereIsSaidRatherThanIgnored()
    {
        var change = Three().Move(new ChecklistItemId(ChecklistScope.Universal, "nothing"), -1);

        Assert.False(change.Changed);
        Assert.Contains("no such item", change.Report, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The states past Open are next actions rather than badges, which is what
    /// <see cref="ChecklistState"/> asks for in its own summary.
    /// </summary>
    [Theory]
    [InlineData(ChecklistState.Elsewhere, "Transfer it")]
    [InlineData(ChecklistState.Blocked, "rank with the engineer")]
    [InlineData(ChecklistState.Unverified, "will not claim it")]
    [InlineData(ChecklistState.Stale, "not there any more")]
    public void EveryStatePastOpenIsANextAction(ChecklistState state, string expected)
    {
        Assert.Contains(expected, ChecklistNextAction.For(state), StringComparison.Ordinal);
    }

    /// <summary>
    /// And the ordinary states say nothing, because a line per item saying "open" is a line the
    /// Commander reads once and then stops reading.
    /// </summary>
    [Theory]
    [InlineData(ChecklistState.Open)]
    [InlineData(ChecklistState.Done)]
    public void TheOrdinaryStatesSayNothing(ChecklistState state)
    {
        Assert.Null(ChecklistNextAction.For(state));
    }

    /// <summary>
    /// Colour is spent only where something is wrong. A page where six things are coloured is a
    /// page where none of them is noticed — and Open is the ordinary condition of every item on a
    /// list that runs for weeks.
    /// </summary>
    [Theory]
    [InlineData(ChecklistState.Open, false)]
    [InlineData(ChecklistState.Done, false)]
    [InlineData(ChecklistState.Elsewhere, false)]
    [InlineData(ChecklistState.Blocked, false)]
    [InlineData(ChecklistState.Unverified, true)]
    [InlineData(ChecklistState.Stale, true)]
    public void OnlyBeingWrongEarnsColour(ChecklistState state, bool wrong)
    {
        Assert.Equal(wrong, ChecklistNextAction.IsWrong(state));
    }
}
