namespace D47.Core.Audio;

/// <summary>
/// One category's share of the output (Phase 12, "#96 Ambient audio mixer").
/// </summary>
/// <param name="Level">
/// How loud, 0 to 1. A level of 0 and <paramref name="Muted"/> sound identical and mean
/// different things: one is a setting the Commander chose and the other is a switch they can
/// flick back without having to remember where the slider was.
/// </param>
/// <param name="Muted">Off, without losing the level.</param>
/// <param name="DuckUnderSpeech">
/// What this drops to while something is being said, 0 to 1. 1 is "does not duck". It is a
/// multiplier on the level rather than a level of its own, so turning a category down turns its
/// ducked form down with it.
/// </param>
public sealed record ChannelMix(double Level, bool Muted, double DuckUnderSpeech)
{
    /// <summary>Full, unmuted, and not ducking. What a category with nothing to yield to gets.</summary>
    public static readonly ChannelMix Full = new(1.0, Muted: false, DuckUnderSpeech: 1.0);

    /// <summary>The gain this category plays at, given whether something is being said over it.</summary>
    public float GainWhile(bool speaking)
    {
        if (Muted)
        {
            return 0f;
        }

        var level = Math.Clamp(Level, 0, 1);

        return (float)(speaking ? level * Math.Clamp(DuckUnderSpeech, 0, 1) : level);
    }
}

/// <summary>
/// Per-category level, mute and ducking, for every channel the arbiter knows about.
/// <para>
/// One property per channel rather than a dictionary, for the same reason the hotkeys are one
/// property per action: an unknown category name in a hand-edited settings file should be
/// refused like any other unknown key, and a dictionary would take it silently. The settings
/// rows are still generated from <see cref="AudioChannel"/> rather than written out one at a
/// time, so a channel added later cannot ship without rows — see <c>AudioCapability</c>.
/// </para>
/// <para>
/// The defaults are what d47 sounded like before there was a mixer, which is the property that
/// matters: turning this on must not change what a Commander who never touches it hears.
/// </para>
/// </summary>
public sealed record AudioMix
{
    /// <summary>
    /// The bed's duck was a constant in the arbiter — "enough to stay present as evidence the
    /// turn is still running, quiet enough not to compete with the words". It is the same
    /// number, now sayable.
    /// </summary>
    public ChannelMix Bed { get; init; } = new(1.0, Muted: false, DuckUnderSpeech: 0.35);

    /// <summary>
    /// Music starts quieter than everything else and ducks harder. It is the one category that
    /// is meant to be under the others rather than among them, and a background layer arriving
    /// at full level is a background layer the Commander turns off rather than down.
    /// </summary>
    public ChannelMix Music { get; init; } = new(0.5, Muted: false, DuckUnderSpeech: 0.2);

    public ChannelMix Cue { get; init; } = ChannelMix.Full;

    /// <summary>Speech does not duck: it is what everything else ducks under.</summary>
    public ChannelMix Speech { get; init; } = ChannelMix.Full;

    /// <summary>Nor does an alert. An alert that yielded to a sentence would not be an alert.</summary>
    public ChannelMix Alert { get; init; } = ChannelMix.Full;

    /// <summary>What d47 sounded like before there was a mixer.</summary>
    public static readonly AudioMix Default = new();

    /// <summary>
    /// The channels that can duck — everything except the two that are ducked <em>under</em>.
    /// Used by the settings rows so a duck row is only offered where it means something.
    /// </summary>
    public static bool Ducks(AudioChannel channel) =>
        channel is not (AudioChannel.Speech or AudioChannel.Alert);

    public ChannelMix For(AudioChannel channel) => channel switch
    {
        AudioChannel.Bed => Bed,
        AudioChannel.Music => Music,
        AudioChannel.Cue => Cue,
        AudioChannel.Alert => Alert,
        _ => Speech,
    };

    public AudioMix With(AudioChannel channel, ChannelMix mix) => channel switch
    {
        AudioChannel.Bed => this with { Bed = mix },
        AudioChannel.Music => this with { Music = mix },
        AudioChannel.Cue => this with { Cue = mix },
        AudioChannel.Alert => this with { Alert = mix },
        _ => this with { Speech = mix },
    };
}
