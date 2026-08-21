using System.Globalization;
using System.Text;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;

namespace D47.Core.Engineers;

/// <summary>
/// The Engineers tab's one seam: it reads both plan stores and the Commander's journal, ranks the
/// ways in, and offers a chosen route to the checklist (list.md Phase 28).
/// <para>
/// <b>A service rather than a store, because this owns nothing.</b> Every figure is recomputed
/// from the two build stores and the live game state, all three of which move underneath it —
/// caching a ranking would mean a page that is right about a plan the Commander has since changed.
/// </para>
/// <para>
/// <b>Promotion is one-to-many, and it is a proposal.</b> The chain reaches the checklist through
/// <see cref="ChecklistService.ProposePlan"/>, so accepting it stays the Commander's own act —
/// the same path the ship and on-foot plans already take, for the same reason.
/// </para>
/// </summary>
public sealed class EngineerPlanService(
    ShipBuildStore ships,
    OnFootBuildStore onFoot,
    ChecklistService checklists,
    Func<CommanderGameState?> state)
{
    /// <summary>Where every engineer stands and which one to go and get next, computed fresh.</summary>
    public EngineerReport Report() =>
        // This Commander's ship builds only — ship ids are per Commander, so another Commander's
        // plans on the same installation are not demand this report should be counting.
        UnlockPlanner.Of(ships.BuildsFor(state()?.Identity.FrontierId), onFoot.Builds, state());

    /// <summary>
    /// The ranking as a Commander hears it: the best few, each with the sentence that explains
    /// where it came from.
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
            // Said rather than silently ranked on nothing. Without a position every candidate
            // scores the same, and a list in that state is alphabetical wearing a ranking's coat.
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
    /// <para>
    /// Scoped <see cref="ChecklistScope.Universal"/>, and that is the honest scope: an engineer is
    /// not unlocked per ship, and filing the chain under the hull that happened to want it would
    /// hide it the moment the Commander sold that hull.
    /// </para>
    /// </summary>
    /// <param name="goal">
    /// The arc asking, where one is (list.md Phase 34). The engineers arc delegates its "what do I
    /// do about this today" here rather than growing a worse solver of its own, and the chain is
    /// stamped with the arc so the lines say where they came from.
    /// </param>
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

            // Every stop on the chain, so a route the Commander re-asks for after unlocking half
            // of it replaces its own lines rather than laying a second set beside them.
            [.. candidate.Chain.Steps.Select(step => step.Engineer.Name)]);

        return $"{said} {candidate.Summary()}";
    }

    private static bool Matches(UnlockCandidate candidate, string named) =>
        EngineerDirectory.ByName(named) is { } engineer && engineer.Id == candidate.Engineer.Id;
}
