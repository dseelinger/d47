using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>Overheard chatter, now and then, from people who do not exist (#244).</summary>
public sealed class NpcChatterCallout : ICallout
{
    public string Id => "npc-chatter";

    /// <summary>Off means no invented chatter, whatever else is enabled.</summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>The shortest gap between two exchanges.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(20);

    /// <summary>
    /// And the longest (asked for 2026-08-31): each cycle waits somewhere inside [<see
    /// cref="Interval"/>, <see cref="Longest"/>], because a fixed cadence is the one thing overheard
    /// traffic must not have.
    /// </summary>
    public TimeSpan Longest { get; set; } = TimeSpan.FromMinutes(40);

    /// <summary>
    /// How long a situation has to hold first — the ambient rule, for the ambient reason: a docked
    /// exchange arriving as the Commander lifts off is worse than silence.
    /// </summary>
    public TimeSpan Settle { get; set; } = TimeSpan.FromSeconds(90);

    private AmbientSituation _situation = AmbientSituation.None;
    private DateTimeOffset _situationSince;
    private DateTimeOffset _lastSpokenAt;
    private int _picks;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var situation = AmbientLines.Situate(context.Status);

        if (situation != _situation)
        {
            _situation = situation;
            _situationSince = context.Now;
        }

        if (context.IsPriming || !Enabled() || Interval <= TimeSpan.Zero || situation == AmbientSituation.None)
        {
            yield break;
        }

        // Seeded on the first tick, exactly as the ambient callout is: the Commander gets one whole interval
        // of silence after launch rather than theatre while they are still reading the panel.
        if (_lastSpokenAt == default)
        {
            _lastSpokenAt = context.Now;
            yield break;
        }

        if (context.Now - _situationSince < Settle || context.Now - _lastSpokenAt < Gap())
        {
            yield break;
        }

        // Not on the heels of the other kind (#257), the ambient guard for the ambient reason and one of its
        // own: a marker held here never reaches Drain, so it is never composed, never sent and never billed.
        if (CalloutEngine.ChatterOwesQuiet(context.LastChatter, context.Now, Interval))
        {
            yield break;
        }

        var kind = KindFor(_picks, context.Status.Has(StatusFlags.Docked));

        _picks++;
        _lastSpokenAt = context.Now;

        // Text deliberately empty: this is a marker the app composes from, and an empty line is one that can
        // never be spoken by mistake if a road is ever missed.
        yield return new Announcement($"{NpcChatter.KeyPrefix}{kind}".ToLowerInvariant(), string.Empty)
        {
            Urgency = CalloutUrgency.Routine,
            Cooldown = Interval,
            Variant = _picks - 1,

            // Said because nothing happened, and the rate the Commander asked for it at — the flag the engine
            // spaces on and the clamp that bounds how long it may hold this (#257).
            Chatter = Interval,
        };
    }

    /// <summary>This cycle's wait, somewhere in [<see cref="Interval"/>, <see cref="Longest"/>].</summary>
    private TimeSpan Gap()
    {
        if (Longest <= Interval)
        {
            return Interval;
        }

        var fraction = unchecked((uint)(_picks + Offset) * 2654435761u) / 4294967296.0;

        return Interval + (Longest - Interval) * fraction;
    }

    /// <summary>How far this callout's spread is wound on from the ambient one's.</summary>
    private const int Offset = 1;

    /// <summary>Which pairing this exchange is.</summary>
    public static NpcChatterKind KindFor(int pick, bool docked)
    {
        if (pick % 4 == 3)
        {
            return NpcChatterKind.Hail;
        }

        return docked && pick % 2 == 0 ? NpcChatterKind.Controller : NpcChatterKind.Passersby;
    }
}
