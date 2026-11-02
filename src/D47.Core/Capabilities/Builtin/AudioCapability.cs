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
        AudioChannel.Overheard => (
            "Overheard chatter",
            "Invented radio traffic between other pilots and controllers, not meant for you."),
        AudioChannel.Speech => ("Speech", "Everything D47 says out loud."),
        _ => ("Alerts", "The danger callouts, which are the one thing that cuts in mid-sentence."),
    };

    /// <summary>The sound to listen to for a channel, and how to make it play.</summary>
    private static string Hear(AudioChannel channel) => channel switch
    {
        AudioChannel.Bed => "the quiet loop that plays while D47 works on a reply: ask a question that takes a "
                            + "while to answer, with Thinking bed ticked in the Cues group",
        AudioChannel.Music => "the ambient music: with Elite's own music volume set to zero (Options, Audio) "
                              + "and ambient music playing",
        AudioChannel.Cue => "the short sounds as listening starts, thinking starts and the reply finishes: hold "
                            + "push-to-talk and ask anything, with Sound cues ticked in the Cues group",
        AudioChannel.Overheard => "invented radio chatter between other pilots and controllers, which plays on "
                                  + "its own while you fly with the chatter callouts on (Settings › Other voices › "
                                  + "Chatter and messages)",
        AudioChannel.Speech => "D47's spoken replies: ask any question out loud and listen to the answer",
        _ => "a spoken danger callout: cause one, such as dropping below the fuel warning or being interdicted",
    };

    private static string Where(AudioChannel channel)
    {
        var (name, _) = Describe(channel);

        return "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), open Settings › Hearing and sound › Sounds and levels, group Levels (a table with "
               + "a row per channel and the columns Level, Mute and Duck while D47 speaks), and find the row "
               + name + ". ";
    }

    private static string Slug(AudioChannel channel) => channel.ToString().ToLowerInvariant();

    /// <summary>The drop-in folder row's key.</summary>
    public const string DropsKey = "audio.drops";

    /// <param name="drops">What was picked up from <c>data/audio/</c> and what was skipped, in words.</param>
    /// <param name="openFolder">Opens that folder, or null where there is no shell to open it with.</param>
    /// <param name="music">Pauses, resumes or skips the ambient music; null where nothing plays it.</param>
    /// <param name="library">The music library download's state line, or null when there is nothing to say.</param>
    /// <param name="thumbsDown">Queues the playing library track for deletion and skips it; null where nothing plays the music.</param>
    /// <param name="maintainer">Whether a Music publish token is present, which is what makes thumbs down reachable.</param>
    /// <param name="deleteRejects">Starts deleting the queued tracks and returns the reply; null where nothing deletes them.</param>
    public static CapabilityDescriptor Create(
        Func<string>? drops = null,
        Action? openFolder = null,
        Func<MusicAction, string>? music = null,
        Func<MusicState>? musicState = null,
        Func<Action, Action>? watchMusic = null,
        Func<string?>? library = null,
        Func<Action, Action>? watchLibrary = null,
        Func<string>? thumbsDown = null,
        Func<bool>? maintainer = null,
        Func<string>? deleteRejects = null) => new()
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
            .. Enum.GetValues<AudioChannel>().SelectMany(channel => channel == AudioChannel.Music
                ? [LibraryRow(library, watchLibrary), .. TransportRows(music, musicState, watchMusic), .. RowsFor(channel)]
                : RowsFor(channel)),
            .. drops is null ? Array.Empty<SettingRow>() : [DropsRow(drops, openFolder)],
            TokenRow,
            .. InterfaceCapability.HotkeyRows(
                ThumbsDownHotkeyKey,
                ThumbsDownButtonKey,
                "Thumbs down the playing track",
                "thumbs-down-music",
                s => s.Hotkeys.ThumbsDownMusic,
                (s, v) => s with { Hotkeys = s.Hotkeys with { ThumbsDownMusic = v } },
                systemWide: true,
                exercise: new(
                    "Settings › Hearing and sound › Sounds and levels, group Ambient music",
                    "(unbound)",
                    "with a Music publish token set and a library track playing, the track is added to "
                        + "data\\music-rejects.tsv and the next track starts. Without a token the row is not shown "
                        + "and the key does nothing."))
                .Select(row => row with { AppliesWhen = _ => maintainer?.Invoke() == true }),
        ],
        Tools = [ManageMusic(music), ThumbsDownMusic(thumbsDown, maintainer), DeleteMusicRejects(deleteRejects, maintainer)],
    };

    public const string ThumbsDownTool = "thumbs_down_music";

    public const string MusicPublishTokenKey = "audio.music.publishToken";

    public const string ThumbsDownHotkeyKey = "hotkeys.thumbsDownMusic";

    public const string ThumbsDownButtonKey = "hotkeys.thumbsDownMusicButton";

    private static readonly SettingRow TokenRow = new()
    {
        Key = MusicPublishTokenKey,
        Exercise =
            ShowMusic + "paste a fine-grained GitHub token with contents write on dseelinger/d47-music into Music "
            + "publish token. For the music library's maintainer only; it needs that GitHub account. The row never "
            + "shows the token back. Then say \"thumbs down this track\" while a library track plays: the track is "
            + "queued for deletion and the next one starts. Without a token, and without D47_MUSIC_PUBLISH_TOKEN in "
            + "the environment, the phrase does nothing.",
        Advanced = true,
        Label = "Music publish token",
        Help =
            "For the maintainer of the D47 music library. With a token here, \"thumbs down this track\" queues the "
            + "library track that is playing for deletion, and skips it.\n\n"
            + "It is a fine-grained GitHub token with contents write on dseelinger/d47-music only. It is stored "
            + "encrypted for this Windows account and is write-only. Without one here, D47 reads "
            + "D47_MUSIC_PUBLISH_TOKEN from the environment.",
        Kind = SettingKind.Secret,
        Group = MusicGroup,
        SecretName = MusicPublishToken.SecretName,
        DocsAnchor = "music-publish-token",
    };

    /// <summary>
    /// Queues the playing library track for deletion and skips it. Reached by phrase only while a token is
    /// present; never offered to the model.
    /// </summary>
    private static ToolDefinition ThumbsDownMusic(Func<string>? thumbsDown, Func<bool>? maintainer) => new()
    {
        Name = ThumbsDownTool,
        Exercise =
            "With a Music publish token set and a library track playing, say \"thumbs down this track\". The "
            + "reply says the track is queued and how many are queued, and the next track starts. For the "
            + "maintainer of the music library.",
        Description = "Queues the playing library music track for deletion and skips it.",
        Protected = true,
        RefusalExample = "thumbs down this track",
        Commands =
        [
            new ToolCommandPhrase("thumbs down this track") { When = () => maintainer?.Invoke() == true },
            new ToolCommandPhrase("thumbs down") { When = () => maintainer?.Invoke() == true },
        ],
        Handler = (_, _) => Task.FromResult(
            thumbsDown is null || maintainer?.Invoke() != true
                ? ToolResult.Error("Thumbs down is not available.")
                : ToolResult.Ok(thumbsDown())),
    };

    public const string DeleteRejectsTool = "delete_music_rejects";

    /// <summary>
    /// Deletes every queued track from the music library's release and from this PC. Reached by phrase only while a
    /// token is present; never offered to the model.
    /// </summary>
    private static ToolDefinition DeleteMusicRejects(Func<string>? deleteRejects, Func<bool>? maintainer) => new()
    {
        Name = DeleteRejectsTool,
        Exercise =
            "With a Music publish token set and at least one track thumbed down, say \"delete the thumbs-down "
            + "tracks\". The reply says how many tracks in how many folders are being deleted; a second reply, when "
            + "it ends, says what was deleted and what failed. The tracks are gone from data\\music-pack and from "
            + "data\\music-rejects.tsv, and the folder's zip on the music-1 release has a new version. For the "
            + "maintainer of the music library.",
        Description = "Deletes the queued music tracks from the music library's release and from this PC.",
        Protected = true,
        RefusalExample = "delete the thumbs-down tracks",
        Commands =
        [
            new ToolCommandPhrase("delete the thumbs-down tracks") { When = () => maintainer?.Invoke() == true },
        ],
        Handler = (_, _) => Task.FromResult(
            deleteRejects is null || maintainer?.Invoke() != true
                ? ToolResult.Error("Deleting the thumbs-down tracks is not available.")
                : ToolResult.Ok(deleteRejects())),
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
        Exercise =
            "With ambient music playing, say \"pause the music\" and it holds on the track. \"Resume "
            + "the music\" restarts it and \"next track\" starts another track from the same folder. The "
            + "reply says what the music did.",
        Description = "Pauses, resumes or skips D47's ambient music. Pause holds the track where it is; next "
                      + "starts another track from the same folder.",
        Parameters =
        [
            ToolParameter.OneOf("action", "What to do to the music.", ["pause", "resume", "next"], required: true),
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

    public const string NowPlayingKey = "audio.music.now";

    public const string MusicLibraryKey = "audio.music.library";

    public const string NextTrackKey = "audio.music.next";

    private const string MusicGroup = "Ambient music";

    private const string ShowMusic = "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), "
                                     + "open Settings › Hearing and sound › Sounds and levels, group Ambient music, ";

    /// <summary>What the transport row says: the track, or why there is none.</summary>
    internal static string NowPlaying(MusicState state, bool muted) => state switch
    {
        _ when muted => "Muted",
        { Paused: true } or { Idle: true } => "Paused",
        { Track: { } track } => Path.GetFileNameWithoutExtension(track),
        _ => "Nothing playing",
    };

    private static bool CanControl(MusicState state, D47Settings settings) =>
        !settings.Audio.For(AudioChannel.Music).Muted && (state.Playing || state.Paused || state.Idle);

    /// <summary>Pause or resume, and next, above the music's level row. None where nothing plays the music.</summary>
    private static IEnumerable<SettingRow> TransportRows(
        Func<MusicAction, string>? music,
        Func<MusicState>? musicState,
        Func<Action, Action>? watch)
    {
        if (music is null || musicState is null)
        {
            yield break;
        }

        var (_, what) = Describe(AudioChannel.Music);

        yield return new SettingRow
        {
            Key = NowPlayingKey,
            Exercise =
                ShowMusic + "find Now playing. It shows the track's file name while music plays, Paused while "
                + "paused, Muted while the music channel is muted and Nothing playing otherwise. The button reads "
                + "Pause while a track plays: press it and the music holds, the row shows Paused and the button "
                + "reads Resume; press Resume and the track carries on. The button is greyed out when nothing "
                + "plays or the channel is muted. Saying \"pause the music\" or \"resume the music\" does the "
                + "same. Needs ambient music playing, which follows what you are doing in game.",
            Advanced = true,
            Label = "Now playing",
            Help = "The track that is playing. Pause holds it where it is; Resume carries on. "
                   + "Voice and the media keys do the same.",
            Kind = SettingKind.Info,
            Group = MusicGroup,
            GroupHelp = what,
            DocsAnchor = "the-six-categories",
            Binding = new SettingBinding
            {
                Read = s => NowPlaying(musicState(), s.Audio.For(AudioChannel.Music).Muted),
            },
            PressLabelFor = () => musicState() is { Paused: true } or { Idle: true } ? "Resume" : "Pause",
            PressEnabled = s => CanControl(musicState(), s),
            Press = () => music(musicState() is { Paused: true } or { Idle: true } ? MusicAction.Resume : MusicAction.Pause),
            Watch = watch,
        };

        yield return new SettingRow
        {
            Key = NextTrackKey,
            Exercise =
                ShowMusic + "find Next track and press Next while music plays: another track from the same "
                + "folder starts and Now playing shows its file name. The button is greyed out when nothing plays "
                + "or the channel is muted. Saying \"next track\" does the same. Needs ambient music playing.",
            Advanced = true,
            Label = "Next track",
            Help = "Starts another track from the same folder.",
            Kind = SettingKind.Info,
            Group = MusicGroup,
            GroupHelp = what,
            DocsAnchor = "the-six-categories",
            PressLabel = "Next",
            PressEnabled = s => CanControl(musicState(), s),
            Press = () => music(MusicAction.Next),
            Watch = watch,
        };
    }

    /// <summary>Whether d47 downloads the music library, with the download's state under it.</summary>
    private static SettingRow LibraryRow(Func<string?>? library, Func<Action, Action>? watch) => new()
    {
        Key = MusicLibraryKey,
        Exercise =
            "Open Settings › Hearing and sound › Sounds and levels, group Ambient music, and find Download the "
            + "D47 music library (off by default). Tick it: the line under it gives the library's size and counts "
            + "folders in as each one lands in data\\music-pack, naming the folder being fetched. Needs a network "
            + "connection to GitHub and several gigabytes free. With Elite running and Elite's music volume at zero, "
            + "every situation plays the library, together with any files of your own. Untick it: the music stops and starts again "
            + "without the library, nothing more is downloaded, and the files stay in data\\music-pack.",
        Label = "Download the D47 music library",
        Help =
            "Ambient music for every situation, several gigabytes in all, downloaded in the background from "
            + "GitHub into data\\music-pack, most-used folders first. Files of your own in a music folder play "
            + "alongside the library's.\n\n"
            + "Off, nothing more is downloaded and the library does not play. The files stay on disk; deleting "
            + "data\\music-pack reclaims the space.",
        Kind = SettingKind.Toggle,
        Group = MusicGroup,
        GroupHelp = Describe(AudioChannel.Music).What,
        DocsAnchor = "the-music-library",
        Note = library is null ? null : _ => library(),
        Watch = watch,
        Binding = SettingBinding.Toggle(
            s => s.Ui.MusicLibrary,
            (s, on) => s with { Ui = s.Ui with { MusicLibrary = on } }),
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
            Exercise = Where(channel) + $"Move Level from its current value to 0 and then to 1 while you hear {Hear(channel)}: "
                       + "it gets quieter to silence at 0 and louder at 1, in steps of 0.05. Set it back "
                       + "and the original loudness returns.",
            Advanced = true,
            Protected = true,
            Label = "Level",
            Help = "0 is silent and 1 is full. Muting is separate, so turning something off does not "
                   + "cost you the level you had it at.",
            Kind = SettingKind.Number,
            Step = 0.05,
            Minimum = 0,
            Maximum = 1,
            Group = group,
            GroupHelp = what,
            // The page explains the six categories together and has no heading per channel, so this points
            // at the section rather than at a heading that would have to be written to satisfy a link (#123).
            DocsAnchor = "the-six-categories",
            Binding = Bind(channel, mix => Number(mix.Level), (mix, v) => mix with { Level = Fraction(v, mix.Level) }),
        };

        yield return new SettingRow
        {
            Key = MuteKey(channel),
            Exercise = Where(channel) + $"Tick Mute while you hear {Hear(channel)}: it goes silent. Untick it and "
                       + "it returns at the Level the row had, because Mute does not change the Level.",
            Advanced = true,
            Protected = true,
            Label = "Mute",
            Help = "Off, without losing the level. A level of zero and a mute sound the same and mean "
                   + "different things.",
            Kind = SettingKind.Toggle,
            Group = group,
            GroupHelp = what,
            DocsAnchor = "the-six-categories",
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
            Exercise = Where(channel) + $"Set Duck while D47 speaks to 0 and have D47 speak a reply while {Hear(channel)} "
                       + "is playing: it falls silent until the sentence ends, then returns. At 1 it does not "
                       + "drop at all. Between, it drops to that fraction of its Level while D47 speaks. "
                       + "The row appears for Thinking bed, Ambient music and Sound cues only.",
            Advanced = true,
            Protected = true,
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
