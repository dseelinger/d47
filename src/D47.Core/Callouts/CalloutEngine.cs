using System.Collections.Concurrent;
using D47.Core.Journal;
using D47.Core.Ticking;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>Everything a callout is allowed to look at on one tick.</summary>
/// <param name="Now">The tick's time, injected like everywhere else in Core.</param>
/// <param name="IsPriming">
/// True on the startup tick, which replays the whole journal backlog. A callout must fold that
/// backlog into its state and announce nothing from it — otherwise starting d47 after Elite
/// means every hull hit of the last two hours is read out at once. This is exactly what
/// "the tracker is primed from the session backlog at startup" asks for.
/// </param>
public readonly record struct CalloutContext(
    DateTimeOffset Now,
    bool IsPriming,
    CommanderGameState? State,
    GameStatus Status,
    NavRoute Route,
    IReadOnlyList<JournalEvent> Events);

/// <summary>
/// One thing d47 might say unprompted. Implementations hold their own memory of what they have
/// already said — the engine only handles cooldown and delivery.
/// </summary>
public interface ICallout
{
    /// <summary>Stable id, used for the enable/disable setting and for logging.</summary>
    string Id { get; }

    /// <summary>
    /// Anything worth saying this tick. Returning nothing is the overwhelmingly common case;
    /// this runs ten times a second.
    /// </summary>
    IEnumerable<Announcement> Examine(CalloutContext context);
}

/// <summary>
/// The tick-loop subscriber that runs every callout and queues what they produce (Phase 8).
/// <para>
/// It owns the two policies that would otherwise be reimplemented, differently, in each
/// callout: <b>a callout that throws does not stop the others</b>, and <b>the same warning is
/// not repeated while its cooldown is running</b>. At 10 Hz a condition-based warning is true
/// on hundreds of consecutive ticks, so without the cooldown "low fuel" would be said until the
/// Commander refuelled or quit.
/// </para>
/// <para>
/// Nothing here speaks. <see cref="Drain"/> hands the queue to the app, which does the awaiting
/// — the tick must not block on synthesis (see <see cref="TickLoop"/>).
/// </para>
/// </summary>
public sealed class CalloutEngine(ILogger<CalloutEngine> logger)
{
    private readonly List<ICallout> _callouts = [];
    private readonly Dictionary<string, DateTimeOffset> _spokenAt = new(StringComparer.Ordinal);
    private readonly Dictionary<Audio.AlertCue, DateTimeOffset> _markedAt = [];
    private readonly ConcurrentQueue<Announcement> _pending = new();
    private readonly HashSet<string> _disabled = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// When each callout last actually said something, by callout id
    /// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
    /// <para>
    /// Not the same as <see cref="_spokenAt"/>, which is keyed by <em>announcement</em> and exists
    /// to serve a cooldown: one callout produces several keys, and the question here is about the
    /// callout the Commander just reached for the switch on.
    /// </para>
    /// <para>
    /// Concurrent because it is written on the tick thread and read on whichever thread applied a
    /// setting, which is the only cross-thread pair in this class.
    /// </para>
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _lastSpokeBy =
        new(StringComparer.Ordinal);

    /// <summary>
    /// How close two alarms of the same kind may fall before the second is played as words alone
    /// (<a href="https://github.com/dseelinger/d47/issues/136">#136</a>).
    /// <para>
    /// <b>The keys cannot answer this, which is why it is here rather than in a cooldown.</b> An
    /// announced interdiction is <c>attack.interdiction</c> and the interdiction itself is
    /// <c>danger.interdiction</c> — two different warnings, correctly, both worth saying. What is
    /// not worth doing twice in six seconds is the <em>alarm</em>: the Commander has already been
    /// told to look up, and a second identical sound carries nothing the first did not.
    /// </para>
    /// <para>
    /// Ten seconds, against a median of six to eight between an announced attack and the shooting.
    /// Long enough to cover that pair, short enough that a second engagement a minute later is
    /// marked as one. <b>Only the cue is dropped and never the line</b>, because the words are what
    /// say which warning this is and they were always the part that mattered.
    /// </para>
    /// </summary>
    private static readonly TimeSpan CueSpacing = TimeSpan.FromSeconds(10);

