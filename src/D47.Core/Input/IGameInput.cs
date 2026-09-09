namespace D47.Core.Input;

/// <summary>What happened when d47 tried to press something.</summary>
public enum InjectionOutcome
{
    Sent,

    /// <summary>Elite does not have the foreground.</summary>
    NotForeground,

    /// <summary>Elite is not running, or no window could be found.</summary>
    GameNotFound,

    /// <summary>
    /// Elite is running but the Commander is not in the game — the main menu, or a status file too
    /// stale to trust (#242).
    /// </summary>
    NotOnline,

    /// <summary>The sequence was empty — an unpressable binding reached the injector.</summary>
    NothingToSend,

    Failed,
}

/// <summary><param name="Outcome">What happened.</param> <param name="Reason">A sentence to say back.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Reason">A sentence to say back.</param>
public readonly record struct InjectionResult(InjectionOutcome Outcome, string Reason = "")
{
    public bool Sent => Outcome == InjectionOutcome.Sent;

    public static readonly InjectionResult Ok = new(InjectionOutcome.Sent);
}

/// <summary>Sending input to the game.</summary>
public interface IGameInput
{
    /// <summary>Whether an Elite window can be found at all.</summary>
    bool IsGameRunning { get; }

    /// <summary>Whether Elite has the foreground right now.</summary>
    bool IsGameForeground { get; }

    /// <summary>Sends a sequence.</summary>
    Task<InjectionResult> SendAsync(IReadOnlyList<InputStep> steps, CancellationToken cancellationToken = default);

    /// <summary>The same send, with a trace watching each step (#365).</summary>
    Task<InjectionResult> SendAsync(
        IReadOnlyList<InputStep> steps,
        IInputTrace? trace,
        CancellationToken cancellationToken = default) => SendAsync(steps, cancellationToken);

    /// <summary>
    /// Opens a trace for one named caller, or null when nothing is watching (#365) — which is every run
    /// that did not ask for one on the command line.
    /// </summary>
    IInputTrace? Trace(string caller) => null;

    /// <summary>Releases everything d47 is holding.</summary>
    void ReleaseAll();
}

/// <summary>
/// An <see cref="IGameInput"/> that records instead of sending: the dry-run injector.
/// </summary>
public sealed class RecordingGameInput : IGameInput
{
    private readonly List<InputStep> _steps = [];
    private readonly Lock _gate = new();

    private DateTimeOffset _stamp = DateTimeOffset.UnixEpoch;

    public bool IsGameRunning { get; set; } = true;

    public bool IsGameForeground { get; set; } = true;

    /// <summary>The trace watching this input, if anything is (#365).</summary>
    public IInputStepObserver? Observer { get; set; }

    /// <summary>What a traced step is stamped with.</summary>
    public Func<DateTimeOffset>? Now { get; set; }

    /// <summary>Everything sent, in order, across every call.</summary>
    public IReadOnlyList<InputStep> Steps
    {
        get
        {
            lock (_gate)
            {
                return [.. _steps];
            }
        }
    }

    public int ReleaseAllCalls { get; private set; }

    public IInputTrace? Trace(string caller) => Observer?.Open(caller);

    public Task<InjectionResult> SendAsync(
        IReadOnlyList<InputStep> steps,
        CancellationToken cancellationToken = default) =>
        SendAsync(steps, null, cancellationToken);

    /// <summary>
    /// The traced send, so the replay harness and the Core tests exercise the same reporting the real
    /// injector does (#365) — including the anonymous trace a caller that declares nothing still gets.
    /// </summary>
    public Task<InjectionResult> SendAsync(
        IReadOnlyList<InputStep> steps,
        IInputTrace? trace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var own = trace is null ? Observer?.Open(IInputStepObserver.Anonymous) : null;
        var watching = trace ?? own;

        var result = Record(steps);

        if (watching is not null && result.Sent)
        {
            for (var index = 0; index < steps.Count; index++)
            {
                watching.Stepped(new InputStepReport(index, steps[index], Stamp(), IsGameForeground));
            }
        }

        if (own is not null)
        {
            own.Verdict(result.Outcome.ToString(), result.Reason);
            own.Dispose();
        }

        return Task.FromResult(result);
    }

    private DateTimeOffset Stamp()
    {
        if (Now is { } clock)
        {
            return clock();
        }

        _stamp += TimeSpan.FromMilliseconds(1);
        return _stamp;
    }

    private InjectionResult Record(IReadOnlyList<InputStep> steps)
    {
        if (!IsGameRunning)
        {
            return new InjectionResult(InjectionOutcome.GameNotFound, "Elite is not running.");
        }

        if (!IsGameForeground)
        {
            return new InjectionResult(InjectionOutcome.NotForeground, "Elite is not the window in front.");
        }

        if (steps.Count == 0)
        {
            return new InjectionResult(InjectionOutcome.NothingToSend, "There was nothing to press.");
        }

        lock (_gate)
        {
            // The delay is recorded rather than waited out.
            _steps.AddRange(steps);
        }

        return InjectionResult.Ok;
    }

    public void ReleaseAll() => ReleaseAllCalls++;

    public void Clear()
    {
        lock (_gate)
        {
            _steps.Clear();
        }
    }
}
