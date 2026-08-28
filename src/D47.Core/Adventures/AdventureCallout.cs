using D47.Core.Callouts;
using D47.Core.Journal;

namespace D47.Core.Adventures;

/// <summary>
/// A beat, said when it is reached (Phase 47, "The ship's AI tells it, and the authored
/// beat is the floor").
/// <para>
/// Takes the callout family's discipline: a settle window, so a beat is not read out over the
/// jump it arrived on and three gates in a minute are not three model calls; and a beat that comes
/// due while the Commander is in danger is <b>dropped rather than spoken late</b> — the line is
/// still in the standing context, so the core can pick it up in the next exchange, which is better
/// than hearing beat four's prose mid-interdiction. The opening is the exception to the settle: the
/// Commander just pressed Begin and is waiting for it.
/// </para>
/// <para>
/// Emits the authored line. The app replaces it with a model-written one in the core's own voice
/// when there is a model, exactly as it does for <see cref="AmbientCallout"/>, and the authored line
/// is what plays when there is no model or personality is off. This is also the one path the fold's
/// events reach the book by, so the tick loop has nothing adventure-shaped of its own.
/// </para>
/// </summary>
public sealed class AdventureCallout(AdventureBook book) : ICallout
{
    public string Id => "adventure";

    public const string KeyPrefix = "adventure.";

    /// <summary>
    /// The short acknowledgement's own prefix, and it is deliberately not <see cref="KeyPrefix"/>
    /// (asked for 2026-08-22). <c>FlavourBriefs</c> routes on the prefix, so sharing one would send
    /// the acknowledgement through the very model round trip it exists to arrive ahead of.
    /// </summary>
    public const string AckPrefix = "adventure-ack.";

    /// <summary>How long a reached beat waits before it is said. The opening does not wait.</summary>
    public TimeSpan Settle { get; set; } = TimeSpan.FromSeconds(20);

    private readonly List<AdventureMoment> _waiting = [];

    /// <summary>Which stock acknowledgement is next. The ambient pool's arrangement, for its reasons.</summary>
    private int _acks;

    /// <summary>
    /// Which story and which beat an announcement of this family is about, or null if it is not one
    /// (asked for 2026-08-22). The inverse of <see cref="AdventureMoment.Key"/>, and here rather
    /// than in the app so the one place that writes the key and the one place that reads it sit
    /// together — the app records what was <em>said</em>, and it has only the announcement to go on.
    /// </summary>
    /// <returns>The adventure's key and the beat index, with <c>-1</c> for the opening.</returns>
    public static (string Key, int Beat)? Reached(string? announcementKey)
    {
        if (announcementKey is null || !announcementKey.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = announcementKey[KeyPrefix.Length..];

        // From the right: an adventure's key is the Commander's or a model's and may hold a dot,
        // and the part after the last one is always the beat.
        var dot = rest.LastIndexOf('.');

        if (dot <= 0)
        {
            return null;
        }

        var story = rest[..dot];
        var tail = rest[(dot + 1)..];

        if (string.Equals(tail, "opening", StringComparison.Ordinal))
        {
            return (story, -1);
        }

        return int.TryParse(tail, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var beat)
            ? (story, beat)
            : null;
    }

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var commander = context.State?.Identity.FrontierId;

        foreach (var journalEvent in context.Events)
        {
            book.Observe(journalEvent, commander);
        }

        var reached = book.Drain();

        // Priming folds the backlog into the standings and announces none of it — a beat that fired
        // two hours ago is in the past, and the context block already says so.
        if (context.IsPriming)
        {
            yield break;
        }

        foreach (var moment in reached)
        {
            // Stamped with the tick rather than the journal's time, so the settle is measured from
            // when d47 learned of it rather than from a timestamp that may be a file flush behind.
            _waiting.Add(moment with { At = context.Now });

            // And the acknowledgement, now, with no settle and no model behind it — see
            // <see cref="AdventureAcks"/> for why it is split off from the beat at all. Not for the
            // opening: the Commander pressed Begin a second ago and is owed the opening itself
            // rather than being congratulated for pressing a button.
            //
            // <b>Not gated on danger, where the beat below is.</b> Two reasons. It lands on the
            // tick the event arrived rather than twenty seconds later, so it is almost never in
            // the window a beat gets dropped in; and where it is, the beat has been dropped and
            // three words are the only thing left telling the Commander their act counted — which
            // is the exact confusion this pair exists to end.
            if (!moment.IsOpening)
            {
                yield return new Announcement($"{AckPrefix}{moment.Adventure.Key}.{moment.Beat}", AdventureAcks.Pick(_acks++))
                {
                    Urgency = CalloutUrgency.Routine,
                };
            }
        }

        if (_waiting.Count == 0)
        {
            yield break;
        }

        var inDanger = context.Status.IsKnown
                       && (context.Status.Has(StatusFlags.InDanger) || context.Status.Has(StatusFlags.BeingInterdicted));

        var due = _waiting
            .Where(moment => moment.IsOpening || context.Now - moment.At >= Settle)
            .ToList();

        foreach (var moment in due)
        {
            _waiting.Remove(moment);

            if (inDanger && !moment.IsOpening)
            {
                // Dropped rather than spoken late — and the tab is told, or its "d47 is composing"
                // animation runs until something else happens to clear it.
                book.Quiet(moment.FrontierId, moment.Adventure.Key);
                continue;
            }

            // The line and where to go next, as one text: the hand-off is part of what the model
            // is told to keep when it says this in the core's voice, and what plays when there is
            // no model. See AdventureMoment.HandOff.
            yield return new Announcement(moment.Key, moment.Spoken)
            {
                Urgency = CalloutUrgency.Routine,

                // The beat index rides along so the brief can say which this is without parsing
                // the key; the opening is -1.
                Variant = moment.Beat,
            };
        }
    }
}
