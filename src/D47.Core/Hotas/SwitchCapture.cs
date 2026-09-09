namespace D47.Core.Hotas;

/// <summary>Where a walk has got to, which is the whole of what the capture surface shows.</summary>
public enum CaptureStage
{
    /// <summary>Waiting for the first movement.</summary>
    Waiting,

    /// <summary>Something moved.</summary>
    Moving,

    /// <summary>A position has sat still long enough to be sampled and recorded.</summary>
    Resting,

    /// <summary>The walk finished and produced a switch.</summary>
    Captured,

    /// <summary>The walk was declined, with a reason.</summary>
    Declined,
}

/// <summary>One thing the walk saw, with when it saw it.</summary>
/// <param name="At">Elapsed since the walk began.</param>
public readonly record struct CaptureEvent(TimeSpan At, string What);

/// <summary>What a walk produced: a switch, or a reason it is not one.</summary>
public sealed record CaptureResult
{
    public required CaptureStage Stage { get; init; }

    /// <summary>The positions found, in the order they were walked.</summary>
    public IReadOnlyList<SwitchPosition> Positions { get; init; } = [];

    /// <summary>The <c>NonRoamableId</c> the walk happened on.</summary>
    public string DeviceId { get; init; } = string.Empty;

    public string Device { get; init; } = string.Empty;

    /// <summary>What to say.</summary>
    public required string Says { get; init; }

    public bool IsCaptured => Stage == CaptureStage.Captured;

    public bool IsDeclined => Stage == CaptureStage.Declined;

    public bool IsOver => Stage is CaptureStage.Captured or CaptureStage.Declined;
}

/// <summary>The walk (Phase 21, "Assign a switch by walking its positions").</summary>
public sealed class SwitchCapture
{
    /// <summary>
    /// How long a position has to sit still before it is sampled. 1.5 s is the spike's figure and it
    /// does two jobs: long enough that a spring-return control has already gone home, short enough that
    /// walking a four-position switch is not a chore.
    /// </summary>
    public static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(1500);

    private readonly List<CaptureEvent> _report = [];
    private readonly List<SwitchPosition> _positions = [];

    /// <summary>Last reading per device, so the walk can see what moved without owning a thread.</summary>
    private readonly Dictionary<string, (IReadOnlyList<bool> Buttons, IReadOnlyList<int> Hats)> _previous = [];

    /// <summary>Last poll's buttons, kept for the one poll in which the device is chosen.</summary>
    private readonly Dictionary<string, IReadOnlyList<bool>> _previousBeforeThisPoll = [];

    /// <summary>Buttons that have moved at all since the walk began.</summary>
    private readonly HashSet<int> _candidates = [];

    /// <summary>Candidates that have been held at a settled stop.</summary>
    private readonly HashSet<int> _sampled = [];

    private DateTimeOffset? _began;
    private DateTimeOffset _movedAt;
    private string? _deviceId;
    private string _device = string.Empty;
    private bool _restSampled;

    /// <summary>Which candidates were held at the last settled stop, or null before there has been one.</summary>
    private IReadOnlyList<int>? _lastRest;

    /// <summary>
    /// Everything the chosen device was holding at the moment it was chosen — sixteen buttons on the
    /// bench, of which at most one belongs to this switch.
    /// </summary>
    private IReadOnlyList<bool> _baseline = [];

    /// <summary>Whether anything has moved since that stop.</summary>
    private bool _movedSinceRest;

    private CaptureResult? _final;

    /// <summary>Everything the walk saw, in order.</summary>
    public IReadOnlyList<CaptureEvent> Report => _report;

    public CaptureStage Stage { get; private set; } = CaptureStage.Waiting;

    /// <summary>Positions found so far, so the surface can show the walk filling in as it happens.</summary>
    public IReadOnlyList<SwitchPosition> Positions => _positions;

    public string DeviceId => _deviceId ?? string.Empty;

