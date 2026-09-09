using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>A remark, now and then, about where the Commander actually is (Phase 11, "Ambient Voice").</summary>
public sealed class AmbientCallout : ICallout
{
    public string Id => "ambient";

    public const string KeyPrefix = "ambient.";

    /// <summary>Off means no unprompted remarks, whatever else is enabled.</summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>The shortest gap between two remarks.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// And the longest (#258): each cycle waits somewhere inside [<see cref="Interval"/>, <see
    /// cref="Longest"/>], the same spread <see cref="NpcChatterCallout"/> already has and for a
    /// stronger reason.
    /// </summary>
    public TimeSpan Longest { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>How long a situation has to hold before it is worth remarking on.</summary>
    public TimeSpan Settle { get; set; } = TimeSpan.FromSeconds(90);

    private AmbientSituation _situation = AmbientSituation.None;
    private AmbientSituation _lastSpoken = AmbientSituation.None;
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

        // Priming folds the backlog.
        if (context.IsPriming || !Enabled() || Interval <= TimeSpan.Zero || situation == AmbientSituation.None)
        {
            yield break;
        }

        // The first tick of a session has _lastSpokenAt at default, which is far enough in the past that the
        // interval passes immediately — so it is seeded here rather than special cased, and the Commander
        // gets silence for one interval after launch rather than a remark while they are still reading the
        // panel.
        if (_lastSpokenAt == default)
        {
            _lastSpokenAt = context.Now;
            yield break;
        }

        if (context.Now - _situationSince < Settle || context.Now - _lastSpokenAt < Gap())
        {
            yield break;
        }

        // Not on the heels of the other kind (#257).
        if (CalloutEngine.ChatterOwesQuiet(context.LastChatter, context.Now, Interval))
        {
            yield break;
        }

        // Never the same situation twice running.
        if (situation == _lastSpoken)
        {
            yield break;
        }

        if (AmbientLines.Pick(situation, _picks) is not { } line)
        {
            yield break;
        }

        var variant = _picks++;
        _lastSpoken = situation;
        _lastSpokenAt = context.Now;

        yield return new Announcement($"{KeyPrefix}{situation}".ToLowerInvariant(), line)
        {
            // Routine, and it never silences anything.
            Urgency = CalloutUrgency.Routine,
            Cooldown = Interval,

            // Said because nothing happened, and the rate the Commander asked for it at — which is both the
            // flag the engine spaces on and the clamp that stops it ever holding this longer than this row
            // asks (#257).
            Chatter = Interval,

            // The index the stock line was picked with, so the model-written replacement can be chosen by the
            // same count — which ambient remark this is decides whether the Commander's story goes with it
            // (Phase 43).
            Variant = variant,
        };
    }

    /// <summary>This cycle's wait, somewhere in [<see cref="Interval"/>, <see cref="Longest"/>].</summary>
    private TimeSpan Gap()
    {
        if (Longest <= Interval)
        {
            return Interval;
        }

        var fraction = unchecked((uint)_picks * 2654435761u) / 4294967296.0;

        return Interval + (Longest - Interval) * fraction;
    }

    /// <summary>What the last emitted line was about, so the app can ask the model for one in character.</summary>
    public AmbientSituation LastSituation => _lastSpoken;
}
