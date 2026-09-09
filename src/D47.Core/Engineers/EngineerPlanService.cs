using System.Globalization;
using System.Text;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;

namespace D47.Core.Engineers;

/// <summary>
/// The Engineers tab's one seam: it reads both plan stores and the Commander's journal, ranks the ways
/// in, and offers a chosen route to the checklist (Phase 28).
/// </summary>
public sealed class EngineerPlanService(
    ShipBuildStore ships,
    OnFootBuildStore onFoot,
    ChecklistService checklists,
    Func<CommanderGameState?> state)
{
    /// <summary>Where every engineer stands and which one to go and get next, computed fresh.</summary>
    public EngineerReport Report() =>
        // This Commander's ship builds only — ship ids are per Commander, so another Commander's plans on the
        // same installation are not demand this report should be counting.
        UnlockPlanner.Of(ships.BuildsFor(state()?.Identity.FrontierId), onFoot.Builds, state());

    /// <summary>
    /// The ranking as a Commander hears it: the best few, each with the sentence that explains where it
    /// came from.
    /// </summary>
    public string Describe(int most = 3)
    {
        var report = Report();

        if (report.Route.Count == 0)
        {
            return report.ProgressKnown
                ? "There is nobody left to unlock — every engineer is already yours at the grade "
                  + "your plans ask for."
                : report.Summary();
        }

        var said = new StringBuilder(report.Summary());

        said.AppendLine();

        if (report.From is { Length: > 0 } here)
        {
            said.Append(CultureInfo.InvariantCulture, $"Measured from {here}");
            said.AppendLine(report.JumpRange is { } range
                ? $", at {range.ToString("N1", CultureInfo.InvariantCulture)} ly a jump."
                : ". No jump range reported, so distances are in light years only.");
        }
        else
        {
            // Said rather than silently ranked on nothing.
            said.AppendLine("I do not know where you are yet, so I cannot rank these by distance.");
        }

        foreach (var candidate in report.Route.Take(Math.Max(most, 1)))
        {
            said.AppendLine();
            said.AppendLine($"{candidate.Engineer.Name} — {candidate.Summary()}");

            foreach (var line in candidate.Working())
            {
                said.AppendLine($"  {line}");
            }
        }

        return said.ToString().TrimEnd();
    }

    /// <summary>
    /// Offers the way in to one engineer — or to the best one, when none is named — as a chain of
    /// checklist items.
    /// </summary>
    /// <param name="goal">The arc asking, where one is (Phase 34).</param>
    public string Promote(string? engineer, string? goal = null)
    {
        var report = Report();

        var candidate = engineer is { Length: > 0 } named
            ? report.Route.FirstOrDefault(entry => Matches(entry, named))
            : report.Route.FirstOrDefault();

        if (candidate is null)
        {
            if (engineer is not { Length: > 0 } wanted)
            {
                return "There is nobody left to unlock.";
            }

            return EngineerDirectory.ByName(wanted) is { } known
                ? $"You already have {known.Name} at the grade your plans ask for."
                : Catalogue.Unknown("engineer", wanted.Trim(), EngineerDirectory.Near(wanted));
        }

        var items = UnlockPlanner.Items(candidate.Chain, ChecklistScope.Universal);

        if (goal is { Length: > 0 })
        {
            items = [.. items.Select(item => item with { Goal = goal })];
        }

        var said = checklists.ProposePlan(
            ChecklistScope.Universal,
            ChecklistSource.EngineeringPlan,

            items,

            // Every stop on the chain, so a route the Commander re-asks for after unlocking half of it
            // replaces its own lines rather than laying a second set beside them.
            [.. candidate.Chain.Steps.Select(step => step.Engineer.Name)]);

        return $"{said} {candidate.Summary()}";
    }

    private static bool Matches(UnlockCandidate candidate, string named) =>
        EngineerDirectory.ByName(named) is { } engineer && engineer.Id == candidate.Engineer.Id;
}
