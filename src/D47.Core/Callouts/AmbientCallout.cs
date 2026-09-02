using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// A remark, now and then, about where the Commander actually is (Phase 11, "Ambient
/// Voice").
/// <para>
/// Everything else in Phase 8 speaks because something happened. This speaks because nothing
/// has, which makes the timing the whole of the design. Three rules keep it from being noise:
/// it waits out an interval, it waits for the situation to have settled rather than firing on
/// the transition, and it never repeats a situation twice running.
/// </para>
/// <para>
/// Silent while personality is off. The checklist puts "no ambient remarks" in that item's own
/// acceptance criteria, which makes this the one callout the personality switch reaches.
/// </para>
/// <para>
/// Emits the stock line. The app replaces it with a model-written one in the core's own voice
/// when there is a model, the same way the carrier lines are varied — which is why the
/// situation travels on the announcement rather than only the text.
/// </para>
/// </summary>
public sealed class AmbientCallout : ICallout
{
    public string Id => "ambient";

    public const string KeyPrefix = "ambient.";

    /// <summary>Off means no unprompted remarks, whatever else is enabled.</summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>
    /// The shortest gap between two remarks. A Commander who wants a talkative companion turns
    /// it down; one who wants a quiet one turns it up. Zero silences it entirely.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// And the longest (<a href="https://github.com/dseelinger/d47/issues/258">#258</a>): each
    /// cycle waits somewhere inside [<see cref="Interval"/>, <see cref="Longest"/>], the same
    /// spread <see cref="NpcChatterCallout"/> already has and for a stronger reason. Chatter is
    /// a rotating cast of strangers, which disguises a beat; this is the same voice every time,
    /// at a fraction of the gap. At or below <see cref="Interval"/> it pins the cadence, which
    /// is what keeps every older test and every settings file meaning what it did.
    /// </summary>
    public TimeSpan Longest { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How long a situation has to hold before it is worth remarking on. Without this, a remark
    /// about being docked arrives as the Commander is lifting off — Status.json flips several
    /// times a minute during an approach, and the ambient line is the slowest thing in the app.
    /// </summary>
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

        // Priming folds the backlog. An ambient remark from an hour ago is not ambient.
        if (context.IsPriming || !Enabled() || Interval <= TimeSpan.Zero || situation == AmbientSituation.None)
        {
            yield break;
        }

        // The first tick of a session has _lastSpokenAt at default, which is far enough in the
        // past that the interval passes immediately — so it is seeded here rather than special
        // cased, and the Commander gets silence for one interval after launch rather than a
        // remark while they are still reading the panel.
        if (_lastSpokenAt == default)
        {
            _lastSpokenAt = context.Now;
            yield break;
        }

        if (context.Now - _situationSince < Settle || context.Now - _lastSpokenAt < Gap())
        {
            yield break;
        }

        // Not on the heels of the other kind (#257). Asked here as well as in the engine, and
        // asked above everything that moves below: a remark turned away after the fact has
        // already spent _lastSpokenAt and _picks, so it would pay a whole interval of silence
        // for a collision that lasted one tick — and would leave a hole in the pick counter
        // that Variant, and therefore whether the Commander's story rides along, is chosen by.
        // Held rather than lost: nothing here moves, so this is still due on the tick the air
        // clears.
        if (CalloutEngine.ChatterOwesQuiet(context.LastChatter, context.Now, Interval))
        {
            yield break;
        }

        // Never the same situation twice running. Two remarks about being in supercruise, an
        // interval apart, is the point at which ambient stops sounding like company.
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
            // Routine, and it never silences anything. An ambient remark that talked over a
            // fuel warning would be the worst possible trade.
            Urgency = CalloutUrgency.Routine,
            Cooldown = Interval,

            // Said because nothing happened, and the rate the Commander asked for it at — which
            // is both the flag the engine spaces on and the clamp that stops it ever holding
            // this longer than this row asks (#257).
            Chatter = Interval,

            // The index the stock line was picked with, so the model-written replacement can be
            // chosen by the same count — which ambient remark this is decides whether the
            // Commander's story goes with it (Phase 43).
            Variant = variant,
        };
    }

    /// <summary>
    /// This cycle's wait, somewhere in [<see cref="Interval"/>, <see cref="Longest"/>].
    /// Deterministic off the pick counter — a Knuth multiplicative hash, because no Core
    /// component reads a clock or a seed and a recorded session has to replay to the same
    /// spacing — and stable within a cycle, since <c>_picks</c> only moves on emission. The same
    /// shape as <see cref="NpcChatterCallout"/>'s on purpose: two rows of the same kind
    /// disagreeing about their own edge cases is worse than neither having the spread.
    /// </summary>
    private TimeSpan Gap()
    {
        if (Longest <= Interval)
        {
            return Interval;
        }

        var fraction = unchecked((uint)_picks * 2654435761u) / 4294967296.0;

        return Interval + (Longest - Interval) * fraction;
    }

    /// <summary>
    /// What the last emitted line was about, so the app can ask the model for one in character.
    /// Read straight after <see cref="Examine"/> yields, which is the only moment it is
    /// meaningful.
    /// </summary>
    public AmbientSituation LastSituation => _lastSpoken;
}
