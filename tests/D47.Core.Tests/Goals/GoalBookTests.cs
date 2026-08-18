using D47.Core.Checklists;
using D47.Core.Goals;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Goals;

/// <summary>
/// The join that makes an arc and a checklist worth having together (list.md Phase 34, "The
/// checklist points at the arc").
/// </summary>
public class GoalBookTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(3311, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-goal-book", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void EveryBuiltInArcIsOnThePageAndNoneOfThemIsCqc()
    {
        using var install = new TempInstall();
        var book = Book(install);

        Assert.Equal(9, book.Standings.Count);
        Assert.DoesNotContain(book.Standings, standing => standing.Arc.Key.Contains("cqc", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The general form of the CQC decision: a judgement about what somebody cares about is theirs
    /// rather than the page's.
    /// </summary>
    [Fact]
    public void ASetAsideArcLeavesThePageAndComesBack()
    {
        using var install = new TempInstall();
        var book = Book(install);

        book.SetAside("rank.soldier", aside: true);

        Assert.Equal(8, book.Standings.Count);
        Assert.DoesNotContain(book.Standings, standing => standing.Arc.Key == "rank.soldier");

        // Still reachable, which is what lets the panel offer bringing it back.
        Assert.Equal(9, book.Everything().Count);

        book.SetAside("rank.soldier", aside: false);
        Assert.Equal(9, book.Standings.Count);
    }

    /// <summary>
    /// <b>The assertion item 3 is about.</b> A line promoted from an arc says which arc it came
    /// from — without it, finishing the line visibly moves nothing bigger than itself.
    /// </summary>
    [Fact]
    public void APromotedLineSaysWhichArcItCameFrom()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);
        var book = Book(install, checklists);

        var said = book.Promote(GoalCatalogue.Ships);

        Assert.Contains("ship collection", said, StringComparison.OrdinalIgnoreCase);

        var proposal = Assert.Single(checklists.Proposals.Pending);
        var item = Assert.Single(proposal.Items);

        Assert.Equal(GoalCatalogue.Ships, item.Goal);

        // And it survives being accepted, because provenance a Commander loses on the way onto
        // their own list is provenance they never had.
        checklists.Accept();
        Assert.Equal(GoalCatalogue.Ships, Assert.Single(checklists.Document.Items).Goal);
    }

    /// <summary>
    /// Accepting stays the Commander's act. The arc writes the proposals file and never the list,
    /// which is the boundary <see cref="ChecklistProposalStore"/> already draws.
    /// </summary>
    [Fact]
    public void PromotingProposesRatherThanCommits()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);
        var book = Book(install, checklists);

        book.Promote(GoalCatalogue.Ships);

        Assert.Single(checklists.Proposals.Pending);
        Assert.Empty(checklists.Document.Items);
    }

    /// <summary>
    /// A rank arc proposes nothing and says why. Manufacturing a line here would be filler on a
    /// page whose credibility is all it has.
    /// </summary>
    [Fact]
    public void ACareerArcOffersNoLineAndNamesTheToolItDoesHave()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);
        var book = Book(install, checklists);

        var step = book.Next("rank.trade");

        Assert.NotNull(step);
        Assert.Empty(step.Lines);
        Assert.False(step.CanPropose);
        Assert.Contains("plot_trade_route", step.Say, StringComparison.Ordinal);

        book.Promote("rank.trade");
        Assert.Empty(checklists.Proposals.Pending);
    }

    [Fact]
    public void AGoalTheCommanderInventedIsTheirsToCallDone()
    {
        using var install = new TempInstall();
        var book = Book(install);

        book.Author("See the war out", "When it ends.", Now);

        var standing = Assert.Single(book.Standings, entry => entry.Arc.Name == "See the war out");

        Assert.Equal(GoalKind.Authored, standing.Arc.Kind);
        Assert.False(standing.IsDone);

        Assert.Contains("done", book.Finish("See the war out", finished: true, Now.AddDays(30)), StringComparison.OrdinalIgnoreCase);
        Assert.True(Assert.Single(book.Standings, entry => entry.Arc.Name == "See the war out").IsDone);
    }

    /// <summary>
    /// The same refusal <see cref="ChecklistDocument.Complete"/> gives about a derived line, for
    /// the same reason: the next journal read would either undo it or leave it standing and wrong.
    /// </summary>
    [Fact]
    public void ADerivedArcCannotBeTickedByHand()
    {
        using var install = new TempInstall();
        var book = Book(install);

        var said = book.Finish(GoalCatalogue.Engineers, finished: true, Now);

        Assert.Contains("rather than ticked", said, StringComparison.Ordinal);
        Assert.DoesNotContain(book.Standings, standing => standing.Arc.Key == GoalCatalogue.Engineers && standing.IsDone);
    }

    [Fact]
    public void ABuiltInArcIsSetAsideRatherThanDeleted()
    {
        using var install = new TempInstall();
        var book = Book(install);

        Assert.Contains("set aside rather than deleted", book.Forget(GoalCatalogue.Ships), StringComparison.Ordinal);
        Assert.Equal(9, book.Standings.Count);
    }

    [Fact]
    public void WithNothingMinedTheReadbackSaysSoRatherThanReportingNothing()
    {
        using var install = new TempInstall();

        var said = Book(install).Describe(Now);

        Assert.Contains("not read back through your journals", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Running", said, StringComparison.Ordinal);
    }

    private GoalBook Book(TempInstall install, ChecklistService? checklists = null)
    {
        var store = new GoalStore(
            Path.Combine(install.Paths.Data, "goals.json"),
            NullLogger<GoalStore>.Instance);

        store.Poll();

        return new GoalBook(store, () => "F1", () => null, checklists);
    }
}
