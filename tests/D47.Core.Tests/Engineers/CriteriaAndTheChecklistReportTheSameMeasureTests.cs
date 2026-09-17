using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Engineers;

/// <summary>
/// <see cref="UnlockCriterion.Measure"/> — the number and target behind a graded prerequisite, read the
/// same way whether the checklist or <see cref="EngineerAccess.CriteriaFor"/> asks for it (#17).
/// </summary>
public class CriteriaAndTheChecklistReportTheSameMeasureTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static Engineer Named(string name) =>
        EngineerDirectory.ByName(name) ?? throw new InvalidOperationException($"no {name}");

    /// <summary>A contribution tribute carries current and target, and the checklist reads the same pair.</summary>
    [Fact]
    public void AContributionCriterionsMeasureMatchesTheChecklistItem()
    {
        var marco = Named("Marco Qwent");

        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        state.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerContribution","EngineerID":300200,"Type":"Commodity","Commodity":"modularterminals","TotalQuantity":18}"""));

        var criterion = EngineerAccess.CriteriaFor(marco, D47.Core.Engineers.UnlockEvidence.From(state))
            .Single(entry => entry.Text == marco.Unlock);

        Assert.Equal(new UnlockMeasure(18, 25, false), criterion.Measure);

        var item = new ChecklistItem
        {
            Key = "test",
            Scope = ChecklistScope.Universal,
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineerPrerequisite,
            Text = marco.Unlock!,
            Intent = new ChecklistIntent(ChecklistIntentKind.EngineerPrerequisite, marco.Name)
            {
                Detail = EngineerAccess.TributeRole,
            },
            Provenance = ChecklistProvenance.Asserted,
        };

        var verdict = ChecklistEvaluator.Evaluate(item, state);

        Assert.Equal(criterion.Measure, verdict!.Value.Measure);
    }

    /// <summary>The ceiling test fills fuller the lower the reputation reading sits under the threshold.</summary>
    [Fact]
    public void UmaLaszlosBarIsFullerAtALowerReputation()
    {
        var uma = Named("Uma Laszlo");

        var low = ReputationState.Empty.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Xuane","Factions":[{"Name":"Sirius Corporation","MyReputation":-95}]}"""));

        var lowFill = EngineerAccess.CriteriaFor(
                uma, new D47.Core.Engineers.UnlockEvidence(null, null, null, low, null, null))
            .Single(criterion => criterion.Text == uma.Meeting).Measure!.Fill;

        var high = ReputationState.Empty.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Xuane","Factions":[{"Name":"Sirius Corporation","MyReputation":-10}]}"""));

        var highFill = EngineerAccess.CriteriaFor(
                uma, new D47.Core.Engineers.UnlockEvidence(null, null, null, high, null, null))
            .Single(criterion => criterion.Text == uma.Meeting).Measure!.Fill;

        Assert.True(lowFill > highFill);
    }
}
