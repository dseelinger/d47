using System.Globalization;
using System.Text;
using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Goals;

/// <summary>What to do about an arc today (Phase 34, "The checklist points at the arc").</summary>
/// <param name="Say">The next concrete thing, in one sentence.</param>
/// <param name="Lines">What to propose, empty where there is nothing to propose without inventing it.</param>
/// <param name="Delegated">
/// Set where accepting is handed to a solver that already does this properly — the engineers arc, which
/// is the one <see cref="UnlockPlanner"/> was built for.
/// </param>
public sealed record GoalStep(string Say, IReadOnlyList<string> Lines, bool Delegated = false)
{
    public bool CanPropose => Lines.Count > 0 || Delegated;
}

/// <summary>The Commander's arcs, read as one thing (Phase 34).</summary>
/// <param name="store">The file.</param>
/// <param name="commander">
/// The Frontier id of whoever is aboard, or null before the journal has said.
/// </param>
/// <param name="state">Live journal state, or null in a configuration that reads no journals.</param>
/// <param name="checklists">Where an accepted arc's lines go.</param>
/// <param name="engineers">The solver the engineers arc delegates to.</param>
public sealed class GoalBook(
    GoalStore store,
    Func<string?> commander,
    Func<CommanderGameState?> state,
    ChecklistService? checklists = null,
    Func<EngineerPlanService?>? engineers = null)
{
    public GoalStore Store => store;

    /// <summary>The story's standing, or null where there is none to show. Set by the host.</summary>
    public Func<GoalStanding?> Story { get; set; } = () => null;

    /// <summary>The last walk over this Commander's journals, or null where none has been done.</summary>
    public GoalMine? Mine => store.MineFor(commander());

    /// <summary>Arc keys this Commander has removed.</summary>
    public IReadOnlyList<string> Removed => store.SetAsideBy(commander());

    /// <summary>
    /// Every arc that should be on the page: the built-ins and the story, less anything removed.
    /// </summary>
    public IReadOnlyList<GoalStanding> Standings
    {
        get
        {
            var removed = Removed;

            return
            [
                .. Catalogued(commander(), state(), removed)
                    .Concat(StoryStandings().Where(standing => !removed.Contains(standing.Arc.Key, StringComparer.OrdinalIgnoreCase))),
            ];
        }
    }

    /// <summary>The built-in arcs for one Commander's state, less the ones they removed; no story arc.</summary>
    public IReadOnlyList<GoalStanding> BuiltInStandingsOf(CommanderGameState commanderState)
    {
        ArgumentNullException.ThrowIfNull(commanderState);

        var fid = commanderState.Identity.FrontierId;

        return [.. Catalogued(fid, commanderState, store.SetAsideBy(fid))];
    }

    private IEnumerable<GoalStanding> Catalogued(string? fid, CommanderGameState? live, IReadOnlyList<string> removed)
    {
        var mine = store.MineFor(fid);

        return GoalCatalogue.All(live)
            .Where(arc => !removed.Contains(arc.Key, StringComparer.OrdinalIgnoreCase))
            .Select(arc => GoalEvaluator.Evaluate(arc, live, mine));
    }

    /// <summary>Including the removed ones.</summary>
    public IReadOnlyList<GoalStanding> Everything()
    {
        var mine = Mine;
        var live = state();

        return
        [
            .. GoalCatalogue.All(live)
                .Select(arc => GoalEvaluator.Evaluate(arc, live, mine))
                .Concat(StoryStandings()),
        ];
    }

    private IEnumerable<GoalStanding> StoryStandings() => Story() is { } standing ? [standing] : [];

    public GoalStanding? Find(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var wanted = key.Trim();

        return Everything().FirstOrDefault(standing =>
                   string.Equals(standing.Arc.Key, wanted, StringComparison.OrdinalIgnoreCase))
               ?? Everything().FirstOrDefault(standing =>
                   standing.Arc.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The whole page in words.</summary>
    public string Describe(DateTimeOffset now)
    {
        var standings = Standings;

        if (standings.Count == 0)
        {
            return "Every goal is removed, so there is nothing to report.";
        }

        var text = new StringBuilder();

        foreach (var standing in standings)
        {
            text.AppendLine(standing.Describe(now));
        }

        if (Mine is null)
        {
            text.AppendLine(
                "I have not read back through your journals yet, so nothing here has an age. The "
                + "goals panel has the button.");
        }
        else if (Mine is { From: { } from, Journals: var journals })
        {
            text.AppendLine(
                $"Ages come from {journals} journals on this disk, the oldest from {from:d MMM yyyy}.");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>One line, for the settings row.</summary>
    public string Summarise(DateTimeOffset now)
    {
        var standings = Standings;
        var done = standings.Count(standing => standing.IsDone);
        var running = standings.Count(standing => !standing.IsDone && standing.Age(now) is not null);

        var mined = Mine is { } mine
            ? $"Last read your journals on {mine.MinedAt:d MMM yyyy} ({mine.Journals} of them)."
            : "I have not read back through your journals yet.";

        return $"{standings.Count} goals, {done} finished, {running} under way. {mined}";
    }

    /// <summary>
    /// The next concrete thing to do about one arc — item 3's question, and the join that makes the arc
    /// and the checklist worth having together.
    /// </summary>
    public GoalStep? Next(string? key)
    {
        if (Find(key) is not { } standing)
        {
            return null;
        }

        if (standing.IsDone)
        {
            return new GoalStep($"{standing.Arc.Name} is done. Nothing to do about it.", []);
        }

        return standing.Arc.Key switch
        {
            _ when GoalCatalogue.CareerOf(standing.Arc.Key) is not null => Career(standing),
            GoalCatalogue.Powerplay => Powerplay(standing),
            GoalCatalogue.Engineers => Engineers(standing),
            GoalCatalogue.Ships => Ships(standing),
            _ => null,
        };
    }

    /// <summary>
    /// Puts an arc's next step to the Commander as a proposal, which they accept or decline like
    /// anything else.
    /// </summary>
    public string Promote(string? key)
    {
        if (Find(key) is not { } standing)
        {
            return key is { Length: > 0 } named
                ? $"I have no goal called {named.Trim()}."
                : "Which goal?";
        }

        if (Next(standing.Arc.Key) is not { } step)
        {
            return $"There is nothing I can turn into a checklist line for {standing.Arc.Name}.";
        }

        if (!step.CanPropose)
        {
            return step.Say;
        }

        if (checklists is null)
        {
            return "I am not holding a checklist in this configuration.";
        }

        if (step.Delegated)
        {
            return engineers?.Invoke() is not { } solver
                ? "I cannot work out the engineer route in this configuration."
                : solver.Promote(null, standing.Arc.Key);
        }

        var said = checklists.ProposeAdd(ChecklistScope.Universal, step.Lines, standing.Arc.Key);

        return $"{said} That is towards {standing.Arc.Name}.";
    }

    /// <summary>Takes an arc off the page. Recover brings it back.</summary>
    public string Remove(string? key) => SetRemoved(key, removed: true);

    /// <summary>Puts a removed arc back on the page.</summary>
    public string Recover(string? key) => SetRemoved(key, removed: false);

    private string SetRemoved(string? key, bool removed)
    {
        if (Find(key) is not { } standing)
        {
            return key is { Length: > 0 } named
                ? $"I have no goal called {named.Trim()}."
                : "Which goal?";
        }

        if (!store.SetAside(commander(), standing.Arc.Key, removed))
        {
            return removed
                ? $"{standing.Arc.Name} is already removed."
                : $"{standing.Arc.Name} is not removed.";
        }

        return removed
            ? $"Removed: {standing.Arc.Name}. I will not bring it up until you recover it."
            : $"Recovered: {standing.Arc.Name}.";
    }

    /// <summary>A career.</summary>
    private static GoalStep Career(GoalStanding standing)
    {
        var remaining = standing.Have is { } have ? RankStanding.EliteTop - have : (long?)null;

        var say = new StringBuilder();

        say.Append(standing.Arc.Name).Append(": ");
        say.Append(standing.Note ?? "I cannot say where you stand yet");
        say.Append(". Rank comes from doing the career rather than from anything I can put on a list");

        if (standing.Arc.Helper is { Length: > 0 } helper)
        {
            say.Append(", and ").Append(helper).Append(" is what I have for it");
        }

        say.Append('.');

        if (remaining is > 0)
        {
            say.Append(' ').Append(remaining.Value).Append(remaining == 1 ? " rank to go." : " ranks to go.");
        }

        return new GoalStep(say.ToString(), []);
    }

    /// <summary>The Powerplay ladder, which proposes nothing for the reason a career arc does not.</summary>
    private static GoalStep Powerplay(GoalStanding standing)
    {
        var remaining = standing.Have is { } have ? GoalCatalogue.PowerplayTop - have : (long?)null;

        var say = new StringBuilder();

        say.Append(standing.Arc.Name).Append(": ");
        say.Append(standing.Note ?? "I cannot say where you stand yet");
        say.Append(". Rank comes from the merits you earn for your Power rather than from anything I can ");
        say.Append("put on a list.");

        if (remaining is > 0)
        {
            say.Append(' ').Append(remaining.Value).Append(remaining == 1 ? " rank to go." : " ranks to go.");
        }

        return new GoalStep(say.ToString(), []);
    }

    /// <summary>The engineers.</summary>
    private GoalStep Engineers(GoalStanding standing)
    {
        if (engineers?.Invoke() is not { } solver)
        {
            return new GoalStep(
                $"{standing.Arc.Name}: {standing.Note ?? "I cannot say"}. I cannot work out a route in "
                + "this configuration.",
                []);
        }

        if (NextEngineer(solver.Report()) is not { } candidate)
        {
            return new GoalStep(
                $"{standing.Arc.Name}: {standing.Note ?? "I cannot say"}. There is nobody I can name as "
                + "the next one to go and get.",
                []);
        }

        return new GoalStep(
            $"{standing.Arc.Name}: {standing.Note ?? "I cannot say"}. Next: {candidate.Summary()}",
            [],
            Delegated: true);
    }

    /// <summary>The collection.</summary>
    private GoalStep Ships(GoalStanding standing)
    {
        if (NextHull(state()) is not { } next)
        {
            return new GoalStep(
                $"{standing.Arc.Name}: {standing.Note ?? "I cannot say"}. I cannot name a hull you are "
                + "missing — either you have them all, or your fleet has not been listed at a shipyard yet.",
                []);
        }

        var cost = next.Cost!.Value.ToString("N0", CultureInfo.InvariantCulture);

        return new GoalStep(
            $"{standing.Arc.Name}: {standing.Note ?? "I cannot say"}. The cheapest hull you do not have "
            + $"is the {next.Name}, at {cost} credits.",
            [$"Buy a {next.Name} — {cost} cr, the cheapest hull you do not own"]);
    }

    /// <summary>The engineer the unlock route names next, or null when it names nobody.</summary>
    public static UnlockCandidate? NextEngineer(EngineerReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return report.Route.FirstOrDefault();
    }

    /// <summary>The cheapest obtainable hull with a price that the Commander does not own, or null.</summary>
    public static ShipSpecification? NextHull(CommanderGameState? live)
    {
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ship in live?.Fleet.Ships ?? [])
        {
            owned.Add(ship.Type);
        }

        if (live?.Ship.Type is { Length: > 0 } flying)
        {
            owned.Add(flying);
        }

        return EliteSpecifications.Ships
            .Where(ship => !owned.Contains(ship.Symbol) && !GoalEvaluator.IsUnobtainable(ship) && ship.Cost is > 0)
            .OrderBy(ship => ship.Cost)
            .FirstOrDefault();
    }
}
