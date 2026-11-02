using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>A remark, now and then, about where the Commander actually is (Phase 11, "Ambient Voice").</summary>
public sealed class AmbientCallout : ICallout
{
    public string Id => "ambient";

    public const string KeyPrefix = "ambient.";

    /// <summary>Off means no unprompted remarks, whatever else is enabled.</summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>Whether the core aboard is a stock core, which makes no idle remarks.</summary>
    public Func<bool> StockCoreAboard { get; set; } = () => false;

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

    private readonly UnpromptedPace _pace = new(0);
    private AmbientSituation _lastSpoken = AmbientSituation.None;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var situation = AmbientLines.Situate(context.Status);

        _pace.Watch(situation, context.Now);

        // Priming folds the backlog.
        if (context.IsPriming || !Enabled() || StockCoreAboard() || Interval <= TimeSpan.Zero || situation == AmbientSituation.None)
        {
            yield break;
        }

        // The first tick of a session is held, so the Commander gets silence for one interval after launch.
        if (!_pace.Due(context.Now, Interval, Longest, Settle))
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

        if (AmbientLines.Pick(situation, _pace.Picks, context.Status, context.State?.Ship) is not { } line)
        {
            yield break;
        }

        var variant = _pace.Picks;
        _pace.Spoke(context.Now);
        _lastSpoken = situation;

        yield return new Announcement($"{KeyPrefix}{situation}".ToLowerInvariant(), line)
        {
            // Routine, and it never silences anything.
            Urgency = CalloutUrgency.Routine,
            Origin = AnnouncementOrigin.InventedChatter,
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
}
