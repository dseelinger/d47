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
        AudioChannel.Bed => ("Thinking bed", "The loop that plays underneath a response while D47 works."),
        AudioChannel.Music => (
            "Ambient music",
            "The background layer that follows what you are doing. To hear it instead of Elite's, set Elite's "
            + "music volume to zero (Options, Audio)."),
        AudioChannel.Cue => ("Sound cues", "The short markers for listening, thinking and answering."),
        AudioChannel.Speech => ("Speech", "Everything D47 says out loud."),
        _ => ("Alerts", "The danger callouts, which are the one thing that cuts in mid-sentence."),
    };

    private static string Slug(AudioChannel channel) => channel.ToString().ToLowerInvariant();

    /// <summary>The drop-in folder row's key.</summary>
    public const string DropsKey = "audio.drops";

    /// <param name="drops">What was picked up from <c>data/audio/</c> and what was skipped, in words.</param>
    /// <param name="openFolder">Opens that folder, or null where there is no shell to open it with.</param>
    /// <param name="music">Pauses, resumes or skips the ambient music; null where nothing plays it.</param>
    public static CapabilityDescriptor Create(
        Func<string>? drops = null,
        Action? openFolder = null,
        Func<MusicAction, string>? music = null) => new()
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
        },
        Settings =
        [
            .. Enum.GetValues<AudioChannel>().SelectMany(RowsFor),
            .. drops is null ? Array.Empty<SettingRow>() : [DropsRow(drops, openFolder)],
        ],
        Tools = [ManageMusic(music)],
    };

    public const string ManageMusicTool = "manage_music";

    private static readonly Dictionary<string, MusicAction> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pause"] = MusicAction.Pause,
        ["resume"] = MusicAction.Resume,
        ["next"] = MusicAction.Next,
    };

    /// <summary>Pause, resume and skip for the ambient music. Levels and mutes stay out of the model's reach.</summary>
    private static ToolDefinition ManageMusic(Func<MusicAction, string>? music) => new()
    {
        Name = ManageMusicTool,
        Description = "Pauses, resumes or skips D47's ambient music. Pause holds the track where it is; next "
                      + "starts another track from the same folder.",
        Parameters =
        [
            new ToolParameter
            {
                Name = "action",
                Type = ToolParameterType.String,
                Description = "What to do to the music.",
                Required = true,
                AllowedValues = ["pause", "resume", "next"],
            },
        ],
        Commands =
        [
            Phrase("pause the music", "pause"),
            Phrase("pause music", "pause"),
            Phrase("resume the music", "resume"),
            Phrase("resume music", "resume"),
            Phrase("unpause the music", "resume"),
            Phrase("next track", "next"),
            Phrase("skip track", "next"),
            Phrase("skip this track", "next"),
        ],
        Handler = (arguments, _) =>
        {
            if (!arguments.TryGetString("action", out var said) || !Actions.TryGetValue(said, out var action))
            {
                return Task.FromResult(ToolResult.Error("The action is pause, resume or next."));
            }

            return Task.FromResult(music is null
                ? ToolResult.Error("Ambient music is not available.")
                : ToolResult.Ok(music(action)));
        },
    };

    private static ToolCommandPhrase Phrase(string phrase, string action) =>
        new(phrase, new Dictionary<string, string>(StringComparer.Ordinal) { ["action"] = action });

    /// <summary>
    /// What d47 found in the Commander's own folder — and, more to the point, what it could not use.
    /// </summary>
    private static SettingRow DropsRow(Func<string> drops, Action? openFolder) => new()
    {
        Key = DropsKey,
        Advanced = true,
        Label = "Your own audio",
        Help = "Drop .mp3, .m4a, .aac, .wma, .flac or .wav files into data\\audio, beside d47.exe: cues/<state> "
               + "replaces a sound cue, beds/<name> adds a thinking bed, and music/<situation>/ is "
               + "ambience. Any sample rate and channel count. They are picked up without a restart.",
        Kind = SettingKind.Info,
        Group = "Your own audio",
        DocsAnchor = "your-own-sounds",
        PressLabel = openFolder is null ? null : "Open audio folder",
        Press = openFolder,
        Binding = new SettingBinding { Read = _ => drops() },
    };

    private static IEnumerable<SettingRow> RowsFor(AudioChannel channel)
    {
        var (name, what) = Describe(channel);
        var group = name;

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
            GroupHelp = what,
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
            GroupHelp = what,
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
            Label = "Duck while D47 speaks",
            Help = "What this drops to while D47 is talking, as a fraction of its level. 1 does not duck "
                   + "at all; 0 goes silent until the sentence ends.",
            Kind = SettingKind.Number,
            Step = 0.05,
            Minimum = 0,
            Maximum = 1,
            Group = group,
            GroupHelp = what,
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
