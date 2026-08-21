using System.Text;
using D47.Core.Checklists;
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
/// <b>Assembled in Core, with no model in the path.</b> The facts come from the memory store and
/// the checklist's own order, and the sentence is authored here — then it goes through the
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
/// <param name="checklists">The Commander's own list, whose top is most of what gets said.</param>
public sealed class ContinuityCallout(
    MemoryBook book,
    ChecklistService checklists) : ICallout
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

        // **The engineer under the Commander's feet outranks everything the list says**
        // (reported 2026-08-20, and the ruling survives Phase 42 intact). Standing in Lei Cheung's
        // system with thirty items he could roll, d47 once said "Selene Jean is one stop away" —
        // an unlock hint about somebody else, which also reads as a distance and is not one. What
        // is under their feet is an errand, and an errand still outranks a project one unlock
        // step away — including the top-ranked one.
        if (Here(state) is { } here)
        {
            clauses.Add(here);
        }

        // The top of the list, said out loud (list.md Phase 42). This replaces the shortfall and
        // unlock clauses outright: their Gap → Short → Here-or-Unlock precedence was this
        // callout's own invention, needed only because the checklist had no order to read from.
        // Now it has one, an unlock is mentioned when an unlock item is genuinely near the top of
        // *this* Commander's list and not otherwise — which answers "I don't always care about
        // the next engineer to unlock" structurally, instead of by deleting a clause somebody
        // will want back.
        if (Top(state) is { } top)
        {
            clauses.Add(top);
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
    /// The next few things on the list, in the order the Commander cares about (list.md Phase 42).
    /// <b>Three, not the list</b> — the page holds the rest, and this is a sentence somebody hears
    /// while they are still putting their headset on.
    /// <para>
    /// Said whole through <see cref="ChecklistWording.Line"/>, ship and all: there is no heading
    /// around a spoken sentence, and the Commander has flown three ships since these were written.
    /// </para>
    /// </summary>
    private string? Top(Journal.CommanderGameState? state)
    {
        var next = ChecklistOrdering.Arrange(checklists.Document, state)
            .Where(item => !item.IsComplete)
            .Take(ChecklistOrdering.Spoken)
            .Select(item => ChecklistWording.Line(item, state))
            .ToList();

        return next.Count == 0
            ? null
            : $"Top of your list: {string.Join("; then ", next)}.";
    }

    /// <summary>
    /// The engineer in this system and what of the list they could roll today, or null where there
    /// is none or they can do nothing (asked for 2026-08-20).
    /// <para>
    /// <b>The most useful sentence available when a Commander docks at an engineer</b>, and the one
    /// d47 had every input for and never said. See <see cref="Checklists.EngineersHere"/>.
    /// </para>
    /// </summary>
    private string? Here(Journal.CommanderGameState? state) =>
        Checklists.EngineersHere.For(checklists.Document.Items, state)
            .FirstOrDefault(found => found.Ready.Count > 0)
            ?.Describe();

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
