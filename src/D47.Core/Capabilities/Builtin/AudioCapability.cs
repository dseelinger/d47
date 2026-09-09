using System.Globalization;
using D47.Core.Audio;
using D47.Core.Configuration;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// The mixer: how loud each category is, whether it is muted, and how far it drops while d47 is
/// speaking (Phase 12, "#96 Ambient audio mixer").
/// </summary>
public static class AudioCapability
{
    public const string Id = "audio";

    /// <summary>Row keys, so a caller naming one does not spell it by hand.</summary>
    public static string LevelKey(AudioChannel channel) => $"audio.{Slug(channel)}.level";

    public static string MuteKey(AudioChannel channel) => $"audio.{Slug(channel)}.mute";

    public static string DuckKey(AudioChannel channel) => $"audio.{Slug(channel)}.duck";

    /// <summary>
    /// What each category is, in the Commander's words rather than the enum's. "Bed" is a mixing term
    /// and "Cue" is an internal one; neither says what the sound is.
    /// </summary>
    private static (string Name, string What) Describe(AudioChannel channel) => channel switch
    {
        AudioChannel.Bed => ("Thinking bed", "the loop that plays underneath a response while D47 works"),
        AudioChannel.Music => ("Ambient music", "the background layer that follows what you are doing"),
        AudioChannel.Cue => ("Sound cues", "the short markers for listening, thinking and answering"),
        AudioChannel.Speech => ("Speech", "everything D47 says out loud"),
        _ => ("Alerts", "the danger callouts, which are the one thing that cuts in mid-sentence"),
    };

    private static string Slug(AudioChannel channel) => channel.ToString().ToLowerInvariant();

    /// <summary>The drop-in folder row's key.</summary>
    public const string DropsKey = "audio.drops";

    /// <summary>
    /// <param name="drops"> What was picked up from <c>data/audio/</c> and what was skipped, in words.
    /// </summary>
    /// <param name="drops">
    /// What was picked up from <c>data/audio/</c> and what was skipped, in words.
    /// </param>
    public static CapabilityDescriptor Create(Func<string>? drops = null) => new()
    {
        Id = Id,
        Group = "Voice",
        Name = "Audio mixer",
        Summary = "How loud each kind of sound is, and how far it drops while D47 is speaking.",
        Display = new CapabilityDisplay
        {
            PanelTitle = "Audio mixer",

            // Stated rather than defaulted (#83).
            Order = 96,
            StartCollapsed = true,
        },
        Settings =
        [
            .. Enum.GetValues<AudioChannel>().SelectMany(RowsFor),
            .. drops is null ? Array.Empty<SettingRow>() : [DropsRow(drops)],
        ],
    };

    /// <summary>
    /// What d47 found in the Commander's own folder — and, more to the point, what it could not use.
    /// </summary>
    private static SettingRow DropsRow(Func<string> drops) => new()
    {
        Key = DropsKey,
        Advanced = true,
        Label = "Your own audio",
        Help = "Drop 16-bit mono 48 kHz .wav files into data/audio: cues/<state>.wav replaces a "
               + "sound cue, beds/<name>.wav adds a thinking bed, and music/<situation>/*.wav is "
               + "ambience. They are picked up without a restart.",
        Kind = SettingKind.Info,
        Group = "Your own audio",
        GroupHelp = "What D47 found beside the set it ships with.",
        DocsAnchor = "your-own-sounds",
        Binding = new SettingBinding { Read = _ => drops() },
    };

    private static IEnumerable<SettingRow> RowsFor(AudioChannel channel)
    {
        var (name, what) = Describe(channel);
        var group = name;
        var groupHelp = $"Level and mute for {what}.";

        yield return new SettingRow
        {
            Key = LevelKey(channel),
            Advanced = true,
            Label = "Level",
            Help = "0 is silent and 1 is full. Muting is separate, so turning something off does not "
                   + "cost you the level you had it at.",
            Kind = SettingKind.Number,
            Step = 0.05,
            Minimum = 0,
            Maximum = 1,
            Group = group,
            GroupHelp = groupHelp,
            // The page explains the five categories together and has no heading per channel, so this points
            // at the section rather than at a heading that would have to be written to satisfy a link (#123).
            DocsAnchor = "the-five-categories",
            Binding = Bind(channel, mix => Number(mix.Level), (mix, v) => mix with { Level = Fraction(v, mix.Level) }),
        };

        yield return new SettingRow
        {
            Key = MuteKey(channel),
            Advanced = true,
            Label = "Mute",
            Help = "Off, without losing the level. A level of zero and a mute sound the same and mean "
                   + "different things.",
            Kind = SettingKind.Toggle,
            Group = group,
            GroupHelp = groupHelp,
            DocsAnchor = "the-five-categories",
            Binding = Bind(
                channel,
                mix => mix.Muted ? "true" : "false",
                (mix, v) => mix with { Muted = string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) }),
        };

        // Speech and alerts are what everything else ducks under.
        if (!AudioMix.Ducks(channel))
        {
            yield break;
        }

        yield return new SettingRow
        {
            Key = DuckKey(channel),
            Advanced = true,
            Label = "Duck while speaking",
            Help = "What this drops to while D47 is talking, as a fraction of its level. 1 does not duck "
                   + "at all; 0 goes silent until the sentence ends.",
            Kind = SettingKind.Number,
            Step = 0.05,
            Minimum = 0,
            Maximum = 1,
            Group = group,
            GroupHelp = groupHelp,
            DocsAnchor = "ducking",
            Binding = Bind(
                channel,
                mix => Number(mix.DuckUnderSpeech),
                (mix, v) => mix with { DuckUnderSpeech = Fraction(v, mix.DuckUnderSpeech) }),
        };
    }

    private static SettingBinding Bind(
        AudioChannel channel,
        Func<ChannelMix, string?> read,
        Func<ChannelMix, string?, ChannelMix> write) => new()
        {
            Read = s => read(s.Audio.For(channel)),
            Write = (s, v) => s with { Audio = s.Audio.With(channel, write(s.Audio.For(channel), v)) },
        };

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Clamped rather than refused.</summary>
    private static double Fraction(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, 0, 1)
            : fallback;
}
