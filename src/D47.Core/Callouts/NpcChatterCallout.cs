using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>Overheard chatter, now and then, from people who do not exist (#244).</summary>
public sealed class NpcChatterCallout(NearbyFight fight) : ICallout
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

    /// <summary>
    /// Whether a ship scene is answering the fight; while it is, kills are left to the scene and no combat
    /// exchange is made for them, then or later.
    /// </summary>
    public Func<bool> SceneHoldsTheFight { get; set; } = () => false;

    private readonly UnpromptedPace _pace = new(1);
    private DateTimeOffset _lastCombatAt;
    private DateTimeOffset _sceneTookKillAt;

    /// <summary>Added to the exchange number that picks cast, topic and opening, so launches start at different picks.</summary>
    public int VariantOffset { get; init; }

    /// <summary>How long a ship kill stays worth reacting to.</summary>
    public static readonly TimeSpan KillFresh = TimeSpan.FromMinutes(2);

    /// <summary>How long the fight must have been quiet before bystanders react to a kill.</summary>
    public static readonly TimeSpan KillQuiet = TimeSpan.FromSeconds(20);

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var situation = AmbientLines.Situate(context.Status);
        var sceneHolds = SceneHoldsTheFight();

        if (sceneHolds && fight.Snapshot.LastKillAt is { } taken)
        {
            _sceneTookKillAt = taken;
        }

        // Status.json sets the supercruise flag in the hyperspace tunnel too; a charging or running jump counts
        // as no situation, so arrival restarts the settle.
        if (context.Status.Has(StatusFlags.FsdJump) || context.Status.Has(StatusFlags.FsdCharging))
        {
            situation = AmbientSituation.None;
        }

        _pace.Watch(situation, context.Now);

        if (context.IsPriming
            || !Enabled()
            || Interval <= TimeSpan.Zero
            || situation is AmbientSituation.None)
        {
            yield break;
        }

        if (situation == AmbientSituation.NormalSpace && !sceneHolds && ReactsToKill(context))
        {
            _pace.Spoke(context.Now);
            _lastCombatAt = context.Now;

            yield return Marker(NpcChatterKind.Combat);
            yield break;
        }

        // The first tick of a session is held, so the Commander gets one interval of silence after launch.
        if (!_pace.Due(context.Now, Interval, Longest, Settle))
        {
            yield break;
        }

        // No small talk in the middle of a fight; the exchange waits until it is over.
        if (fight.On(context.Now, context.Status) || context.InOperation)
        {
            yield break;
        }

        // Not on the heels of the other kind (#257), the ambient guard for the ambient reason and one of its
        // own: a marker held here never reaches Drain, so it is never composed, never sent and never billed.
        if (CalloutEngine.ChatterOwesQuiet(context.LastChatter, context.Now, Interval))
        {
            yield break;
        }

        var kind = KindFor(_pace.Picks, context.Status.Has(StatusFlags.Docked));

        // Passersby and Hail invent people who are not there where nobody lives (#230); Controller only
        // fires docked, where a station or the Commander's own carrier already justifies the scene.
        if (kind is NpcChatterKind.Passersby or NpcChatterKind.Hail
            && context.State?.Location.Population == 0)
        {
            yield break;
        }

        _pace.Spoke(context.Now);

        yield return Marker(kind);
    }

    /// <summary>Whether a ship kill newer than the last combat exchange is fresh and the fight has gone quiet.</summary>
    private bool ReactsToKill(CalloutContext context)
    {
        var snapshot = fight.Snapshot;

        return snapshot.LastKillAt is { } killed
            && snapshot.LastShip is not null
            && killed > _lastCombatAt
            && killed > _sceneTookKillAt
            && context.Now - killed < KillFresh
            && (snapshot.LastActionAt is not { } acted || context.Now - acted >= KillQuiet)
            && (_lastCombatAt == default || context.Now - _lastCombatAt >= Interval)
            && !CalloutEngine.ChatterOwesQuiet(context.LastChatter, context.Now, Interval);
    }

    // Text deliberately empty: this is a marker the app composes from, and an empty line is one that can
    // never be spoken by mistake if a road is ever missed.
    private Announcement Marker(NpcChatterKind kind) =>
        new($"{NpcChatter.KeyPrefix}{kind}".ToLowerInvariant(), string.Empty)
        {
            Urgency = CalloutUrgency.Routine,
            Origin = AnnouncementOrigin.InventedChatter,
            Cooldown = Interval,
            Variant = VariantOffset + _pace.Picks - 1,

            // Said because nothing happened, and the rate the Commander asked for it at — the flag the engine
            // spaces on and the clamp that bounds how long it may hold this (#257).
            Chatter = Interval,
        };

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
