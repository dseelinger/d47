using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Overheard chatter, now and then, from people who do not exist (#244).
/// <para>
/// The same three timing rules as <see cref="AmbientCallout"/>, because they are what keep
/// unprompted speech from being noise: it waits out an interval, it waits for the situation to
/// have settled rather than firing on the transition, and it never fires while priming folds
/// the backlog.
/// </para>
/// <para>
/// <b>What it emits is a marker, never a line.</b> Chatter is model-written or it is nothing
/// (#245): the announcement's key carries the pairing and its text is empty, the app composes
/// the exchange and speaks the parsed lines in invented voices, and with no model the marker
/// composes to nothing and nobody hears anything. There is no authored fallback on purpose.
/// </para>
/// <para>
/// <b>One-way, by construction.</b> The lines the app speaks ride <c>VoiceRole.Comms</c> with a
/// speaker name, so nothing enters the conversation history — the Commander is not in this
/// conversation, which is the point.
/// </para>
/// </summary>
public sealed class NpcChatterCallout : ICallout
{
    public string Id => "npc-chatter";

    /// <summary>Off means no invented chatter, whatever else is enabled.</summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>
    /// The shortest gap between two exchanges. Longer than the ambient default out of the box:
    /// a remark is one sentence and an exchange is a scene, and scenes wear out faster. Zero
    /// silences it entirely.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(20);

    /// <summary>
    /// And the longest (asked for 2026-08-31): each cycle waits somewhere inside
    /// [<see cref="Interval"/>, <see cref="Longest"/>], because a fixed cadence is the one
    /// thing overheard traffic must not have. At or below <see cref="Interval"/> it pins the
    /// cadence, which is also what keeps every older test and setting meaning what it did.
    /// </summary>
    public TimeSpan Longest { get; set; } = TimeSpan.FromMinutes(40);

    /// <summary>
    /// How long a situation has to hold first — the ambient rule, for the ambient reason: a
    /// docked exchange arriving as the Commander lifts off is worse than silence.
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

        // Seeded on the first tick, exactly as the ambient callout is: the Commander gets one
        // whole interval of silence after launch rather than theatre while they are still
        // reading the panel.
        if (_lastSpokenAt == default)
        {
            _lastSpokenAt = context.Now;
            yield break;
        }

        if (context.Now - _situationSince < Settle || context.Now - _lastSpokenAt < Gap())
        {
            yield break;
        }

        // Not on the heels of the other kind (#257), the ambient guard for the ambient reason
        // and one of its own: a marker held here never reaches Drain, so it is never composed,
        // never sent and never billed. Above the mutations below, so nothing is spent — _picks
        // not moving is what makes this deal the same pairing and the same Variant when it does
        // fire, which is what a recorded session replays to.
        if (CalloutEngine.ChatterOwesQuiet(context.LastChatter, context.Now, Interval))
        {
            yield break;
        }

        var kind = KindFor(_picks, context.Status.Has(StatusFlags.Docked));

        _picks++;
        _lastSpokenAt = context.Now;

        // Text deliberately empty: this is a marker the app composes from, and an empty line is
        // one that can never be spoken by mistake if a road is ever missed.
        yield return new Announcement($"{NpcChatter.KeyPrefix}{kind}".ToLowerInvariant(), string.Empty)
        {
            Urgency = CalloutUrgency.Routine,
            Cooldown = Interval,
            Variant = _picks - 1,

            // Said because nothing happened, and the rate the Commander asked for it at — the
            // flag the engine spaces on and the clamp that bounds how long it may hold this
            // (#257).
            Chatter = Interval,
        };
    }

    /// <summary>
    /// This cycle's wait, somewhere in [<see cref="Interval"/>, <see cref="Longest"/>].
    /// Deterministic off the pick counter — a Knuth multiplicative hash, because no Core
    /// component reads a clock or a seed and a recorded session has to replay to the same
    /// spacing — and stable within a cycle, since <c>_picks</c> only moves on emission.
    /// <para>
    /// <b>Offset by one against the ambient callout's, on purpose</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/257">#257</a>). The two hashes are
    /// otherwise identical, they are indexed by counters that start together, and the fraction at
    /// zero is exactly zero — so both callouts served exactly their <see cref="Interval"/> on
    /// their first cycle, both are seeded on the same first live tick, and since the two pairs of
    /// rows were given the same numbers on purpose, the first remark and the first exchange of
    /// every session were due on the same tick, every session, by construction. The shared
    /// ninety-second <see cref="Settle"/> then released both together after every situation change.
    /// </para>
    /// <para>
    /// The offset de-correlates the two spreads at no cost: still off the pick counter, still no
    /// clock and no seed, so a recorded session replays to the same spacing as before for any
    /// given counter. <b>The floor is what guarantees the air; this is what stops the guarantee
    /// having to fire every cycle</b> — which would be a fixed ninety-second couplet followed by
    /// several minutes of silence, on repeat. A cadence, which is the one thing the spread exists
    /// to prevent.
    /// </para>
    /// </summary>
    private TimeSpan Gap()
    {
        if (Longest <= Interval)
        {
            return Interval;
        }

        var fraction = unchecked((uint)(_picks + Offset) * 2654435761u) / 4294967296.0;

        return Interval + (Longest - Interval) * fraction;
    }

    /// <summary>
    /// How far this callout's spread is wound on from the ambient one's. One, which is all it
    /// takes: the hash deals a different fraction for every counter, so one step apart is as
    /// de-correlated as any other and is the smallest change that says what it is for.
    /// </summary>
    private const int Offset = 1;

    /// <summary>
    /// Which pairing this exchange is. Deterministic off the pick counter, because no Core
    /// component reads a clock or a seed and a recorded session has to replay to the same call:
    /// every fourth exchange addresses the Commander, the rest alternate between the dock and
    /// the passers-by where there is a dock — the controller only exists somewhere to be docked
    /// at — and are all passers-by in flight.
    /// </summary>
    public static NpcChatterKind KindFor(int pick, bool docked)
    {
        if (pick % 4 == 3)
        {
            return NpcChatterKind.Hail;
        }

        return docked && pick % 2 == 0 ? NpcChatterKind.Controller : NpcChatterKind.Passersby;
    }
}
