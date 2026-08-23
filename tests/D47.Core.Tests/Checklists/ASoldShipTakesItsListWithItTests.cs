using D47.Core.Checklists;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// Selling a ship clears the list that was about it (change-requests.md 27).
/// <para>
/// <b>Deleted rather than reset, and the corpus settled that rather than taste.</b> The request
/// offered a second option — put the items back to Open and add a "Purchase X" line — and it is
/// not buildable on this scope, because Frontier reissues <c>ShipID</c>: of 55 distinct ships sold
/// across the 925-journal corpus, 17 had their id come back alive afterwards, one as a
/// <c>ShipyardNew</c> three days later. A list left keyed to that id attaches itself to a
/// different ship.
/// </para>
/// </summary>
public class ASoldShipTakesItsListWithItTests
{
    private const int Sold = 51;

    private const int Kept = 53;

    private static ChecklistService Loaded(TempInstall install)
    {
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.AddNote(ChecklistScope.Ship(Sold), "Grade 5 Dirty Drives");
        checklists.AddNote(ChecklistScope.Ship(Sold), "Swap the shield generator");
        checklists.AddNote(ChecklistScope.Ship(Kept), "Grade 3 Long Range");
        checklists.AddNote(ChecklistScope.Universal, "Buy limpets");

        return checklists;
    }

    [Fact]
    public void ItsLinesGoAndNobodyElsesDo()
    {
        using var install = new TempInstall();
        var checklists = Loaded(install);

        var news = checklists.ShipSold(Sold);

        Assert.NotNull(news);
        Assert.Empty(checklists.Document.In(ChecklistScope.Ship(Sold)));

        // The ship still in the fleet, and the lines about no ship at all, are untouched.
        Assert.Single(checklists.Document.In(ChecklistScope.Ship(Kept)));
        Assert.Single(checklists.Document.In(ChecklistScope.Universal));
    }

    /// <summary>
    /// It says what it did. Two items disappearing silently from a list somebody spent an evening
    /// on is the same event as a bug, from where the Commander is sitting.
    /// </summary>
    [Fact]
    public void ItSaysHowManyItCleared()
    {
        using var install = new TempInstall();
        var checklists = Loaded(install);

        var news = checklists.ShipSold(Sold);

        Assert.NotNull(news);
        Assert.Contains("2 items", news.Text, StringComparison.Ordinal);

        // And onto the queue the callout drains, so it is spoken like anything else the list says
        // and can be silenced the same way.
        Assert.Contains(checklists.Drain(), waiting => waiting.Key == news.Key);
    }

    /// <summary>Selling a ship with nothing planned for it says nothing.</summary>
    [Fact]
    public void AShipWithNoListSaysNothing()
    {
        using var install = new TempInstall();
        var checklists = Loaded(install);

        Assert.Null(checklists.ShipSold(99));
        Assert.Single(checklists.Document.In(ChecklistScope.Ship(Kept)));
    }

    /// <summary>
    /// And it is gone from the file rather than only from the copy in hand — the same claim the
    /// removal tests make, for the same reason: a store that reported a deletion it had not
    /// written looks identical to a working one until the next restart.
    /// </summary>
    [Fact]
    public void TheLinesAreGoneFromTheFileToo()
    {
        using var install = new TempInstall();
        var checklists = Loaded(install);

        checklists.ShipSold(Sold);

        var reopened = TestSurface.Checklists(install.Paths);

        // Read off disk rather than taken on trust: a fresh service has not loaded the file until
        // its store is polled, and asserting against an unloaded one would pass for both ships.
        reopened.List.Poll();

        Assert.Empty(reopened.Document.In(ChecklistScope.Ship(Sold)));
        Assert.Single(reopened.Document.In(ChecklistScope.Ship(Kept)));
    }
}