    /// <summary>One reading of every device.</summary>
    public CaptureResult Poll(DateTimeOffset now, IReadOnlyList<HotasReading> readings)
    {
        if (_final is { } over)
        {
            return over;
        }

        _began ??= now;
        _movedAt = _movedAt == default ? now : _movedAt;

        if (readings.Count == 0)
        {
            return Say(CaptureStage.Waiting, "D47 cannot see any controllers. Plug the stick in and try again.");
        }

        // Every device is diffed, every poll, whether or not it is the chosen one.
        var moved = Diff(readings);

        _previousBeforeThisPoll.Clear();

        foreach (var (id, before) in _previous)
        {
            _previousBeforeThisPoll[id] = before.Buttons;
        }

        foreach (var reading in readings)
        {
            _previous[reading.Id] = ([.. reading.Buttons], [.. reading.Hats]);
        }

        if (_deviceId is null)
        {
            if (moved.Count == 0)
            {
                return Say(CaptureStage.Waiting, Progress());
            }

            var first = moved[0];
            _deviceId = first.Reading.Id;
            _device = first.Reading.Describe();

            // The state *before* this poll's change, which is where the switch was sitting when the walk
            // began.
            _baseline = _previousBeforeThisPoll.TryGetValue(_deviceId, out var before) ? before : [];

            Note(now, $"device {_deviceId} moved first ({_device})");
        }

        var mine = moved.FirstOrDefault(change => change.Reading.Id == _deviceId);

        if (mine.Reading is null)
        {
            // Nothing moved on the chosen device this poll.
            if (readings.FirstOrDefault(reading => reading.Id == _deviceId) is not { } resting)
            {
                return Decline(now, "That device disappeared part-way through the walk.");
            }

            return Rest(now, resting);
        }

        // A hat is a third input class and is declined rather than read as a two-position toggle.
        if (mine.Hats)
        {
            return Decline(
                now,
                "That is a hat, not a switch. A hat springs back to centre, so it can only mean a press — "
                + "never a state.");
        }

        _movedAt = now;
        _restSampled = false;
        _movedSinceRest = true;

        foreach (var (button, down) in mine.Buttons)
        {
            _candidates.Add(button);
            Note(now, $"button {button} {(down ? "down" : "up")}");
        }

        // The spring-return decline, taken at the release rather than at the end.
        if (mine.Buttons.Any(button => !button.Down) && WentHome(mine.Reading))
        {
            return Decline(
                now,
                "That control went back where it started without stopping anywhere new. A spring-return "
                + "switch or a push button cannot be left anywhere, so it cannot mean a state — only a "
                + "press. If it is a maintained switch, walk it again and pause at each position.");
        }

        return Say(CaptureStage.Moving, "Hold it there.");
    }

    /// <summary>The Commander says the walk is done.</summary>
    public CaptureResult Finish(DateTimeOffset now)
    {
        if (_final is { } over)
        {
            return over;
        }

        if (_positions.Count == 0)
        {
            return Decline(
                now,
                "D47 did not see the switch settle anywhere. Move it to each position in turn, and pause "
                + "at each one for a second or two.");
        }

        if (_positions.Count < 2)
        {
            return Decline(
                now,
                "D47 only saw one position. Walk the switch through every position it has, pausing at each.");
        }

        // A candidate that moved but was never held at a stop is a position that was walked past without
        // pausing.
        var missed = _candidates.Except(_sampled).OrderBy(button => button).ToList();

        if (missed.Count > 0)
        {
            var plural = missed.Count == 1 ? "Button" : "Buttons";

            return Decline(
                now,
                $"{plural} {string.Join(", ", missed)} moved during the walk but was never held still long "
                + "enough to be a position. Walk it again, pausing at each position.");
        }

        Note(now, $"captured {_positions.Count} positions");

        return Settled(new CaptureResult
        {
            Stage = CaptureStage.Captured,
            Positions = [.. _positions],
            DeviceId = _deviceId ?? string.Empty,
            Device = _device,
            Says = $"Found {_positions.Count} positions: {Listed()}.",
        });
    }

