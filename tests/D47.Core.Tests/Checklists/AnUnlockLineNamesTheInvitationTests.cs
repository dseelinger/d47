using D47.Core.Checklists;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// A promoted unlock line says what the invitation actually asks for
/// (<a href="https://github.com/dseelinger/d47/issues/22">#22</a>).
/// <para>
/// It used to say <em>"Unlock Bill Turner at Alioth"</em> and stop, while d47 held the answer all
/// along — the shipped engineer table carries the invitation prose for 34 of the 38, and both the
/// Engineers drill and <c>find_engineer</c> already read it. The checklist is the one surface that
/// survives leaving the page, so it is the one that needed it.
/// </para>
/// </summary>
public class AnUnlockLineNamesTheInvitationTests
{
    private static ChecklistItem Unlock(string engineer, int grade = 1)
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.EngineerAccess, engineer) { Grade = grade };

        return new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Universal,
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = grade > 1 ? $"Rank {grade} with {engineer}" : $"Unlock {engineer} at somewhere",
            Intent = intent,
            Provenance = ChecklistProvenance.Asserted,
        };
    }

    [Fact]
    public void ItSaysWhatTheInvitationAsksFor()
    {
        var said = ChecklistWording.Said(Unlock("Bill Turner"), null);

        Assert.Contains("Bromellite", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every engineer with the prose gets it, rather than the one that was easy to check.
    /// </summary>
    [Fact]
    public void EveryEngineerWithAnInvitationTaskNamesIt()
    {
        var named = 0;

        foreach (var engineer in EngineerDirectory.All)
        {
            if (engineer.Unlock is not { Length: > 0 })
            {
                continue;
            }

            var said = ChecklistWording.Said(Unlock(engineer.Name), null);

            Assert.NotEqual(Unlock(engineer.Name).Text, said);
            named++;
        }

        // 34 of the 38 carry it. A drop here means the table changed and this wants re-reading,
        // rather than the wording quietly covering fewer engineers than it did.
        Assert.Equal(34, named);
    }

    /// <summary>
    /// And the four without one say so rather than stopping, which would read as though nothing
    /// were required. What earns the invitation is filled for all thirty-eight.
    /// </summary>
    [Theory]
    [InlineData("Oden Geiger")]
    [InlineData("Uma Laszlo")]
    [InlineData("Yarden Bond")]
    [InlineData("Yi Shen")]
    public void TheFourWithNoInvitationTaskSaySoRatherThanNothing(string engineer)
    {
        var said = ChecklistWording.Said(Unlock(engineer), null);

        Assert.NotEqual(Unlock(engineer).Text, said);
        Assert.Contains("no invitation task on record", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// A rank step is left alone. "Rank 4 with Selene Jean" is self-explanatory — ranking is
    /// rolling — and hanging an invitation on it would describe work already done.
    /// </summary>
    [Fact]
    public void ARankStepIsNotAnInvitation()
    {
        var item = Unlock("Bill Turner", grade: 4);

        Assert.Equal(item.Text, ChecklistWording.Said(item, null));
    }

    /// <summary>
    /// Nothing is stored. The wording is computed from the table every time the line is drawn, so a
    /// regenerated table reaches lines already on the list.
    /// </summary>
    [Fact]
    public void TheLineItselfIsUnchanged()
    {
        var item = Unlock("Bill Turner");

        ChecklistWording.Said(item, null);

        Assert.Equal("Unlock Bill Turner at somewhere", item.Text);
    }

    /// <summary>
    /// And the spoken form stays a sentence rather than becoming a paragraph — the checklist
    /// callout is the one line with no heading or page around it.
    /// <para>
    /// This caught a real one: falling back to what <em>earns</em> the invitation for the four
    /// without a task read 172 characters on Oden Geiger. The line says the short honest thing now
    /// and the drill keeps the prose.
    /// </para>
    /// </summary>
    [Fact]
    public void TheSpokenLineStaysASentence()
    {
        foreach (var engineer in EngineerDirectory.All)
        {
            var said = ChecklistWording.Aloud(Unlock(engineer.Name), null);

            Assert.True(
                said.Length <= 160,
                $"{engineer.Name} speaks {said.Length} characters: {said}");
        }
    }
}
