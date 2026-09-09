using D47.Core.Checklists;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>A promoted unlock line says what the invitation actually asks for.</summary>
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

    /// <summary>Every engineer with the prose gets it, rather than the one that was easy to check.</summary>
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

        // 34 of the 38 carry it.
        Assert.Equal(34, named);
    }

    /// <summary>
    /// And the four without one say so rather than stopping, which would read as though nothing were
    /// required.
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
    /// A rank step is left alone. "Rank 4 with Selene Jean" is self-explanatory — ranking is rolling —
    /// and hanging an invitation on it would describe work already done.
    /// </summary>
    [Fact]
    public void ARankStepIsNotAnInvitation()
    {
        var item = Unlock("Bill Turner", grade: 4);

        Assert.Equal(item.Text, ChecklistWording.Said(item, null));
    }

    /// <summary>Nothing is stored.</summary>
    [Fact]
    public void TheLineItselfIsUnchanged()
    {
        var item = Unlock("Bill Turner");

        ChecklistWording.Said(item, null);

        Assert.Equal("Unlock Bill Turner at somewhere", item.Text);
    }

    /// <summary>
    /// And the spoken form stays a sentence rather than becoming a paragraph — the checklist callout is
    /// the one line with no heading or page around it.
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
