using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Adventures;

/// <summary>
/// Where one adventure stands, computed rather than read (Phase 47, "Progress is derived,
/// and an adventure counts forward only").
/// <para>
/// <see cref="Fired"/> is one stamp per beat reached, in order, and everything else is arithmetic
/// over it. <see cref="Place"/> says the current beat's title, or that the story has not begun,
/// finished, or was abandoned.
/// </para>
/// <para>
/// <b>The count reaches the Commander after all, from 2026-08-22, on their instruction.</b> Phase 47
///  wrote the opposite rule here — <em>beat 3 of 7 is checklist language and belongs to the
/// Technical transcript</em> — on the story-not-a-checklist framing that governs the whole phase.
/// Flown, the Commander asked for <em>Step X of Y</em> on both surfaces. The framing was about the
/// <em>prose</em>: the beats are still dramatic functions with titles rather than numbered stops,
/// and nothing generated says a number. What changed is that the panel is also the place a person
/// checks how far through an evening they are, and refusing to answer that made the tab worse at a
/// job it was already doing badly by implying it. <see cref="Step"/> is that answer, and it is the
/// only place a count is spelled; <see cref="Place"/> is unchanged and still names the beat.
/// </para>
/// </summary>
public sealed record AdventureStanding
{
    public required Adventure Adventure { get; init; }

    /// <summary>When each beat reached so far fired, oldest first. Index <c>i</c> is beat <c>i</c>.</summary>
    public IReadOnlyList<DateTimeOffset> Fired { get; init; } = [];

    /// <summary>The index of the beat the story is waiting on. Past the end once finished.</summary>
    public int Current => Fired.Count;

    public bool IsDone => Adventure.IsBegun && Fired.Count >= Adventure.Beats.Count && Adventure.Beats.Count > 0;

    public AdventureBeat? CurrentBeat =>
        Adventure.IsBegun && !IsDone && Current < Adventure.Beats.Count ? Adventure.Beats[Current] : null;

    public AdventureBeat? LastBeat => Fired.Count > 0 ? Adventure.Beats[Fired.Count - 1] : null;

    public DateTimeOffset? LastFiredAt => Fired.Count > 0 ? Fired[^1] : null;

    /// <summary>Derived — the last beat's fire time — and never written.</summary>
    public DateTimeOffset? FinishedAt => IsDone ? Fired[^1] : null;

    /// <summary>Whether the turn (the beat whose function says so) has fired, for the spoiler rule.</summary>
    public bool TurnReached => Reached("midpoint", "turn");

    public bool EndingReached => IsDone;

