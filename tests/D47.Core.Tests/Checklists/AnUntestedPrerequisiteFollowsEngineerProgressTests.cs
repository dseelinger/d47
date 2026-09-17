using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// Marco Qwent's invitation carries no structured test at all (<c>Engineers.tsv</c> has none), so the
/// checklist item for it is open until <c>EngineerProgress</c> itself decides it — invited finishes the
/// invitation, and unlocked finishes both lines (#257).
/// </summary>
public class AnUntestedPrerequisiteFollowsEngineerProgressTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static GameStateStore Store()
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        return store;
    }

    private static ChecklistItem Item(string role)
    {
        var marco = EngineerDirectory.ByName("Marco Qwent")!;

        return Assert.Single(
            EngineerAccess.UnmetPrerequisites(marco, new D47.Core.Engineers.UnlockEvidence(null, null, null, null, null, null)),
            item => item.Intent!.Detail == role);
    }

    [Fact]
    public void KnownAndNoMoreIsOpenWithNothingReported()
    {
        var store = Store();

        var invitation = Item(EngineerAccess.InvitationRole);
        var verdict = ChecklistEvaluator.Evaluate(invitation, store.Active);

        Assert.NotNull(verdict);
        Assert.Equal(ChecklistState.Open, verdict.Value.State);
    }

    [Fact]
    public void InvitedFinishesTheInvitationButNotTheTribute()
    {
        var store = Store();

        store.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Marco Qwent","EngineerID":300200,"Progress":"Invited"}]}"""));

        var invitation = Item(EngineerAccess.InvitationRole);
        var tribute = Item(EngineerAccess.TributeRole);

        Assert.Equal(ChecklistState.Done, ChecklistEvaluator.Evaluate(invitation, store.Active)!.Value.State);
        Assert.Equal(ChecklistState.Open, ChecklistEvaluator.Evaluate(tribute, store.Active)!.Value.State);
    }

    [Fact]
    public void UnlockedFinishesBothLines()
    {
        var store = Store();

        store.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Marco Qwent","EngineerID":300200,"Progress":"Unlocked","Rank":1}]}"""));

        var invitation = Item(EngineerAccess.InvitationRole);
        var tribute = Item(EngineerAccess.TributeRole);

        Assert.Equal(ChecklistState.Done, ChecklistEvaluator.Evaluate(invitation, store.Active)!.Value.State);
        Assert.Equal(ChecklistState.Done, ChecklistEvaluator.Evaluate(tribute, store.Active)!.Value.State);
    }
}
