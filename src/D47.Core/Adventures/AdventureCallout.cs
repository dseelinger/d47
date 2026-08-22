using D47.Core.Callouts;
using D47.Core.Journal;

namespace D47.Core.Adventures;

/// <summary>
/// A beat, said when it is reached (list.md Phase 47, "The ship's AI tells it, and the authored
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

    /// <summary>How long a reached beat waits before it is said. The opening does not wait.</summary>
    public TimeSpan Settle { get; set; } = TimeSpan.FromSeconds(20);

    private readonly List<AdventureMoment> _waiting = [];

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
