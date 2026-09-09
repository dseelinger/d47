using D47.Core.Capabilities.Builtin;

namespace D47.Core.Listening;

/// <summary>The line under the microphone indicator, saying what would open the gate (Phase 13).</summary>
public static class MicrophoneNarration
{
    /// <summary>
    /// <param name="wakePhrases"> What d47 answers to, from <see cref="ListeningWiring.WakePhrases"/>.
    /// </summary>
    /// <param name="wakePhrases">
    /// What d47 answers to, from <see cref="ListeningWiring.WakePhrases"/>.
    /// </param>
    /// <param name="gesture">
    /// The push-to-talk key as a Commander would say it, or null when nothing is bound.
    /// </param>
    public static string For(
        MicrophoneState state,
        string? mode,
        IReadOnlyList<string> wakePhrases,
        string? gesture,
        int preRollMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(wakePhrases);

        return state switch
        {
            MicrophoneState.Armed when string.Equals(mode, ListeningCapability.WakeMode, StringComparison.Ordinal) =>
                wakePhrases is { Count: > 0 } names
                    ? $"say {names[0]} and D47 will listen"
                    : "say D47's name and it will listen",

            MicrophoneState.Armed => "D47 opens the microphone by itself when it hears you start",

            // The pre-roll is the point: audio is already being captured, and this says how long it survives
            // before being discarded unheard.
            MicrophoneState.Idle when gesture is not null =>
                $"audio is discarded within {preRollMilliseconds} ms unless you "
                + $"{(string.Equals(mode, ListeningCapability.ToggleMode, StringComparison.Ordinal) ? "press" : "hold")} "
                + gesture,

            _ => string.Empty,
        };
    }

    /// <summary>
    /// What a prompt that is waiting on a spoken value says about itself (remediation.md 10, item 12).
    /// </summary>
    /// <param name="gesture">
    /// The push-to-talk key as a Commander would say it, or null when nothing is bound — rendered by
    /// the caller for the reason <see cref="For"/> already gives.
    /// </param>
    public static string Prompt(string? mode, IReadOnlyList<string> wakePhrases, string? gesture)
    {
        ArgumentNullException.ThrowIfNull(wakePhrases);

        if (string.Equals(mode, ListeningCapability.WakeMode, StringComparison.Ordinal))
        {
            return wakePhrases is { Count: > 0 } names
                ? $"Say {names[0]}, then say it."
                : "Say D47's name, then say it.";
        }

        if (string.Equals(mode, ListeningCapability.ContinuousMode, StringComparison.Ordinal))
        {
            return "Say it — I am listening.";
        }

        // Hold and toggle both need a key, and neither can be done without one.
        if (gesture is null)
        {
            return "No push-to-talk key is bound. Type it instead.";
        }

        return string.Equals(mode, ListeningCapability.ToggleMode, StringComparison.Ordinal)
            ? $"Press {gesture} and say it."
            : $"Hold {gesture} and say it.";
    }
}
