using D47.Core.Capabilities.Builtin;

namespace D47.Core.Listening;

/// <summary>
/// The line under the microphone indicator, saying what would open the gate (list.md Phase 13).
/// <para>
/// A settings question rather than a view one, which is why it is neither in the view — which
/// reads no settings — nor on the gate, which knows about audio and not about keys. It was in
/// <c>AppHost</c> for want of a third place; this is the third place.
/// </para>
/// <para>
/// Pure, so the sentences a Commander actually reads are covered by a test rather than by
/// launching the app and looking at the panel (list.md Phase 19).
/// </para>
/// </summary>
public static class MicrophoneNarration
{
    /// <param name="wakePhrases">
    /// What d47 answers to, from <see cref="ListeningWiring.WakePhrases"/>. The first is named
    /// because it is the one the Commander is most likely to have meant; naming all of them
    /// turns a hint into a list.
    /// </param>
    /// <param name="gesture">
    /// The push-to-talk key as a Commander would say it, or null when nothing is bound. Rendered
    /// by the caller: describing a key is the App's business, and Core has no keyboard.
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

            // The pre-roll is the point: audio is already being captured, and this says how long
            // it survives before being discarded unheard. A Commander who does not know that
            // reads an armed microphone as d47 recording them.
            MicrophoneState.Idle when gesture is not null =>
                $"audio is discarded within {preRollMilliseconds} ms unless you "
                + $"{(string.Equals(mode, ListeningCapability.ToggleMode, StringComparison.Ordinal) ? "press" : "hold")} "
                + gesture,

            _ => string.Empty,
        };
    }
}
