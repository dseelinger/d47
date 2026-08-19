using System.Text;
using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Memory;

namespace D47.Core.Callouts;

/// <summary>
/// The opening line of a session (list.md Phase 31, "Picking up where you left off").
/// <para>
/// <b>This is the item the other three exist for.</b> The store, the recall and the forgetting are
/// machinery; this is the only part of Phase 31 a Commander experiences, which is why it is
/// deliberately last in the checklist and last in the plan.
/// </para>
/// <para>
/// <b>Assembled in Core, with no model in the path.</b> The facts come from the memory store, the
/// checklist and the engineer plans, and the sentence is authored here — then it goes through the
/// same <see cref="FlavourBriefs"/> path the ambient remarks and the carrier's lines already use, so
/// a core can say it in its own words and personality-off says it plainly. A model handed a session
/// to summarise embellishes: it promotes a routine three-material shortfall into a saga, and this is
/// the one line a Commander hears before anything else has happened.
/// </para>
/// <para>
/// <b>It is a callout, not an autonomous action</b> — it presses nothing, so it takes the callout
/// family's settings shape, cooldown and precedence rather than a protected row of its own. The same
/// reasoning <see cref="LoreCallout"/> records.
/// </para>
/// <para>
/// <b>Silent when there is nothing worth saying.</b> A first run says nothing at all rather than
/// manufacturing continuity out of an empty store, which is the failure mode a "welcome back" line
/// with no memory behind it always has.
/// </para>
/// </summary>
/// <param name="book">The memory store, for where the Commander was and how long ago.</param>
/// <param name="checklists">The Commander's own list, for what a plan is short of.</param>
/// <param name="unlocks">
/// The engineer solver, or a function answering null where nothing composed one — which costs the
/// second clause and not the callout, the shape every optional service in this repository has.
/// <para>
/// A function rather than the service, because of where the callouts are built: the engine has to
/// exist before the tick loop, and the solver reads two stores that are composed after the journal
/// readers. Asking for it when the line is written rather than when the engine is assembled is what
/// keeps that ordering out of this type.
/// </para>
/// </param>
public sealed class ContinuityCallout(
    MemoryBook book,
    ChecklistService checklists,
    Func<EngineerPlanService?> unlocks) : ICallout
{
    public const string Key = "continuity.resume";

    /// <summary>
    /// How long after the first live tick the line waits. Long enough for the journal backlog to
    /// have been folded and for Status.json to have been read at least once — a line assembled
    /// from a half-folded state would be about the middle of last Tuesday.
    /// <para>
    /// Also long enough that it does not arrive while the Commander is still reading the panel,
    /// which is the reason <see cref="AmbientCallout"/> seeds its own first interval.
    /// </para>
    /// </summary>
    public TimeSpan Settle { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// How recently the Commander has to have been seen for the gap to be worth mentioning. Under
    /// this, the line skips its first clause: telling somebody who reconnected after a crash that
    /// it has been four minutes is not continuity, it is a clock.
    /// </summary>
    public TimeSpan WorthMentioning { get; set; } = TimeSpan.FromHours(6);

    private DateTimeOffset _firstLiveTick;
    private bool _said;

    public string Id => "continuity";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        // Nothing to fold and nothing to say from a backlog. The whole point is the first live
        // moment of a session, and priming is the replay of everything before it.
        if (context.IsPriming)
        {
            yield break;
        }

        if (_firstLiveTick == default)
        {
            _firstLiveTick = context.Now;
            yield break;
        }

        if (_said || context.Now - _firstLiveTick < Settle)
        {
            yield break;
        }

        // Said once per run of d47, whatever happens next. Marked before the line is built rather
        // than after, so a session with nothing to say does not re-examine ten times a second for
        // as long as the app is open.
        _said = true;

        if (Compose(context.Now, context.State) is not { } line)
        {
            yield break;
        }

        yield return new Announcement(Key, line)
        {
            // Routine. It is the least urgent thing d47 ever says — the Commander has just sat
            // down — and it stands down for anything that fires on an event.
            Urgency = CalloutUrgency.Routine,

            // Zero, because the once-per-run flag above is the real cooldown and a time-based one
            // would be a second answer to the same question.
            Cooldown = TimeSpan.Zero,
        };
    }

    /// <summary>
    /// The line, or null when there is nothing to say. Separated from <see cref="Examine"/> so a
    /// test can ask for the sentence without driving eight seconds of ticks past it.
    /// </summary>
    public string? Compose(DateTimeOffset now, Journal.CommanderGameState? state)
    {
        var clauses = new List<string>();

        if (Gap(now) is { } gap)
        {
            clauses.Add(gap);
        }

        if (Short(state) is { } shortfall)
        {
            clauses.Add(shortfall);
        }

        if (Unlock() is { } unlock)
        {
            clauses.Add(unlock);
        }

        if (clauses.Count == 0)
        {
            return null;
        }

        var line = new StringBuilder(clauses[0]);

        foreach (var clause in clauses.Skip(1))
        {
            line.Append(' ').Append(clause);
        }

        return line.ToString();
    }

    /// <summary>
    /// How long it has been, and where they were — read off the observation the journal wrote
    /// rather than off any clock d47 kept for itself.
    /// </summary>
    private string? Gap(DateTimeOffset now)
    {
        var seen = book.Mine.FirstOrDefault(entry =>
            string.Equals(entry.Key, MemoryObserver.WhereKey, StringComparison.Ordinal));

        if (seen?.AddedAt is not { } at)
        {
            return null;
        }

        var away = now - at;

        if (away < WorthMentioning)
        {
            return null;
        }

        // Capitalised here rather than stored capitalised: the observation is a fact that reads as
        // a clause anywhere it is used, and only this sentence starts with it.
        var where = char.ToUpperInvariant(seen.Fact[0]) + seen.Fact[1..];

        return $"It has been {Elapsed(away)}. {where}";
    }

    /// <summary>
    /// The nearest thing a plan is short of. <b>One material, not the list</b> — the shortfall
    /// report already exists and is a page long, and this is a sentence somebody hears while they
    /// are still putting their headset on.
    /// </summary>
    private string? Short(Journal.CommanderGameState? state)
    {
        var items = checklists.Document.Items;

        var shortfall = EngineeringPlan.Cost(items, state).Shortfall
            .Concat(OnFootPlan.Cost(items, state).Shortfall)

            // The closest to done, because that is the one worth doing something about today —
            // and because "you are two short" is an errand where "you are ninety short" is a
            // project (list.md Phase 31, "you were three Selenium short").
            .OrderBy(ingredient => ingredient.Short)
            .ThenBy(ingredient => ingredient.Material.Name, StringComparer.Ordinal)
            .FirstOrDefault();

        return shortfall is null
            ? null
            : $"You were {shortfall.Short} {shortfall.Material.Name} short.";
    }

    /// <summary>
    /// The engineer the solver would go and get next, and <b>only when it is one stop</b>
    /// (remediation.md 14, item 2).
    /// <para>
    /// Reported as not useful, and the reason is the rule the clause above already follows: the
    /// nearest shortfall is picked because "you are two short" is an errand and "you are ninety
    /// short" is a project. "Broo Tarquin is still three steps out" is a project — three unlocks
    /// and two rank climbs — and it says the same thing every session until they are done, which
    /// is how a Commander learns to stop listening to the opening line.
    /// </para>
    /// <para>
    /// One step is the case worth saying: somebody they could go and get today, in the one moment
    /// they are deciding what today is for.
    /// </para>
    /// </summary>
    private string? Unlock()
    {
        if (unlocks()?.Report().Route is not [{ } best, ..])
        {
            return null;
        }

        return best.Chain.Steps.Count == 1
            ? $"{best.Engineer.Name} is one stop away."
            : null;
    }

    /// <summary>
    /// A gap in the words a person would use. Coarse on purpose: the difference between 61 and 78
    /// hours is not something anybody wants read out, and an exact figure invites the Commander to
    /// check it.
    /// </summary>
    private static string Elapsed(TimeSpan away)
    {
        if (away.TotalDays < 1)
        {
            return $"{away.TotalHours:0} hours";
        }

        if (away.TotalDays < 2)
        {
            return "a day";
        }

        if (away.TotalDays < 25)
        {
            return $"{away.TotalDays:0} days";
        }

        var months = (int)Math.Round(away.TotalDays / 30, MidpointRounding.AwayFromZero);

        return months <= 1 ? "a month" : $"{months} months";
    }

}
