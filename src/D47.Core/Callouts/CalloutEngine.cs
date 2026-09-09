using System.Collections.Concurrent;
using D47.Core.Journal;
using D47.Core.Ticking;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>Everything a callout is allowed to look at on one tick.</summary>
/// <param name="Now">The tick's time, injected like everywhere else in Core.</param>
/// <param name="IsPriming">
/// True on the startup tick, which replays the whole journal backlog.
/// </param>
/// <param name="LastChatter">
/// The last thing d47 said because nothing had happened, or null if it has said nothing this session
/// (#257).
/// </param>
public readonly record struct CalloutContext(
    DateTimeOffset Now,
    bool IsPriming,
    CommanderGameState? State,
    GameStatus Status,
    NavRoute Route,
    IReadOnlyList<JournalEvent> Events,
    ChatterSaid? LastChatter = null);

/// <summary>
/// Something d47 said because nothing had happened — when, and the rate the row it came from asks for
/// (#257).
/// </summary>
/// <param name="At">When it was queued.</param>
/// <param name="Asked">
/// The least time between two of its kind, as the Commander set it — carried on <see
/// cref="Announcement.Chatter"/>.
/// </param>
public readonly record struct ChatterSaid(DateTimeOffset At, TimeSpan Asked);

/// <summary>One thing d47 might say unprompted.</summary>
public interface ICallout
{
    /// <summary>Stable id, used for the enable/disable setting and for logging.</summary>
    string Id { get; }

    /// <summary>Anything worth saying this tick.</summary>
    IEnumerable<Announcement> Examine(CalloutContext context);
}

