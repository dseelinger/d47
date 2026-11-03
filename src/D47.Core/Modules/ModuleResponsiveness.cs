namespace D47.Core.Modules;

/// <summary>Whether a running module is answering. The caller supplies every time; thread-safe.</summary>
public sealed class ModuleResponsiveness
{
    public static readonly TimeSpan PingEvery = TimeSpan.FromSeconds(15);

    public static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(5);

    /// <summary>A call outstanding for longer than this makes the module not responding.</summary>
    public static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);

    private readonly Lock _gate = new();
    private readonly Dictionary<long, DateTimeOffset> _calls = [];
    private long _nextCall;
    private DateTimeOffset _lastAnswer;
    private DateTimeOffset _lastPing;
    private DateTimeOffset? _unansweredPingSince;

    /// <param name="answeredAt">When the module finished starting, which counts as its first answer.</param>
    public ModuleResponsiveness(DateTimeOffset answeredAt)
    {
        _lastAnswer = answeredAt;
        _lastPing = answeredAt;
    }

    public DateTimeOffset LastAnswer
    {
        get
        {
            lock (_gate)
            {
                return _lastAnswer;
            }
        }
    }

    public bool PingDue(DateTimeOffset now)
    {
        lock (_gate)
        {
            return now - _lastPing >= PingEvery;
        }
    }

    public void PingSent(DateTimeOffset at)
    {
        lock (_gate)
        {
            _lastPing = at;
            _unansweredPingSince ??= at;
        }
    }

    /// <summary>A ping or a call got a reply.</summary>
    public void Answered(DateTimeOffset at)
    {
        lock (_gate)
        {
            _unansweredPingSince = null;

            if (at > _lastAnswer)
            {
                _lastAnswer = at;
            }
        }
    }

    /// <summary>Starts timing a call; pass the result to <see cref="CallEnded"/>.</summary>
    public long CallStarted(DateTimeOffset at)
    {
        lock (_gate)
        {
            var call = _nextCall++;
            _calls[call] = at;
            return call;
        }
    }

    /// <summary>Stops timing a call, whether or not it got a reply.</summary>
    public void CallEnded(long call)
    {
        lock (_gate)
        {
            _calls.Remove(call);
        }
    }

    /// <summary>Not responding while a ping has gone unanswered past its timeout or a call has run past its own.</summary>
    public ModuleStatus Status(DateTimeOffset now)
    {
        lock (_gate)
        {
            var missedPing = _unansweredPingSince is { } since && now - since > PingTimeout;
            var slowCall = _calls.Values.Any(started => now - started > CallTimeout);

            return missedPing || slowCall ? ModuleStatus.NotResponding : ModuleStatus.Running;
        }
    }
}
