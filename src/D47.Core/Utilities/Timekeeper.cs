namespace D47.Core.Utilities;

/// <summary>A reminder that has gone off, and whether d47 was there for it.</summary>
/// <param name="Reminder">What fired.</param>
/// <param name="Missed">
/// True when it was already due the first time d47 looked — an alarm that came round while the app was
/// closed.
/// </param>
public sealed record Fired(Reminder Reminder, bool Missed);

/// <summary>The Commander's timers and alarms (Phase 24, "Timers and alarms").</summary>
public sealed class Timekeeper(AlarmStore alarms)
{
    private readonly Lock _gate = new();

    /// <summary>Countdowns.</summary>
    private readonly List<Reminder> _timers = [];

    /// <summary>What has already gone off, so a tick does not fire the same reminder twice.</summary>
    private readonly HashSet<string> _fired = new(StringComparer.Ordinal);

    /// <summary>The first instant this was polled at, which is what tells a missed alarm from a due one.</summary>
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

    /// <summary>Starts a countdown.</summary>
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

    /// <summary>Sets an alarm for a moment, and writes it.</summary>
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

    /// <summary>Cancels one by id, and says whether there was one.</summary>
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

    /// <summary>Cancels the one a phrase names, and says what happened.</summary>
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

    /// <summary>What has come due since the last look.</summary>
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

            // Taken out of the file once they have gone off, so a restart does not re-report them for ever.
            alarms.Save([.. alarms.Alarms.Where(alarm => !alarm.DueBy(now))]);
        }

        // Timers first, then alarms, and each in the order it was set — which is the order the Commander made
        // them in and therefore the order they will expect to hear them.
        return went;
    }
}