/// <summary>The tick-loop subscriber that runs every callout and queues what they produce (Phase 8).</summary>
public sealed class CalloutEngine(ILogger<CalloutEngine> logger)
{
    private readonly List<ICallout> _callouts = [];
    private readonly Dictionary<string, DateTimeOffset> _spokenAt = new(StringComparer.Ordinal);
    private readonly Dictionary<Audio.AlertCue, DateTimeOffset> _markedAt = [];
    private readonly ConcurrentQueue<Announcement> _pending = new();
    private readonly HashSet<string> _disabled = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>When each callout last actually said something, by callout id (#162).</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _lastSpokeBy =
        new(StringComparer.Ordinal);

    /// <summary>
    /// How close two alarms of the same kind may fall before the second is played as words alone
    /// (#136).
    /// </summary>
    private static readonly TimeSpan CueSpacing = TimeSpan.FromSeconds(10);

    /// <summary>The least air between two things d47 said because nothing happened (#257).</summary>
    public static readonly TimeSpan ChatterSpacing = TimeSpan.FromSeconds(90);

    /// <summary>
    /// The floor in force between a line whose row asks for <paramref name="spoken"/> and one whose row
    /// asks for <paramref name="asked"/> — the least of the three (#257).
    /// </summary>
    public static TimeSpan ChatterSpacingFor(TimeSpan asked, TimeSpan spoken)
    {
        var floor = ChatterSpacing;

        if (asked < floor)
        {
            floor = asked;
        }

        return spoken < floor ? spoken : floor;
    }

    /// <summary>
    /// Whether an unprompted line whose row asks for <paramref name="asked"/> still owes the Commander
    /// quiet, given what was last said unprompted (#257).
    /// </summary>
    public static bool ChatterOwesQuiet(ChatterSaid? last, DateTimeOffset now, TimeSpan asked) =>
        last is { } said && now - said.At < ChatterSpacingFor(asked, said.Asked);

    /// <summary>What was last said unprompted, or null if nothing has been this session (#257).</summary>
    private ChatterSaid? _lastChatter;

    /// <summary>Whether callouts are on at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How soon after speaking a callout has to be silenced for the two to be read as connected (#162).
    /// </summary>
    public static readonly TimeSpan SilencedSoonAfter = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Raised when a callout is switched off within <see cref="SilencedSoonAfter"/> of it last speaking
    /// (#162).
    /// </summary>
    public event Action<CalloutSilenced>? Silenced;

    public IReadOnlyList<ICallout> Callouts => _callouts;

    public CalloutEngine Add(ICallout callout)
    {
        _callouts.Add(callout);
        return this;
    }

    /// <summary>Turns one callout off by id.</summary>
    /// <param name="now">
    /// When the Commander did it, or null where the caller has no clock and does not want the signal.
    /// </param>
    public void SetEnabled(string id, bool enabled, DateTimeOffset? now = null)
    {
        bool silenced;

        lock (_disabled)
        {
            if (enabled)
            {
                _disabled.Remove(id);
                return;
            }

            // The transition, not the state.
            silenced = _disabled.Add(id);
        }

        if (!silenced || now is not { } at || !_lastSpokeBy.TryGetValue(id, out var spoke))
        {
            return;
        }

        var after = at - spoke;

        if (after >= TimeSpan.Zero && after <= SilencedSoonAfter)
        {
            Silenced?.Invoke(new CalloutSilenced(id, at, after));
        }
    }

    public bool IsEnabled(string id)
    {
        lock (_disabled)
        {
            return !_disabled.Contains(id);
        }
    }

    /// <summary>The tick-loop entry point.</summary>
    public void Tick(CalloutContext context)
    {
        // Priming still runs.
        if (!Enabled && !context.IsPriming)
        {
            return;
        }

        foreach (var callout in _callouts)
        {
            if (!IsEnabled(callout.Id))
            {
                continue;
            }

            try
            {
                // Derived fresh per callout off the engine's own field, never off the struct it was handed:
                // two unprompted callouts examined on one tick must not both read "nothing has been said"
                // (#257).
                foreach (var announcement in callout.Examine(context with { LastChatter = _lastChatter }))
                {
                    if (Offer(announcement, context))
                    {
                        _lastSpokeBy[callout.Id] = context.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                // One broken callout must not silence the rest — and must certainly not take the danger
                // warnings down with it.
                logger.LogError(ex, "Callout {Id} threw while examining a tick", callout.Id);
            }
        }
    }

    /// <summary>
    /// Queues one announcement, and says whether it queued it — which is what makes <see
    /// cref="_lastSpokeBy"/> a record of what was said rather than of what was examined.
    /// </summary>
    private bool Offer(Announcement announcement, CalloutContext context)
    {
        // Never during priming, whatever a callout returns.
        if (context.IsPriming || !Enabled)
        {
            return false;
        }

        // The floor between two unprompted lines (#257), asked here rather than trusted to the callouts that
        // already asked it.
        if (announcement.Chatter is { } asked && ChatterOwesQuiet(_lastChatter, context.Now, asked))
        {
            return false;
        }

        if (announcement.Cooldown > TimeSpan.Zero &&
            _spokenAt.TryGetValue(announcement.Key, out var last) &&
            context.Now - last < announcement.Cooldown)
        {
            return false;
        }

        _spokenAt[announcement.Key] = context.Now;

        // Stamped on the way through rather than in Tick, because Offer can still refuse for priming, for
        // Enabled or for the key cooldown — stamping where the callout spoke would hold the other voice for
        // something that was never queued.
        if (announcement.Chatter is { } rate)
        {
            _lastChatter = new ChatterSaid(context.Now, rate);
        }

        // After the key cooldown, never before it: an announcement that is not going to be said must not
        // consume the spacing that would silence the alarm of one that is.
        announcement = Marked(announcement, context.Now);

        _pending.Enqueue(announcement);

        logger.LogInformation("Callout {Key}: {Text}", announcement.Key, announcement.Text);

        return true;
    }

    /// <summary>
    /// The announcement with its alarm kept or dropped, by how recently that same alarm sounded (#136).
    /// </summary>
    private Announcement Marked(Announcement announcement, DateTimeOffset now)
    {
        if (announcement.Cue is not { } cue)
        {
            return announcement;
        }

        if (_markedAt.TryGetValue(cue, out var last) && now - last < CueSpacing)
        {
            // The line still goes.
            return announcement with { Cue = null };
        }

        _markedAt[cue] = now;

        return announcement;
    }

    /// <summary>Whether anything queued behind is more than routine (#259).</summary>
    public bool AnythingUrgentWaiting => _pending.Any(pending => pending.Urgency == CalloutUrgency.Urgent);

    /// <summary>Takes everything queued since the last call.</summary>
    public IReadOnlyList<Announcement> Drain()
    {
        if (_pending.IsEmpty)
        {
            return [];
        }

        var drained = new List<Announcement>();

        while (_pending.TryDequeue(out var announcement))
        {
            drained.Add(announcement);
        }

        return drained;
    }
}

/// <summary>A callout the Commander switched off within seconds of hearing it (#162).</summary>
/// <param name="Id">Which callout, by the id the settings rows use.</param>
/// <param name="When">When it was switched off.</param>
/// <param name="After">How long after it last spoke.</param>
public sealed record CalloutSilenced(string Id, DateTimeOffset When, TimeSpan After);
