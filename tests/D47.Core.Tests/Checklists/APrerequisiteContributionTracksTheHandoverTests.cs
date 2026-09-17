using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// A checklist item for an engineer's tribute stays open with a running tally as
/// <c>EngineerContribution</c> arrives, and finishes once the total reaches what the invitation asks for
/// (#257).
/// </summary>
public class APrerequisiteContributionTracksTheHandoverTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState State(string? contribution = null)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        if (contribution is { Length: > 0 })
        {
            store.Apply(Event(contribution));
        }

        return store.Active!;
    }

    private static ChecklistItem TributeItem()
    {
        var marco = EngineerDirectory.ByName("Marco Qwent")!;

        return Assert.Single(
            EngineerAccess.UnmetPrerequisites(marco, new D47.Core.Engineers.UnlockEvidence(null, null, null, null, null, null)),
            item => item.Intent!.Detail == EngineerAccess.TributeRole);
    }

    [Fact]
    public void APartialHandoverStaysOpenWithTheReading()
    {
        var item = TributeItem();

        var verdict = ChecklistEvaluator.Evaluate(item, State(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerContribution","EngineerID":300200,"Type":"Commodity","Commodity":"modularterminals","TotalQuantity":18}"""));

        Assert.NotNull(verdict);
        Assert.Equal(ChecklistState.Open, verdict.Value.State);
        Assert.Equal("18 of 25 handed over", verdict.Value.Reason);
    }

    [Fact]
    public void ReachingTheQuantityFinishesTheLine()
    {
        var item = TributeItem();

        var verdict = ChecklistEvaluator.Evaluate(item, State(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerContribution","EngineerID":300200,"Type":"Commodity","Commodity":"modularterminals","TotalQuantity":25}"""));

        Assert.NotNull(verdict);
        Assert.Equal(ChecklistState.Done, verdict.Value.State);
    }

    /// <summary>Nothing reported yet is open, and says so rather than claiming a shortfall.</summary>
    [Fact]
    public void NoContributionYetIsOpenAndSaysNothingHasBeenReported()
    {
        var item = TributeItem();

        var verdict = ChecklistEvaluator.Evaluate(item, State());

        Assert.NotNull(verdict);
        Assert.Equal(ChecklistState.Open, verdict.Value.State);
        Assert.Equal("Nothing in the journal has reported this yet.", verdict.Value.Reason);
    }
}
