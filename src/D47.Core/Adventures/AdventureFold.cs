using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Adventures;

/// <summary>
/// Where one adventure stands, computed rather than read (list.md Phase 47, "Progress is derived,
/// and an adventure counts forward only").
/// <para>
/// <see cref="Fired"/> is one stamp per beat reached, in order, and everything else is arithmetic
/// over it. <b>No count reaches the Commander</b>: the card says the current beat's title, or that
/// the story has not begun, finished, or was abandoned — <em>beat 3 of 7</em> is checklist language
/// and belongs to the Technical transcript.
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

    /// <summary>Where the story is, in words a card can show. Never a fraction.</summary>
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
/// The one fold, for the live tick and the startup catch-up alike (list.md Phase 47).
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

            TriggerKind.Scan =>
                journalEvent.Kind is "Scan"
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
