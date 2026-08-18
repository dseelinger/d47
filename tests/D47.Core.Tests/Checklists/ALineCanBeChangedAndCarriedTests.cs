using D47.Core.Checklists;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// Rewording a line, and moving a whole list to another machine (remediation.md 10, items 13
/// and 15).
/// </summary>
public class ALineCanBeChangedAndCarriedTests
{
    private static (ChecklistService Checklists, ChecklistItem Item) One(TempInstall install, string line)
    {
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.AddNote(ChecklistScope.Universal, line);

        return (checklists, checklists.Document.In(ChecklistScope.Universal)[0]);
    }

    [Fact]
    public void ALineTheCommanderWroteCanBeReworded()
    {
        using var install = new TempInstall();
        var (checklists, item) = One(install, "Unlockly Chung");

        var change = checklists.Reword(item.Id, "Unlock Lei Cheung");

        Assert.True(change.Changed);
        Assert.Equal("Unlock Lei Cheung", checklists.Document.In(ChecklistScope.Universal)[0].Text);
    }

    /// <summary>
    /// The key does not move. An item is cited by key — by the arc that proposed it, by a
    /// tombstone, by a proposal already waiting — so a reword that minted a new one would be a
    /// delete and an add wearing a correction's clothes.
    /// </summary>
    [Fact]
    public void RewordingKeepsTheLinesIdentityAndItsTick()
    {
        using var install = new TempInstall();
        var (checklists, item) = One(install, "buy limpets");

        checklists.Complete(item.Id);
        checklists.Reword(item.Id, "buy collector limpets");

        var after = checklists.Document.In(ChecklistScope.Universal)[0];

        Assert.Equal(item.Key, after.Key);
        Assert.True(after.IsComplete);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ALineWithNothingOnItIsRefused(string blank)
    {
        using var install = new TempInstall();
        var (checklists, item) = One(install, "buy limpets");

        Assert.False(checklists.Reword(item.Id, blank).Changed);
        Assert.Equal("buy limpets", checklists.Document.In(ChecklistScope.Universal)[0].Text);
    }

    /// <summary>
    /// A derived line's words are the plan's words. Rewriting them here would leave the list
    /// saying one thing and the plan another until the next journal read silently put the
    /// original back — the same failure ticking one already refuses.
    /// </summary>
    [Fact]
    public void ADerivedLineIsNotRewordedHere()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);
        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines") { Grade = 5 };

        checklists.List.Save(
        [
            ChecklistDocument.For(string.Empty) with
            {
                Items =
                [
                    new ChecklistItem
                    {
                        Key = ChecklistKeys.For(intent),
                        Scope = ChecklistScope.Ship(12),
                        Kind = ChecklistItemKind.Derived,
                        Source = ChecklistSource.EngineeringPlan,
                        Text = "Grade 5 on MainEngines",
                        Intent = intent,
                    },
                ],
            },
        ]);

        var item = checklists.Document.In(ChecklistScope.Ship(12))[0];
        var change = checklists.Reword(item.Id, "Grade 4 will do");

        Assert.False(change.Changed);
        Assert.Contains("came from a plan", change.Report, StringComparison.Ordinal);
        Assert.Equal("Grade 5 on MainEngines", checklists.Document.In(ChecklistScope.Ship(12))[0].Text);
    }

    /// <summary>
    /// The round trip, which is the whole of item 15: everything out, everything back, unchanged.
    /// </summary>
    [Fact]
    public void AnExportedListImportsBackAsItself()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.System("Sol"), "ask Jim about the Krait build");
        checklists.Complete(checklists.Document.In(ChecklistScope.Universal)[0].Id);

        var exported = checklists.Export();

        using var second = new TempInstall();
        var elsewhere = TestSurface.Checklists(second.Paths);

        Assert.True(elsewhere.Import(exported).Changed);

        var carried = elsewhere.Document.In(ChecklistScope.Universal);

        Assert.Equal("buy limpets", Assert.Single(carried).Text);
        Assert.True(carried[0].IsComplete);
        Assert.Equal(
            "ask Jim about the Krait build",
            Assert.Single(elsewhere.Document.In(ChecklistScope.System("Sol"))).Text);
    }

    /// <summary>
    /// A tombstone is part of "everything". It is the answer to "why did you stop tracking that"
    /// three weeks later, and a round trip that dropped it would import into something quietly
    /// different from what was exported.
    /// </summary>
    [Fact]
    public void WhatWasFinishedWithTravelsToo()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);
        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines") { Grade = 5 };

        checklists.Revise(
            ChecklistScope.Ship(12),
            ChecklistSource.EngineeringPlan,
            [
                new ChecklistItem
                {
                    Key = ChecklistKeys.For(intent),
                    Scope = ChecklistScope.Ship(12),
                    Kind = ChecklistItemKind.Derived,
                    Source = ChecklistSource.EngineeringPlan,
                    Text = "Grade 5 on MainEngines",
                    Intent = intent,
                },
            ]);

        // Revised to say nothing about that slot, which tombstones the line rather than losing it.
        checklists.Revise(ChecklistScope.Ship(12), ChecklistSource.EngineeringPlan, []);

        var before = checklists.Document.Items.Count;

        Assert.True(before > 0);

        using var second = new TempInstall();
        var elsewhere = TestSurface.Checklists(second.Paths);

        elsewhere.Import(checklists.Export());

        Assert.Equal(before, elsewhere.Document.Items.Count);
    }

    /// <summary>
    /// One bad line refuses the lot. The store drops a bad line on load and reports it, which is
    /// right for a file somebody hand-edited and wrong here: half a checklist arriving with a note
    /// about the other half is worse than the import not happening.
    /// </summary>
    [Fact]
    public void ABadFileImportsNothingAtAll()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");

        var change = checklists.Import(BadFile);

        Assert.False(change.Changed);
        Assert.Contains("Nothing was imported", change.Report, StringComparison.Ordinal);
        Assert.Equal("buy limpets", Assert.Single(checklists.Document.In(ChecklistScope.Universal)).Text);
    }

    [Fact]
    public void SomethingThatIsNotAChecklistIsSaidSoRatherThanThrown()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        var change = checklists.Import("this is not json");

        Assert.False(change.Changed);
        Assert.Contains("not a checklist file", change.Report, StringComparison.Ordinal);
    }

    /// <summary>One good line and one with no key, which is a line nothing can identify.</summary>
    private const string BadFile = """
        {
          "commanderFid": "",
          "items": [
            { "key": "note-1", "scope": { "group": "universal" }, "kind": "authored", "text": "fine" },
            { "key": "", "scope": { "group": "universal" }, "kind": "authored", "text": "nameless" }
          ]
        }
        """;
}
