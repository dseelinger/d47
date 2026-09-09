using System.Text.Json;
using System.Text.Json.Serialization;

namespace D47.Core.Audio;

/// <summary>One category's share of the output (Phase 12, "#96 Ambient audio mixer").</summary>
/// <param name="Level">How loud, 0 to 1.</param>
/// <param name="Muted">Off, without losing the level.</param>
/// <param name="DuckUnderSpeech">
/// What this drops to while something is being said, 0 to 1. 1 is "does not duck".
/// </param>
public sealed record ChannelMix(double Level, bool Muted, double DuckUnderSpeech)
{
    /// <inheritdoc cref="Configuration.D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>Full, unmuted, and not ducking.</summary>
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

/// <summary>Per-category level, mute and ducking, for every channel the arbiter knows about.</summary>
public sealed record AudioMix
{
    /// <inheritdoc cref="Configuration.D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>
    /// The bed's duck was a constant in the arbiter — "enough to stay present as evidence the turn is
    /// still running, quiet enough not to compete with the words".
    /// </summary>
    public ChannelMix Bed { get; init; } = new(1.0, Muted: false, DuckUnderSpeech: 0.35);

    /// <summary>Music starts quieter than everything else and ducks harder.</summary>
    public ChannelMix Music { get; init; } = new(0.5, Muted: false, DuckUnderSpeech: 0.2);

    public ChannelMix Cue { get; init; } = ChannelMix.Full;

    /// <summary>Speech does not duck: it is what everything else ducks under.</summary>
    public ChannelMix Speech { get; init; } = ChannelMix.Full;

    /// <summary>Nor does an alert.</summary>
    public ChannelMix Alert { get; init; } = ChannelMix.Full;

    /// <summary>What d47 sounded like before there was a mixer.</summary>
    public static readonly AudioMix Default = new();

    /// <summary>The channels that can duck — everything except the two that are ducked under.</summary>
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
