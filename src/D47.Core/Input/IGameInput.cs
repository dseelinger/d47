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

    /// <summary>d47 is showing a Commander other than the one Elite is running.</summary>
    OffDuty,

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

/// <summary>An <see cref="IGameInput"/> with no game behind it: nothing is sent and every send reports that Elite is not running.</summary>
public sealed class NoGameInput : IGameInput
{
    public bool IsGameForeground => true;

    public Task<InjectionResult> SendAsync(IReadOnlyList<InputStep> steps, CancellationToken cancellationToken = default) =>
        Task.FromResult(new InjectionResult(InjectionOutcome.GameNotFound, "Elite is not running."));

    public void ReleaseAll()
    {
    }
}
