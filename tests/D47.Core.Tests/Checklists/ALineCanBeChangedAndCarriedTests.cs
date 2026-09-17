using D47.Core.Checklists;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// Rewording a line, and moving a whole list to another machine.
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

    /// <summary>The key does not move.</summary>
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

    /// <summary>A derived line's words are the plan's words.</summary>
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

}
