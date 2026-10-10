using D47.Core.Audio;

namespace D47.App;

/// <summary>What d47 says aloud at startup about settings or the model, or nothing.</summary>
public static class StartupProblems
{
    public sealed record Problem(LoopState State, string Text);

    /// <param name="setupShown">The first-run wizard was offered; it already covers a missing model.</param>
    public static Problem? Say(bool settingsFailed, bool setupShown, bool canAttemptModelTurn, string? reason)
    {
        if (settingsFailed)
        {
            return new Problem(LoopState.Failed, "My settings did not load cleanly.");
        }

        if (setupShown || canAttemptModelTurn || reason is null)
        {
            return null;
        }

        return new Problem(
            LoopState.Unsure,
            $"I have no language model right now. {reason} I can still answer from my own capabilities.");
    }
}