    /// <summary>
    /// How far through, as a count (asked for 2026-08-22). Null where a count means nothing — a
    /// draft, a story not begun, one with no beats — so a caller draws the row or does not, rather
    /// than drawing "Step 0 of 0".
    /// <para>
    /// The step is the one being <em>worked on</em>, not the number finished: a Commander who has
    /// reached nothing is on step 1, and a finished story reads as its own last step rather than
    /// as one past the end.
    /// </para>
    /// </summary>
    public string? Step()
    {
        if (!Adventure.IsBegun || Adventure.Beats.Count == 0)
        {
            return null;
        }

        var of = Adventure.Beats.Count;
        var at = Math.Min(Fired.Count + (IsDone ? 0 : 1), of);

        return $"Step {at.ToString(CultureInfo.InvariantCulture)} of {of.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// What the Commander did to reach where they are — the last beat's trigger, in words. Null
    /// before anything has fired.
    /// </summary>
    public string? LastTrigger() => LastBeat?.Trigger.Describe();

    /// <summary>
    /// What the story is waiting for the Commander to do next, in words. Null when it is not
    /// waiting for anything: finished, abandoned, or never begun.
    /// </summary>
    public string? NextTrigger() =>
        Adventure.IsActive && CurrentBeat is { } current ? current.Trigger.Describe() : null;

    /// <summary>The last thing the ship's AI actually said about this story, beat or aside.</summary>
    public AdventureTold? LastSaid() => Adventure.Told.Count > 0 ? Adventure.Told[^1] : null;

    /// <summary>Where the story is, in words a card can show.</summary>
    public string Place()
    {
        if (Adventure.IsAbandoned)
        {
            return CurrentBeatTitle() is { } waiting ? $"abandoned at {waiting}" : "abandoned";
        }

        if (!Adventure.IsBegun)
        {
            return Adventure.IsDraft ? "waiting for your yes" : "not begun";
        }

        if (IsDone)
        {
            return "finished";
        }

        return CurrentBeatTitle() ?? "under way";
    }

    /// <summary>The whole standing in one sentence, for the spoken path and the Technical transcript.</summary>
    public string Describe(DateTimeOffset now)
    {
        var parts = new List<string> { $"{Adventure.Name}: {Place()}." };

        if (Adventure.IsBegun && !IsDone && !Adventure.IsAbandoned && LastFiredAt is { } last)
        {
            parts.Add($"Last beat {Ago(now - last)}.");
        }

        if (Adventure.IsBegun && Adventure.Beats.Count > 0)
        {
            parts.Add(
                $"({Math.Min(Fired.Count, Adventure.Beats.Count).ToString(CultureInfo.InvariantCulture)} of "
                + $"{Adventure.Beats.Count.ToString(CultureInfo.InvariantCulture)} beats)");
        }

        return string.Join(" ", parts);
    }

    private string? CurrentBeatTitle() =>
        Current < Adventure.Beats.Count ? Adventure.Beats[Current].Title : null;

    private bool Reached(params string[] functions)
    {
        for (var index = 0; index < Fired.Count && index < Adventure.Beats.Count; index++)
        {
            if (Adventure.Beats[index].Function is { } function
                && functions.Any(wanted => function.Contains(wanted, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string Ago(TimeSpan age) => age.TotalMinutes switch
    {
        < 1 => "just now",
        < 60 => $"{(int)age.TotalMinutes} minutes ago",
        < 1440 => $"{(int)age.TotalHours} hours ago",
        < 20160 => $"{(int)age.TotalDays} days ago",
        _ => $"{(int)(age.TotalDays / 7)} weeks ago",
    };
}

/// <summary>
/// The one fold, for the live tick and the startup catch-up alike (Phase 47).
/// <para>
/// Pure: owns no thread, reads no clock, takes the event's own timestamp. Ignores everything before
/// <see cref="Adventure.AcceptedAt"/> — <em>adventures start when accepted and mine no history</em>
/// — and everything from <see cref="Adventure.AbandonedAt"/> on. <b>Only the current beat can
/// match.</b> A later beat's place reached early is not banked; an earlier beat's place revisited
/// does nothing. That is what makes the corpus question a sentence: fired at each place in order,
/// once, and at nothing before the stamp.
/// </para>
/// </summary>
public static class AdventureFold
{
    /// <summary>Whether one journal event is what this trigger waits for. A field comparison, never text.</summary>
    public static bool Matches(AdventureTrigger trigger, JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(journalEvent);

        if (!trigger.IsResolved)
        {
            return false;
        }

        var raw = journalEvent.Raw;

        return trigger.Kind switch
        {
            TriggerKind.Arrive =>
                journalEvent.Kind is "FSDJump" or "Location" or "CarrierJump"
                && raw.Long("SystemAddress") == trigger.SystemAddress,

            TriggerKind.Dock =>
                journalEvent.Kind is "Docked"
                && raw.Long("MarketID") == trigger.MarketId,

            TriggerKind.Land =>
                journalEvent.Kind is "Touchdown"
                && raw.Long("SystemAddress") == trigger.SystemAddress
                && raw.Int("BodyID") == trigger.BodyId,

            // A scan beat is satisfied by the scan, or by the Commander going to the body
            // (#77). Elite writes Scan when a body enters the Commander's discovered set and
            // then, overwhelmingly, never again: across the corpus's three Commanders only
            // 239 of 7,091, 155 of 2,722 and 289 of 11,727 scanned bodies were ever seen in a
            // second session, and most of those are a nav beacon re-reading a whole system.
            // So a story that sends a Commander to a body they have already been to waits on
            // an event Elite has already spent, and the beat can never fire — which stuck the
            // first flown story, with abandon as the only way out.
            //
            // ApproachBody and SupercruiseExit are the evidence that they went. Both carry
            // SystemAddress and BodyID on every occurrence, and both fire regardless of what
            // was discovered when. ApproachBody alone is not enough: across 1,203 of them not
            // one is a star and not one a body that cannot be landed on. SupercruiseExit
            // covers the rest, firing on already-scanned stars, non-landable and landable
            // bodies alike. SupercruiseDestinationDrop is not here — it carries no body
            // fields at all (0 of 3,910), and a Commander drops at a destination when they
            // mean to land rather than to look.
            //
            // This can fire the beat on arrival rather than on the scan: of 528 first-ever
            // scans where the Commander also reached the body in normal space that session,
            // 90 reached it first. It costs the instant and never the place — in every one of
            // those the scan followed in the same session — and unsticking a dead end is
            // worth an early beat. Touchdown is deliberately absent as redundant: a body that
            // can be landed on is one ApproachBody already fired for.
            TriggerKind.Scan =>
                journalEvent.Kind is "Scan" or "ApproachBody" or "SupercruiseExit"
                && raw.Long("SystemAddress") == trigger.SystemAddress
                && raw.Int("BodyID") == trigger.BodyId,

            TriggerKind.Rank =>
                journalEvent.Kind is "Promotion"
                && trigger.Career is { } career
                && raw.Int(career) is { } reached
                && reached >= trigger.Rank,

            _ => false,
        };
    }

    /// <summary>
    /// One event against one standing. Returns the same instance when nothing moved, which is the
    /// overwhelmingly common case — this runs for every event of every active adventure.
    /// </summary>
    public static AdventureStanding Apply(AdventureStanding standing, JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(standing);
        ArgumentNullException.ThrowIfNull(journalEvent);

        var adventure = standing.Adventure;

        if (!adventure.IsBegun || standing.IsDone)
        {
            return standing;
        }

        if (journalEvent.Timestamp < adventure.AcceptedAt)
        {
            return standing;
        }

        if (adventure.AbandonedAt is { } abandoned && journalEvent.Timestamp >= abandoned)
        {
            return standing;
        }

        var current = standing.CurrentBeat;

        if (current is null || !Matches(current.Trigger, journalEvent))
        {
            return standing;
        }

        return standing with { Fired = [.. standing.Fired, journalEvent.Timestamp] };
    }

    /// <summary>A fresh standing: begun or not, nothing fired yet.</summary>
    public static AdventureStanding Start(Adventure adventure) => new() { Adventure = adventure };
}
