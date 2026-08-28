using System.Globalization;
using System.Text;
using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Goals;

/// <summary>
/// What to do about an arc today (Phase 34, "The checklist points at the arc").
/// <para>
/// <b>Two halves, and only one of them is a proposal.</b> <see cref="Say"/> is what d47 tells the
/// Commander; <see cref="Lines"/> is what it would put on their list if they agreed. A step with
/// something to say and nothing to propose is the ordinary case for a career rank — <em>rank is
/// earned by doing the career</em> — and manufacturing a checklist line there would be filler on a
/// page whose credibility is all it has.
/// </para>
/// </summary>
/// <param name="Say">The next concrete thing, in one sentence.</param>
/// <param name="Lines">What to propose, empty where there is nothing honest to propose.</param>
/// <param name="Delegated">
/// Set where accepting is handed to a solver that already does this properly — the engineers arc,
/// which is the one <see cref="UnlockPlanner"/> was built for.
/// </param>
public sealed record GoalStep(string Say, IReadOnlyList<string> Lines, bool Delegated = false)
{
    public bool CanPropose => Lines.Count > 0 || Delegated;
}

/// <summary>
/// The Commander's arcs, read as one thing (Phase 34).
/// <para>
/// The store knows about a file; this knows who is flying, what the game is currently saying, and
/// which arcs have been set aside. Same arrangement as <see cref="Habits.HabitBook"/> over
/// <see cref="Habits.HabitStore"/>, and for the same reason.
/// </para>
/// </summary>
/// <param name="store">The file.</param>
/// <param name="commander">
/// The Frontier id of whoever is aboard, or null before the journal has said. Read per call rather
/// than captured, because a Commander can log into a second character without restarting d47.
/// </param>
/// <param name="state">Live journal state, or null in a configuration that reads no journals.</param>
/// <param name="checklists">Where an accepted arc's lines go. The proposals file, never the list.</param>
/// <param name="engineers">
/// The solver the engineers arc delegates to. A function rather than the service, because the
/// solver is composed after the stores it reads and a holder is what the App already uses to say so.
/// </param>
public sealed class GoalBook(
    GoalStore store,
    Func<string?> commander,
    Func<CommanderGameState?> state,
    ChecklistService? checklists = null,
    Func<EngineerPlanService?>? engineers = null)
{
    public GoalStore Store => store;

    public string? CommanderId => commander();

    /// <summary>The last walk over this Commander's journals, or null where none has been done.</summary>
    public GoalMine? Mine => store.MineFor(commander());

    /// <summary>Arc keys this Commander has put out of sight.</summary>
    public IReadOnlyList<string> Aside => store.SetAsideBy(commander());

    /// <summary>
    /// Every arc that should be on the page: the built-ins, plus whatever the Commander invented,
    /// less anything set aside. <b>The one place the set-aside list is applied</b>, so the page and
    /// the tool cannot disagree about what is hidden.
    /// </summary>
    public IReadOnlyList<GoalStanding> Standings
    {
        get
        {
            var aside = Aside;
            var mine = Mine;
            var live = state();

            return
            [
                .. GoalCatalogue.All
                    .Concat(store.AuthoredBy(commander()))
                    .Where(arc => !aside.Contains(arc.Key, StringComparer.OrdinalIgnoreCase))
                    .Select(arc => GoalEvaluator.Evaluate(arc, live, mine)),
            ];
        }
    }

    /// <summary>Including the ones set aside. What the panel needs to offer bringing one back.</summary>
    public IReadOnlyList<GoalStanding> Everything()
    {
        var mine = Mine;
        var live = state();

        return
        [
            .. GoalCatalogue.All
                .Concat(store.AuthoredBy(commander()))
                .Select(arc => GoalEvaluator.Evaluate(arc, live, mine)),
        ];
    }

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

    /// <summary>The whole page in words. What <c>get_goals</c> returns and the panel row summarises.</summary>
    public string Describe(DateTimeOffset now)
    {
        var standings = Standings;

        if (standings.Count == 0)
        {
            return "Every goal is set aside, so there is nothing to report.";
        }

        var text = new StringBuilder();

        foreach (var standing in standings)
        {
            text.AppendLine(standing.Describe(now));
        }

        if (Mine is null)
        {
            text.AppendLine(
                "I have not read back through your journals yet, so nothing here has an age and the "
                + "milestones have no figures. The goals panel has the button.");
        }
        else if (Mine is { From: { } from, Journals: var journals })
        {
            text.AppendLine(
                $"Ages and milestones come from {journals} journals on this disk, the oldest from "
                + $"{from:d MMM yyyy}.");
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
    /// The next concrete thing to do about one arc — item 3's question, and the join that makes the
    /// arc and the checklist worth having together.
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

        if (standing.Arc.Kind == GoalKind.Authored)
        {
            return new GoalStep(
                $"{standing.Arc.Name} is yours rather than mine to work out. {standing.Arc.Done}",
                []);
        }

        return standing.Arc.Key switch
        {
            _ when GoalCatalogue.CareerOf(standing.Arc.Key) is not null => Career(standing),
            GoalCatalogue.Engineers => Engineers(standing),
            GoalCatalogue.Ships => Ships(standing),
            GoalCatalogue.Systems or GoalCatalogue.Distance => Milestone(standing),
            _ => null,
        };
    }

    /// <summary>
    /// Puts an arc's next step to the Commander as a proposal, which they accept or decline like
    /// anything else. <b>Accepting stays their act</b> — item 3 says so, and it is the same trust
    /// boundary <see cref="ChecklistProposalStore"/> already draws.
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

    /// <summary>Writes a goal the Commander invented. Their words, their definition of done.</summary>
    public string Author(string name, string? done, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "A goal needs a name.";
        }

        var trimmed = name.Trim();

        if (trimmed.Length > ChecklistLimits.MaxTextLength)
        {
            return $"That is {trimmed.Length} characters; a goal's name is at most "
                   + $"{ChecklistLimits.MaxTextLength}.";
        }

        var arc = new GoalArc
        {
            Key = Key(trimmed),
            Name = trimmed,
            Done = string.IsNullOrWhiteSpace(done) ? "Yours to call finished." : done.Trim(),
            Kind = GoalKind.Authored,
            Written = now,
        };

        return store.Author(commander(), arc)
               ?? $"Noted: {arc.Name}. That one is yours to call done, and I will count how long it runs.";
    }

    /// <summary>
    /// Marks an authored arc finished, or re-opens it. <b>Refuses a derived one and says why</b> —
    /// the same refusal <see cref="ChecklistDocument.Complete"/> gives about a derived line, and the
    /// same reason: the next journal read would either undo it or leave it standing and wrong.
    /// </summary>
    public string Finish(string? key, bool finished, DateTimeOffset now)
    {
        if (Find(key) is not { } standing)
        {
            return key is { Length: > 0 } named
                ? $"I have no goal called {named.Trim()}."
                : "Which goal?";
        }

        if (standing.Arc.Kind != GoalKind.Authored)
        {
            return $"{standing.Arc.Name} is worked out from your journal rather than ticked. "
                   + "If I marked it by hand the next read would either undo it or leave it standing and wrong.";
        }

        if (standing.Arc.Finished == finished)
        {
            return $"{standing.Arc.Name} is already {(finished ? "done" : "open")}.";
        }

        var updated = standing.Arc with { Finished = finished, FinishedAt = finished ? now : null };

        if (!store.Replace(commander(), updated))
        {
            return $"I could not write {standing.Arc.Name} back.";
        }

        return finished
            ? $"{standing.Arc.Name} — done. That one ran {Ran(standing, now)}."
            : $"{standing.Arc.Name} is open again.";
    }

    /// <summary>Puts an arc out of sight, or brings it back.</summary>
    public string SetAside(string? key, bool aside)
    {
        if (Find(key) is not { } standing)
        {
            return key is { Length: > 0 } named
                ? $"I have no goal called {named.Trim()}."
                : "Which goal?";
        }

        if (!store.SetAside(commander(), standing.Arc.Key, aside))
        {
            return aside
                ? $"{standing.Arc.Name} is already set aside."
                : $"{standing.Arc.Name} is not set aside.";
        }

        return aside
            ? $"Set aside: {standing.Arc.Name}. I will not bring it up until you ask for it back."
            : $"Back on the list: {standing.Arc.Name}.";
    }

    /// <summary>Drops an authored arc for good. A built-in is set aside instead, never deleted.</summary>
    public string Forget(string? key)
    {
        if (Find(key) is not { } standing)
        {
            return key is { Length: > 0 } named
                ? $"I have no goal called {named.Trim()}."
                : "Which goal?";
        }

        if (standing.Arc.Kind != GoalKind.Authored)
        {
            return $"{standing.Arc.Name} is one of mine, so it is set aside rather than deleted.";
        }

        return store.Forget(commander(), standing.Arc.Key)
            ? $"Forgotten: {standing.Arc.Name}."
            : $"I could not find {standing.Arc.Name} to forget.";
    }

    /// <summary>
    /// A career. Nothing d47 ships computes a route to a rank, so the honest next step is the
    /// capability it already has for that career — naming a tool that exists is not inventing a
    /// plan.
    /// </summary>
    private static GoalStep Career(GoalStanding standing)
    {
        var remaining = standing.Have is { } have ? RankStanding.Elite - have : (long?)null;

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

    /// <summary>
    /// The engineers. <b>Delegated outright to <see cref="UnlockPlanner"/></b>, which already
    /// answers "what is the next concrete thing" for exactly this — ranked by fastest unlock and
    /// distance together, with its reasoning on the page.
    /// </summary>
    private GoalStep Engineers(GoalStanding standing)
    {
        if (engineers?.Invoke() is not { } solver)
        {
            return new GoalStep(
                $"{standing.Arc.Name}: {standing.Note ?? "I cannot say"}. I cannot work out a route in "
                + "this configuration.",
                []);
        }

        var report = solver.Report();

        if (report.Route.FirstOrDefault() is not { } candidate)
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

    /// <summary>
    /// The collection. The cheapest hull the Commander does not own, which is a fact off the
    /// shipyard table rather than an opinion about what they should fly.
    /// </summary>
    private GoalStep Ships(GoalStanding standing)
    {
        var live = state();

        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ship in live?.Fleet.Ships ?? [])
        {
            owned.Add(ship.Type);
        }

        if (live?.Ship.Type is { Length: > 0 } flying)
        {
            owned.Add(flying);
        }

        var next = EliteSpecifications.Ships
            .Where(ship => !owned.Contains(ship.Symbol) && ship.Cost is > 0)
            .OrderBy(ship => ship.Cost)
            .FirstOrDefault();

        if (next is null)
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

    /// <summary>A ladder. The line is the gap to the next rung, which is a number rather than advice.</summary>
    private static GoalStep Milestone(GoalStanding standing)
    {
        if (standing.Have is not { } have || standing.Need is not { } need)
        {
            return new GoalStep(
                $"{standing.Arc.Name}: I have not counted these yet. Read my journals from the goals "
                + "panel and I will have a figure.",
                []);
        }

        var gap = need - have;
        var unit = standing.Arc.Unit ?? string.Empty;

        return new GoalStep(
            $"{standing.Arc.Name}: {have:N0} of {need:N0} {unit}. {gap:N0} {unit} to the next milestone.",
            [$"{gap:N0} more {unit} to reach {need:N0}"]);
    }

    private static string Ran(GoalStanding standing, DateTimeOffset now) =>
        standing.Age(now) is { } age && age.TotalDays >= 1
            ? $"{(int)age.TotalDays} days"
            : "less than a day";

    /// <summary>
    /// A key from a name — lowercase, spaces to hyphens, prefixed so it can never collide with a
    /// built-in arc's key however somebody names their goal.
    /// </summary>
    private static string Key(string name)
    {
        var text = new StringBuilder("mine.");

        foreach (var character in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                text.Append(character);
            }
            else if (text.Length > 5 && text[^1] != '-')
            {
                text.Append('-');
            }
        }

        return text.ToString().TrimEnd('-');
    }
}
