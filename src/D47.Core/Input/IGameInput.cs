namespace D47.Core.Input;

/// <summary>What happened when d47 tried to press something.</summary>
public enum InjectionOutcome
{
    Sent,

    /// <summary>Elite does not have the foreground. Nothing was sent, on purpose.</summary>
    NotForeground,

    /// <summary>Elite is not running, or no window could be found.</summary>
    GameNotFound,

    /// <summary>The sequence was empty — an unpressable binding reached the injector.</summary>
    NothingToSend,

    Failed,
}

/// <param name="Outcome">What happened.</param>
/// <param name="Reason">A sentence to say back. Empty when the outcome is <see cref="InjectionOutcome.Sent"/>.</param>
public readonly record struct InjectionResult(InjectionOutcome Outcome, string Reason = "")
{
    public bool Sent => Outcome == InjectionOutcome.Sent;

    public static readonly InjectionResult Ok = new(InjectionOutcome.Sent);
}

/// <summary>
/// Sending input to the game. Declared here and implemented in the app, like every other
/// hardware contract, so Core stays free of the platform and the whole action path can be
/// driven in a test with nothing sent (architecture.md §8).
/// <para>
/// The three rules from architecture.md D4 are properties of the implementation rather than
/// of this interface, but they are what it exists to make enforceable in one place: scancodes
/// only, never a low-level keyboard hook, <see cref="ReleaseAll"/> unconditional, and Elite
/// verified as the foreground window before anything is sent.
/// </para>
/// </summary>
public interface IGameInput
{
    /// <summary>Whether an Elite window can be found at all.</summary>
    bool IsGameRunning { get; }

    /// <summary>Whether Elite has the foreground right now. False means nothing may be sent.</summary>
    bool IsGameForeground { get; }

    /// <summary>
    /// Sends a sequence. Returns without sending when Elite is not in front — a voice command
    /// must never type into a browser, and that check belongs on this side of the call rather
    /// than at each of the several call sites that will grow over the phase.
    /// </summary>
    Task<InjectionResult> SendAsync(IReadOnlyList<InputStep> steps, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases everything d47 is holding. Unconditional: called in a finally, on focus loss
    /// and on exit, and safe to call when nothing is held. A stranded key here is a throttle
    /// that will not stop.
    /// </summary>
    void ReleaseAll();
}

/// <summary>
/// An <see cref="IGameInput"/> that records instead of sending — the dry-run injector
/// architecture.md §8 names. Lives in Core rather than in a test project because the replay
/// harness needs it too, and because a second copy of it in the tests would be a second thing
/// to keep true.
/// </summary>
public sealed class RecordingGameInput : IGameInput
{
    private readonly List<InputStep> _steps = [];
    private readonly Lock _gate = new();

    public bool IsGameRunning { get; set; } = true;

    public bool IsGameForeground { get; set; } = true;

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

    public Task<InjectionResult> SendAsync(
        IReadOnlyList<InputStep> steps,
        CancellationToken cancellationToken = default)
    {
        if (!IsGameRunning)
        {
            return Task.FromResult(new InjectionResult(InjectionOutcome.GameNotFound, "Elite is not running."));
        }

        if (!IsGameForeground)
        {
            return Task.FromResult(new InjectionResult(
                InjectionOutcome.NotForeground, "Elite is not the window in front."));
        }

        if (steps.Count == 0)
        {
            return Task.FromResult(new InjectionResult(InjectionOutcome.NothingToSend, "There was nothing to press."));
        }

        lock (_gate)
        {
            // The delay is recorded rather than waited out. A test asserting a hold is asserting
            // that the wait was asked for, and waiting sixty milliseconds per tap would make the
            // macro tests slower than the thing they test.
            _steps.AddRange(steps);
        }

        return Task.FromResult(InjectionResult.Ok);
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
