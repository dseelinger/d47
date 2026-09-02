using System.Collections.Concurrent;
using D47.Core.Journal;
using D47.Core.Ticking;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>
/// Everything a callout is allowed to look at on one tick.
/// <para>
/// <b>The world here is fixed for the tick and the last field is not.</b> Now, Status, Route and
/// Events are what d47 saw, and no two callouts may disagree about that. <see cref="LastChatterAt"/>
/// is a record of d47's own conduct rather than of the world, and it advances within a tick as
/// soon as something unprompted is queued — otherwise the second such callout of a tick could not
/// see what the first one just did.
/// </para>
/// </summary>
/// <param name="Now">The tick's time, injected like everywhere else in Core.</param>
/// <param name="IsPriming">
/// True on the startup tick, which replays the whole journal backlog. A callout must fold that
/// backlog into its state and announce nothing from it — otherwise starting d47 after Elite
/// means every hull hit of the last two hours is read out at once. This is exactly what
/// "the tracker is primed from the session backlog at startup" asks for.
/// </param>
/// <param name="LastChatter">
/// The last thing d47 said because <em>nothing</em> had happened, or null if it has said nothing
/// this session (<a href="https://github.com/dseelinger/d47/issues/257">#257</a>).
/// <para>
/// Supplied by <see cref="CalloutEngine.Tick"/> and read by the two chatter callouts alone, so
/// each can hold its turn rather than spend it: a callout that yields and is then refused has
/// already moved its own cycle on, and would pay a whole interval of silence for a collision that
/// lasted one tick. <c>AutonomousActionRunner</c> takes this same struct and neither sets nor
/// reads it.
/// </para>
/// <para>
/// Last and defaulted, so every context built without it — and <c>default</c> — is permissive and
/// holds nothing back. There are a dozen such construction sites across the tests and the corpus
/// replay harness, and none of them is about this.
/// </para>
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
/// Something d47 said because nothing had happened — when, and the rate the row it came from asks
/// for (<a href="https://github.com/dseelinger/d47/issues/257">#257</a>).
/// <para>
/// <b>The rate travels with the timestamp because the floor is two-sided.</b> A voice set to speak
/// every twenty seconds cannot claim ninety seconds of air behind it — it is going to break that
/// silence itself long before it is up, and a floor that let it would silence the other voice
/// altogether rather than spacing the two out. So the last speaker's rate bounds the wait exactly
/// as the waiter's own does, and either alone is a floor that stops composing the moment the two
/// rows differ.
/// </para>
/// </summary>
/// <param name="At">When it was queued.</param>
/// <param name="Asked">
/// The least time between two of <em>its</em> kind, as the Commander set it — carried on
/// <see cref="Announcement.Chatter"/>.
/// </param>
public readonly record struct ChatterSaid(DateTimeOffset At, TimeSpan Asked);

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

    /// <summary>
    /// The least air between two things d47 said because nothing happened
    /// (<a href="https://github.com/dseelinger/d47/issues/257">#257</a>).
    /// <para>
    /// <b>The keys cannot answer this either</b>, for the reason <see cref="CueSpacing"/> gives
    /// one paragraph up. <c>ambient.supercruise</c> and <c>npc.chatter.passersby</c> are two
    /// different things to say, correctly, both worth saying; what is not worth doing is saying
    /// them back to back, which reads as one companion filling silence with itself however many
    /// separate timers arrived at it.
    /// </para>
    /// <para>
    /// <b>Ninety seconds, and it is arithmetic rather than taste.</b> The longest of these
    /// utterances is an exchange of <see cref="NpcChatter.MostLines"/> lines: eleven to seventeen
    /// seconds of speech, plus the beats between them, plus a synthesis a line — call it thirty.
    /// And <see cref="SilencedSoonAfter"/> already fixes thirty seconds as this app's span for two
    /// things reading as connected. Thirty of air behind thirty of scene, rounded up because the
    /// engine can measure neither.
    /// </para>
    /// <para>
    /// <b>Not a settings row, and the argument is already on record one level down.</b>
    /// <see cref="NpcChatter.Beat"/> says of itself that a row asking about the gap inside an
    /// exchange would be "a knob for something a Commander wants right rather than adjustable".
    /// The gap between two of them is the same argument. What a Commander does set is the rate,
    /// and <see cref="ChatterSpacingFor"/> clamps this down to it.
    /// </para>
    /// <para>
    /// <b>Measured from where an announcement was queued, not from where it was heard.</b> Core
    /// holds no clock and cannot know how long the last line took to say, so ninety of floor buys
    /// about eighty-five of real air after a one-sentence remark and about seventy after a
    /// four-line scene. That is the honest limit of a guarantee the engine can make alone, and it
    /// is why the number is materially larger than the adjacency it fixes rather than merely
    /// larger than zero. It is also why a remark the model declined to write — dropped by the app
    /// after this point, never heard — can still hold the other voice for one floor. Bounded at
    /// that, and never a lost line, because the held one is deferred rather than dropped.
    /// </para>
    /// </summary>
    public static readonly TimeSpan ChatterSpacing = TimeSpan.FromSeconds(90);

    /// <summary>
    /// The floor in force between a line whose row asks for <paramref name="spoken"/> and one
    /// whose row asks for <paramref name="asked"/> — the least of the three
    /// (<a href="https://github.com/dseelinger/d47/issues/257">#257</a>).
    /// <para>
    /// <b>Both rows bound it, and each one has to.</b> The waiter's own rate bounds it because a
    /// Commander asking for a line every twenty seconds should get a twenty-second floor rather
    /// than a ninety-second one. The <em>speaker's</em> rate bounds it because otherwise a fast
    /// voice starves a slow one outright: a kind set to speak every sixty seconds would restamp
    /// the floor faster than a ninety-second wait could ever expire, and the other kind would be
    /// refused forever rather than spaced out. Either bound alone stops composing the moment the
    /// two rows differ.
    /// </para>
    /// <para>
    /// One place, read by <see cref="ChatterOwesQuiet"/> and therefore by <see cref="Offer"/> and
    /// the callouts both, so the rule cannot be implemented twice and differently.
    /// </para>
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
    /// Whether an unprompted line whose row asks for <paramref name="asked"/> still owes the
    /// Commander quiet, given what was last said unprompted
    /// (<a href="https://github.com/dseelinger/d47/issues/257">#257</a>).
    /// <para>
    /// The whole rule, in one place and in one form. The callouts ask it before anything of theirs
    /// moves, so a held line keeps its turn; <see cref="Offer"/> asks it again as the line is
    /// queued, which is what makes the answer the engine's rather than the callouts' manners.
    /// </para>
    /// </summary>
    public static bool ChatterOwesQuiet(ChatterSaid? last, DateTimeOffset now, TimeSpan asked) =>
        last is { } said && now - said.At < ChatterSpacingFor(asked, said.Asked);

    /// <summary>
    /// What was last said unprompted, or null if nothing has been this session
    /// (<a href="https://github.com/dseelinger/d47/issues/257">#257</a>).
    /// <para>
    /// A plain field rather than a concurrent one, for the reason <see cref="_spokenAt"/> is a
    /// plain <see cref="Dictionary{TKey,TValue}"/>: it is written and read on the tick thread and
    /// nowhere else. <see cref="_lastSpokeBy"/> is the one that is not, and its own note says why.
    /// </para>
    /// </summary>
    private ChatterSaid? _lastChatter;

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
    /// The tick-loop entry point. Runs every enabled callout against the same world, so two
    /// callouts cannot disagree about what it looked like this tick.
    /// <para>
    /// The one field that does advance inside a tick is
    /// <see cref="CalloutContext.LastChatterAt"/>, which is not the world: it is whether d47 has
    /// just spoken unprompted, and the second such callout of a tick has to be able to see what
    /// the first one did (<a href="https://github.com/dseelinger/d47/issues/257">#257</a>).
    /// </para>
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
                // Derived fresh per callout off the engine's own field, never off the struct it
                // was handed: two unprompted callouts examined on one tick must not both read
                // "nothing has been said" (#257).
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

        // The floor between two unprompted lines (#257), asked here rather than trusted to the
        // callouts that already asked it. Examine returns a sequence, so one callout may offer
        // two on a tick and no check it made before yielding can see the second — and this is
        // what makes "two of these are never queued closer than the floor" a property of the
        // engine rather than of the order its callouts happen to be registered in.
        //
        // Above the key cooldown, and above Marked, for the reason stated below both: an
        // announcement that is not going to be said must spend nothing. What this one would
        // spend is its own three-hundred-second cooldown, which would turn a ninety-second
        // deferral into a lost cycle.
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

        // Stamped on the way through rather than in Tick, because Offer can still refuse for
        // priming, for Enabled or for the key cooldown — stamping where the callout spoke would
        // hold the other voice for something that was never queued.
        if (announcement.Chatter is { } rate)
        {
            _lastChatter = new ChatterSaid(context.Now, rate);
        }

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
    /// Whether anything queued behind is more than routine
    /// (<a href="https://github.com/dseelinger/d47/issues/259">#259</a>).
    /// <para>
    /// Asked by the speaking loop before it takes one of the pauses between the lines of an
    /// invented exchange. The speaking lock is held for a whole batch, so every second of air
    /// added inside one is a second longer that the <em>next</em> batch waits — and the next
    /// batch is where a danger or fuel callout would be. <b>The Commander hearing about the heat
    /// two seconds late because a courier was chatting is a worse defect than the one the pauses
    /// fix</b>, so the pause yields to it rather than the other way round.
    /// </para>
    /// <para>
    /// A read of the queue rather than a drain: nothing is consumed, and the answer is asked
    /// again on every slice of a pause, so an alert arriving mid-gap cuts the gap short.
    /// </para>
    /// </summary>
    public bool AnythingUrgentWaiting => _pending.Any(pending => pending.Urgency == CalloutUrgency.Urgent);

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