    /// <summary>The walk as text.</summary>
    public string Export()
    {
        var lines = new List<string>
        {
            "# D47 switch capture",
            string.Empty,
            $"Device id  : {(_deviceId is { Length: > 0 } id ? id : "(none)")}",
            $"Device     : {(_device.Length == 0 ? "(unknown)" : _device)}",
            $"Outcome    : {Stage}",
            $"Says       : {_final?.Says ?? "(the walk did not finish)"}",
            string.Empty,
            $"Positions  : {(_positions.Count == 0 ? "(none)" : Listed())}",
            $"Moved      : {Numbers(_candidates)}",
            $"Sampled    : {Numbers(_sampled)}",
            string.Empty,
            "## Timeline",
            string.Empty,
        };

        lines.AddRange(_report.Select(entry => $"{entry.At.TotalMilliseconds,9:F0} ms  {entry.What}"));

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    /// <summary>The chosen device is sitting still.</summary>
    private CaptureResult Rest(DateTimeOffset now, HotasReading reading)
    {
        if (_candidates.Count == 0 || now - _movedAt < Settle)
        {
            return Say(Stage, Holding());
        }

        if (_restSampled)
        {
            return Say(CaptureStage.Resting, Progress());
        }

        _restSampled = true;

        var held = _candidates.Where(reading.IsHeld).OrderBy(button => button).ToList();

        if (held.Count > 1)
        {
            return Decline(
                now,
                $"Buttons {string.Join(" and ", held)} are held together at one position, so D47 cannot tell "
                + "which one that position is. That is not a switch it can read.");
        }

        _lastRest = held;
        _movedSinceRest = false;

        // The position the walk started in, resolved now that the candidate set is known.
        if (_positions.Count == 0)
        {
            var home = Home();
            var start = new SwitchPosition(home.Count == 1 ? home[0] : null);

            _positions.Add(start);

            if (home.Count == 1)
            {
                _sampled.Add(home[0]);
            }

            Note(now, $"started at {start.Describe()}");
        }

        var position = new SwitchPosition(held.Count == 1 ? held[0] : null);

        if (_positions.Any(existing => existing.Button == position.Button))
        {
            // Walked back through somewhere it has already been.
            Note(now, $"rest at {position.Describe()} (already known)");
            return Say(CaptureStage.Resting, Progress());
        }

        if (held.Count == 1)
        {
            _sampled.Add(held[0]);
        }

        _positions.Add(position);
        Note(now, $"rest at {position.Describe()}");

        return Say(CaptureStage.Resting, Progress());
    }

    /// <summary>Whether the device is back where the last stop was.</summary>
    private bool WentHome(HotasReading reading)
    {
        if (!_movedSinceRest)
        {
            return false;
        }

        var held = _candidates.Where(reading.IsHeld).OrderBy(button => button).ToList();

        return held.SequenceEqual(_lastRest ?? Home());
    }

    /// <summary>
    /// Where the switch was when the walk began, as far as is currently known: the candidates that were
    /// already held at the baseline.
    /// </summary>
    private List<int> Home() =>
        [.. _candidates
            .Where(button => button < _baseline.Count && _baseline[button])
            .OrderBy(button => button)];

    /// <summary>Every device that moved this poll, in device order, with what moved.</summary>
    private List<(HotasReading Reading, List<(int Button, bool Down)> Buttons, bool Hats)> Diff(
        IReadOnlyList<HotasReading> readings)
    {
        var moved = new List<(HotasReading, List<(int, bool)>, bool)>();

        foreach (var reading in readings)
        {
            // First sight of a device is a baseline, never a change.
            if (!_previous.TryGetValue(reading.Id, out var before))
            {
                continue;
            }

            var buttons = new List<(int, bool)>();
            var count = Math.Min(before.Buttons.Count, reading.Buttons.Count);

            for (var button = 0; button < count; button++)
            {
                if (before.Buttons[button] != reading.Buttons[button])
                {
                    buttons.Add((button, reading.Buttons[button]));
                }
            }

            var hats = false;
            var hatCount = Math.Min(before.Hats.Count, reading.Hats.Count);

            for (var hat = 0; hat < hatCount; hat++)
            {
                hats |= before.Hats[hat] != reading.Hats[hat];
            }

            if (buttons.Count > 0 || hats)
            {
                moved.Add((reading, buttons, hats));
            }
        }

        return moved;
    }

    private string Holding() => Stage == CaptureStage.Waiting ? Progress() : "Hold it there.";

    private string Progress() =>
        _positions.Count switch
        {
            0 => "Move the switch to each position in turn, and pause at each one.",
            1 => $"One position so far ({_positions[0].Describe()}). Move it to the next one.",
            _ => $"{_positions.Count} positions so far: {Listed()}. Move it to the next one, or finish.",
        };

    private string Listed() => string.Join(", ", _positions.Select(position => position.Describe()));

    private static string Numbers(IEnumerable<int> buttons)
    {
        var ordered = buttons.OrderBy(button => button).ToList();

        return ordered.Count == 0 ? "(none)" : string.Join(", ", ordered);
    }

    private CaptureResult Say(CaptureStage stage, string says)
    {
        Stage = stage;

        return new CaptureResult
        {
            Stage = stage,
            Positions = [.. _positions],
            DeviceId = _deviceId ?? string.Empty,
            Device = _device,
            Says = says,
        };
    }

    private CaptureResult Decline(DateTimeOffset now, string why)
    {
        Note(now, $"declined: {why}");

        return Settled(new CaptureResult
        {
            Stage = CaptureStage.Declined,
            Positions = [.. _positions],
            DeviceId = _deviceId ?? string.Empty,
            Device = _device,
            Says = why,
        });
    }

    private CaptureResult Settled(CaptureResult result)
    {
        Stage = result.Stage;
        return _final = result;
    }

    private void Note(DateTimeOffset now, string what) =>
        _report.Add(new CaptureEvent(_began is { } began ? now - began : TimeSpan.Zero, what));
}