    /// <summary>Whether callouts are on at all. Off leaves everything else running.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How soon after speaking a callout has to be silenced for the two to be read as connected
    /// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
    /// <para>
    /// Thirty seconds: long enough to cover a Commander hearing a warning, deciding it was wrong
    /// and finding the row, and short enough that switching something off an hour later is what it
    /// looks like — an unrelated decision.
    /// </para>
    /// </summary>
    public static readonly TimeSpan SilencedSoonAfter = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Raised when a callout is switched off within <see cref="SilencedSoonAfter"/> of it last
    /// speaking (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
    /// <para>
    /// <b>A signal, and nothing more.</b> It changes no threshold and writes nothing to a prompt;
    /// the debrief turns it into a <em>question</em> at the end of the session, which the Commander
    /// answers or discards. Adapting to it silently would be a companion whose behaviour changed
    /// for a reason its Commander could not name, and the reading is genuinely ambiguous: the
    /// warning may have been wrong, or it may have been right and dealt with.
    /// </para>
    /// </summary>
    public event Action<CalloutSilenced>? Silenced;

    public IReadOnlyList<ICallout> Callouts => _callouts;

    public CalloutEngine Add(ICallout callout)
    {
        _callouts.Add(callout);
        return this;
    }

    /// <summary>
    /// Turns one callout off by id. Individually settable, because a Commander who finds route
    /// progress chatty should not have to silence danger warnings to stop it.
    /// </summary>
    /// <param name="now">
    /// When the Commander did it, or null where the caller has no clock and does not want the
    /// signal. Supplied rather than read, because no Core component reads one — and it is the only
    /// thing that makes <see cref="Silenced"/> possible at all.
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

            // The transition, not the state. This is called with every id on every settings
            // change, so "it is off" says nothing; "it has just been turned off" is the fact.
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

    /// <summary>
    /// The tick-loop entry point. Runs every enabled callout against the same context so two
    /// callouts cannot disagree about what the world looked like this tick.
    /// </summary>
    public void Tick(CalloutContext context)
    {
        // Priming still runs. A callout has to fold the backlog to know what "changed" means on
        // the first live tick — it just must not announce any of it, which is what IsPriming
        // tells it. Skipping the callouts entirely here is the bug that makes the first real
        // event after startup either fire spuriously or not fire at all.
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
                foreach (var announcement in callout.Examine(context))
                {
                    if (Offer(announcement, context))
                    {
                        _lastSpokeBy[callout.Id] = context.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                // One broken callout must not silence the rest — and must certainly not take
                // the danger warnings down with it.
                logger.LogError(ex, "Callout {Id} threw while examining a tick", callout.Id);
            }
        }
    }

    /// <summary>
    /// Queues one announcement, and says whether it queued it — which is what makes
    /// <see cref="_lastSpokeBy"/> a record of what was <em>said</em> rather than of what was
    /// examined.
    /// </summary>
    private bool Offer(Announcement announcement, CalloutContext context)
    {
        // Never during priming, whatever a callout returns. Belt and braces: the contract says
        // callouts check IsPriming themselves, and this is what makes forgetting it harmless
        // rather than a two-hour backlog read aloud at startup.
        if (context.IsPriming || !Enabled)
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

        // After the key cooldown, never before it: an announcement that is not going to be said
        // must not consume the spacing that would silence the alarm of one that is.
        announcement = Marked(announcement, context.Now);

        _pending.Enqueue(announcement);

        logger.LogInformation("Callout {Key}: {Text}", announcement.Key, announcement.Text);

        return true;
    }

    /// <summary>
    /// The announcement with its alarm kept or dropped, by how recently that same alarm sounded
    /// (<a href="https://github.com/dseelinger/d47/issues/136">#136</a>).
    /// <para>
    /// Keyed on the cue rather than on the callout or the key, because the question being asked is
    /// about the Commander's ear: two sounds a Commander cannot tell apart, six seconds apart, are
    /// one sound played twice however many different things produced them.
    /// </para>
    /// </summary>
    private Announcement Marked(Announcement announcement, DateTimeOffset now)
    {
        if (announcement.Cue is not { } cue)
        {
            return announcement;
        }

        if (_markedAt.TryGetValue(cue, out var last) && now - last < CueSpacing)
        {
            // The line still goes. Only the marker in front of it is dropped.
            return announcement with { Cue = null };
        }

        _markedAt[cue] = now;

        return announcement;
    }

    /// <summary>
    /// Takes everything queued since the last call. Drained by the app on its own thread, which
    /// is where the awaiting happens.
    /// </summary>
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

/// <summary>
/// A callout the Commander switched off within seconds of hearing it
/// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// </summary>
/// <param name="Id">Which callout, by the id the settings rows use.</param>
/// <param name="When">When it was switched off.</param>
/// <param name="After">How long after it last spoke. What makes the two read as connected.</param>
public sealed record CalloutSilenced(string Id, DateTimeOffset When, TimeSpan After);
