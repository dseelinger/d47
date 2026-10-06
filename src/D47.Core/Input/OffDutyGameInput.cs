using D47.Core.Journal;

namespace D47.Core.Input;

/// <summary>Refuses every send while d47 shows a Commander other than the one Elite is running.</summary>
public sealed class OffDutyGameInput(IGameInput inner, GameStateStore commanders) : IGameInput
{
    public bool IsGameRunning => inner.IsGameRunning;

    public bool IsGameForeground => inner.IsGameForeground;

    public Task<InjectionResult> SendAsync(IReadOnlyList<InputStep> steps, CancellationToken cancellationToken = default) =>
        SendAsync(steps, null, cancellationToken);

    public Task<InjectionResult> SendAsync(
        IReadOnlyList<InputStep> steps,
        IInputTrace? trace,
        CancellationToken cancellationToken = default) =>
        Refusal() is { } refused ? Task.FromResult(refused) : inner.SendAsync(steps, trace, cancellationToken);

    public IInputTrace? Trace(string caller) => inner.Trace(caller);

    public void ReleaseAll() => inner.ReleaseAll();

    /// <summary>Why nothing may be sent now, or null while the shown Commander is the one in the game.</summary>
    public InjectionResult? Refusal()
    {
        if (!commanders.IsOffDuty)
        {
            return null;
        }

        return new InjectionResult(
            InjectionOutcome.OffDuty,
            commanders.InGame is { } inGame
                ? $"Elite is running Commander {inGame.Identity.Name}, not the Commander shown here, so I will not press keys in it."
                : "Elite has not named the Commander it is running, and it is not the one shown here, so I will not press keys in it.");
    }
}
