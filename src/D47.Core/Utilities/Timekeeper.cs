namespace D47.Core.Utilities;

/// <summary>
/// A reminder that has gone off, and whether d47 was there for it.
/// </summary>
/// <param name="Reminder">What fired.</param>
/// <param name="Missed">
/// True when it was already due the first time d47 looked — an alarm that came round while the
/// app was closed. Reported afterwards rather than sounded late as though nothing had happened.
/// </param>
public sealed record Fired(Reminder Reminder, bool Missed);

/// <summary>
/// The Commander's timers and alarms (list.md Phase 24, "Timers and alarms").
/// <para>
/// <b>The first thing d47 does that nothing external triggers.</b> Every other behaviour is
/// reactive — a journal event arrives, a callout goes out — or the Commander started it in that
/// moment; a timer fires because time passed and nothing happened at all. So the state lives here
/// and the tick loop asks <see cref="Poll"/> <em>is anything due</em> with the injected now, which
/// keeps both the clock rule and the replay harness.
/// </para>
/// <para>
/// <b>Alarms survive a restart and timers do not.</b> An alarm for 07:00 is a promise about a
/// wall-clock moment that outlives the process; a forty-minute timer through a crash is a question
/// nobody can answer — half elapsed, or start again? So timers are held here and nowhere else,
/// and alarms are held in <see cref="AlarmStore"/>.
/// </para>
/// <para>
/// <b>Cancelling is the Commander's.</b> Reachable from the panel and from the keyword router and
/// from nothing else — <c>Protected</c> is a property of the caller rather than of the modality,
/// so a spoken cancel works and a hostile in-game message cannot reach one. An alarm somebody
/// relies on to leave the house is worth that line being drawn before anybody asks for it.
/// </para>
/// </summary>
public sealed class Timekeeper(AlarmStore alarms)
{
    private readonly Lock _gate = new();

    /// <summary>Countdowns. Here and nowhere else, which is what "timers do not persist" is.</summary>
    private readonly List<Reminder> _timers = [];

    /// <summary>What has already gone off, so a tick does not fire the same reminder twice.</summary>
    private readonly HashSet<string> _fired = new(StringComparer.Ordinal);

    /// <summary>
    /// The first instant this was polled at, which is what tells a missed alarm from a due one.
    /// Null until the first poll, because "when did d47 start" is a question about now and Core
    /// does not read the clock to answer it — the first tick supplies it.
    /// </summary>
    private DateTimeOffset? _from;

    /// <summary>Everything running, timers first, each set in the order it was made.</summary>
    public IReadOnlyList<Reminder> Running
    {
        get
        {
            lock (_gate)
            {
                return [.. _timers, .. alarms.Alarms.Where(alarm => !_fired.Contains(alarm.Id))];
            }
        }
    }

    /// <summary>
    /// Starts a countdown. Refused for a duration that is not one, because a timer for zero
    /// minutes is a chime rather than a timer and one for a negative is nothing at all.
    /// </summary>
    public Reminder? StartTimer(string name, TimeSpan duration, DateTimeOffset now)
    {
        if (duration <= TimeSpan.Zero || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var timer = new Reminder(
            Guid.NewGuid().ToString("N"), name.Trim(), now + duration, ReminderKind.Timer);

        lock (_gate)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>
    /// Sets an alarm for a moment, and writes it. Refused for a moment already past: an alarm
    /// that is due the instant it is made would go off before the Commander finished saying so.
    /// </summary>
    public Reminder? SetAlarm(string name, DateTimeOffset due, DateTimeOffset now)
    {
        if (due <= now || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var alarm = new Reminder(
            Guid.NewGuid().ToString("N"), name.Trim(), due, ReminderKind.Alarm);

        alarms.Save([.. alarms.Alarms, alarm]);

        return alarm;
    }

    /// <summary>
    /// Cancels one by id, and says whether there was one. <b>Protected — the Commander's own act.</b>
    /// </summary>
    public bool Cancel(string id)
    {
        lock (_gate)
        {
            if (_timers.RemoveAll(timer => timer.Id == id) > 0)
            {
                return true;
            }
        }

        var remaining = alarms.Alarms.Where(alarm => alarm.Id != id).ToList();

        if (remaining.Count == alarms.Alarms.Count)
        {
            return false;
        }

        alarms.Save(remaining);
        return true;
    }

    /// <summary>
    /// Cancels the one a phrase names, and says what happened.
    /// <para>
    /// Null on nought and on several rather than guessing, which is the rule the checklist's own
    /// matcher already follows: cancelling the wrong alarm of two is worse than asking which.
    /// </para>
    /// </summary>
    public Reminder? Find(string phrase)
    {
        var wanted = phrase.Trim();

        if (wanted.Length == 0)
        {
            return null;
        }

        var matches = Running
            .Where(reminder => string.Equals(reminder.Name, wanted, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>
    /// What has come due since the last look. Called by the tick loop with the injected now.
    /// <para>
    /// An alarm already due at the <em>first</em> poll is a missed one: d47 cannot fire while it
    /// is closed, so it is reported with when it was due rather than sounded hours late.
    /// </para>
    /// <para>
    /// Each reminder fires once. A timer is dropped when it does; an alarm is remembered as fired
    /// and then removed from the file, so a Commander who edits the file back gets it again — the
    /// file is the record and this is not a second copy of it.
    /// </para>
    /// </summary>
    public IReadOnlyList<Fired> Poll(DateTimeOffset now)
    {
        var first = false;

        lock (_gate)
        {
            if (_from is null)
            {
                _from = now;
                first = true;
            }
        }

        var went = new List<Fired>();

        lock (_gate)
        {
            for (var index = _timers.Count - 1; index >= 0; index--)
            {
                if (!_timers[index].DueBy(now))
                {
                    continue;
                }

                went.Add(new Fired(_timers[index], Missed: false));
                _timers.RemoveAt(index);
            }
        }

        var due = alarms.Alarms.Where(alarm => alarm.DueBy(now)).ToList();

        if (due.Count > 0)
        {
            foreach (var alarm in due)
            {
                lock (_gate)
                {
                    if (!_fired.Add(alarm.Id))
                    {
                        continue;
                    }
                }

                went.Add(new Fired(alarm, Missed: first));
            }

            // Taken out of the file once they have gone off, so a restart does not re-report them
            // for ever. Written after the report is built, so a failed write costs a repeat rather
            // than a silence.
            alarms.Save([.. alarms.Alarms.Where(alarm => !alarm.DueBy(now))]);
        }

        // Timers first, then alarms, and each in the order it was set — which is the order the
        // Commander made them in and therefore the order they will expect to hear them.
        return went;
    }
}
