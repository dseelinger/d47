using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// Reordering by voice, and the selection that makes "it" mean something (reported 2026-08-21).
/// <para>
/// The report was a transcript: <i>"Ordering I cannot set — I have no tool that moves an item to
/// the top"</i>, and then <i>"I have no tool that reorders your checklist — no selection, no
/// move"</i>. Both sentences were true and neither had to be.
/// </para>
/// <para>
/// <b>The boundary is not what was missing.</b> Writing to the Commander's own list stays off the
/// tool surface — the order is their answer to what they are working on next, which is not a thing
/// an in-game message gets to rearrange. What was missing is the route that boundary has always
/// allowed: declared phrases through the model-free router, which is how accepting and declining
/// already work.
/// </para>
/// </summary>
public class OrderingIsReachableWithoutTheModelTests
{
    /// <summary>
    /// A service over a throwaway install. The <see cref="TempInstall"/> is deliberately not
    /// disposed: nothing here reads the files back, and each test wants its own empty list.
    /// </summary>
    private static ChecklistService Checklists()
    {
        var install = new TempInstall();

        return new ChecklistService(
            new ChecklistStore(
                Path.Combine(install.Paths.Data, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);
    }

    private static ChecklistService Seeded(params string[] lines)
    {
        var checklists = Checklists();

        foreach (var line in lines)
        {
            checklists.AddNote(ChecklistScope.Universal, line);
        }

        return checklists;
    }

    private static IReadOnlyList<string> Order(ChecklistService checklists) =>
        [.. checklists.Document.Items.Where(item => item.IsLive).Select(item => item.Text)];

    // ------------------------------------------------------------- the selection

    /// <summary>
    /// The second half of the report: <i>"The just-added item should be the currently selected
    /// one."</i> A Commander who has just said a line is thinking about that line.
    /// </summary>
    [Fact]
    public void TheAddedLineIsTheSelectedOne()
    {
        var checklists = Seeded("buy limpets", "refill manufactured materials");

        Assert.Equal(
            "refill manufactured materials",
            checklists.Document.Find(checklists.Selected!.Value)?.Text);
    }

    /// <summary>
    /// Which is what makes the transcript work end to end: a line, and then "put it at the top"
    /// in the next breath, with nothing named in between.
    /// </summary>
    [Fact]
    public void AndPutItAtTheTopThenMeansTheLineJustAdded()
    {
        var checklists = Seeded("buy limpets", "sell the cargo", "refill manufactured materials");

        Assert.True(checklists.Move(phrase: null, ChecklistMove.Top).Changed);

        Assert.Equal(
            ["refill manufactured materials", "buy limpets", "sell the cargo"],
            Order(checklists));
    }

    /// <summary>
    /// A line taken off the list takes the selection with it, rather than leaving an id nothing
    /// answers to for the next "move it up" to act on.
    /// </summary>
    [Fact]
    public void DeletingTheSelectedLineClearsTheSelection()
    {
        var checklists = Seeded("buy limpets");
        var id = checklists.Selected!.Value;

        Assert.True(checklists.Delete(id).Changed);
        Assert.Null(checklists.Selected);
    }

    /// <summary>
    /// And with nothing selected, "it" is refused in words that say what to say instead. A
    /// Commander told only that they were wrong has been told nothing.
    /// </summary>
    [Fact]
    public void WithNothingSelectedTheRefusalNamesTheWayOut()
    {
        var checklists = Seeded("buy limpets");
        checklists.Select(null);

        var change = checklists.Move(phrase: null, ChecklistMove.Up);

        Assert.False(change.Changed);
        Assert.Contains("No line is selected", change.Report, StringComparison.Ordinal);
        Assert.Contains("Checklist tab", change.Report, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------- the ends

    [Fact]
    public void ToTheTopIsOnePressFromAnywhere()
    {
        var checklists = Seeded("one", "two", "three", "four");
        var last = checklists.Selected!.Value;

        Assert.True(checklists.Move(last, ChecklistMove.Top).Changed);
        Assert.Equal(["four", "one", "two", "three"], Order(checklists));
    }

    [Fact]
    public void AndToTheBottomTheOtherWay()
    {
        var checklists = Seeded("one", "two", "three", "four");
        var first = checklists.Document.Items[0].Id;

        var change = checklists.Move(first, ChecklistMove.Bottom);

        Assert.True(change.Changed);
        Assert.Equal(["two", "three", "four", "one"], Order(checklists));
        Assert.Equal("Moved \"one\" to the bottom.", change.Report);
    }

    /// <summary>
    /// An item already at the end it was sent to is told which end that is. "Is already where it
    /// is" would be true of every item on the list and would answer nothing.
    /// </summary>
    [Fact]
    public void AnItemAlreadyAtTheEndSaysWhichEnd()
    {
        var checklists = Seeded("one", "two");
        var first = checklists.Document.Items[0].Id;

        Assert.Equal("\"one\" is already at the top.", checklists.Move(first, ChecklistMove.Top).Report);
        Assert.Equal("\"two\" is already at the bottom.", checklists.Move(checklists.Selected!.Value, ChecklistMove.Bottom).Report);
    }

    /// <summary>
    /// Tombstones stay exactly where they are through an end move, for the same reason they do
    /// through a step: this reorders the Commander's work, never their history.
    /// </summary>
    [Fact]
    public void AnEndMoveLeavesTheHistoryWhereItWas()
    {
        var checklists = Seeded("one", "two", "three");
        var dropped = checklists.Document.Items[1].Id;

        Assert.True(checklists.Delete(dropped).Changed);

        var before = checklists.Document.Items.Count;
        var last = checklists.Document.Items.Last(item => item.IsLive).Id;

        Assert.True(checklists.Move(last, ChecklistMove.Top).Changed);

        Assert.Equal(before, checklists.Document.Items.Count);
        Assert.Equal(["three", "one"], Order(checklists));
    }

    /// <summary>The named form, for a line that is not the selected one.</summary>
    [Fact]
    public void APhraseCanNameTheLineInstead()
    {
        var checklists = Seeded("buy limpets", "sell the cargo", "refuel");

        Assert.True(checklists.Move("buy limpets", ChecklistMove.Bottom).Changed);
        Assert.Equal(["sell the cargo", "refuel", "buy limpets"], Order(checklists));
    }

    // ----------------------------------------------------------------- the route

    /// <summary>
    /// The tool stays off the surface the model can see. This is the invariant the fix must not
    /// have spent to buy the feature.
    /// </summary>
    [Fact]
    public void TheMoveToolIsUnreachableFromTheModel()
    {
        var move = ChecklistCapability.Create(Checklists()).Tools
            .Single(tool => tool.Name == "move_checklist_item");

        Assert.True(move.Protected);
    }

    /// <summary>
    /// And it is reachable without one. Each phrase carries the end it means, so the router hands
    /// the handler an argument rather than the handler parsing the sentence.
    /// </summary>
    [Theory]
    [InlineData("move it up", "up")]
    [InlineData("move the currently selected checklist item up", "up")]
    [InlineData("move that down", "down")]
    [InlineData("move it to the top", "top")]
    [InlineData("put it at the top", "top")]
    [InlineData("put that at the bottom", "bottom")]
    public void EveryDeclaredPhraseCarriesTheEndItMeans(string phrase, string end)
    {
        var commands = ChecklistCapability.Create(Checklists()).Tools
            .Single(tool => tool.Name == "move_checklist_item")
            .Commands;

        var command = Assert.Single(
            commands,
            candidate => string.Equals(candidate.Phrase, phrase, StringComparison.Ordinal));

        Assert.Equal(end, command.Arguments["to"]);
    }

    /// <summary>
    /// A phrase is never a bare word. "top" alone would fire on a sentence about the top of a
    /// gravity well, which is the rule the router is phrase-level for.
    /// </summary>
    [Fact]
    public void NoMovePhraseIsASingleWord()
    {
        var commands = ChecklistCapability.Create(Checklists()).Tools
            .Single(tool => tool.Name == "move_checklist_item")
            .Commands;

        Assert.NotEmpty(commands);

        foreach (var command in commands)
        {
            Assert.Contains(' ', command.Phrase);
        }
    }
}
