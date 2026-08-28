using D47.Core.Capabilities.Builtin;

namespace D47.Core.Listening;

/// <summary>
/// The line under the microphone indicator, saying what would open the gate (Phase 13).
/// <para>
/// A settings question rather than a view one, which is why it is neither in the view — which
/// reads no settings — nor on the gate, which knows about audio and not about keys. It was in
/// <c>AppHost</c> for want of a third place; this is the third place.
/// </para>
/// <para>
/// Pure, so the sentences a Commander actually reads are covered by a test rather than by
/// launching the app and looking at the panel (Phase 19).
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

    /// <summary>
    /// What a prompt that is waiting on a spoken value says about itself (remediation.md 10,
    /// item 12).
    /// <para>
    /// It used to say "Say it — I am listening." in every mode, with a comment arguing that the
    /// wording covered both — that under push-to-talk the microphone is not open until a key is
    /// held, and the panel's own indicator row says which state it is really in. The Commander
    /// read it and reported it as a lie, which settles it: a prompt that claims to be listening
    /// while the gate is shut is wrong however carefully the sentence was chosen, and pointing at
    /// another row is asking somebody to reconcile two controls to find out whether one of them
    /// is true.
    /// </para>
    /// <para>
    /// So it says what would open the gate, in the imperative, because the Commander is being
    /// asked to do something. Only continuous mode claims to be listening, and only continuous
    /// mode is.
    /// </para>
    /// </summary>
    /// <param name="gesture">
    /// The push-to-talk key as a Commander would say it, or null when nothing is bound — rendered
    /// by the caller for the reason <see cref="For"/> already gives.
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

        // Hold and toggle both need a key, and neither can be done without one. An unbound key
        // is not a degraded hint, it is a different instruction: there is no way to speak here at
        // all, and the keyboard is the answer rather than a preference.
        if (gesture is null)
        {
            return "No push-to-talk key is bound. Type it instead.";
        }

        return string.Equals(mode, ListeningCapability.ToggleMode, StringComparison.Ordinal)
            ? $"Press {gesture} and say it."
            : $"Hold {gesture} and say it.";
    }
}
