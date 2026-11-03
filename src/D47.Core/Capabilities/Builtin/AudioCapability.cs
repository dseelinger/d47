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
    /// <param name="thumbsDown">Recycles the playing own track, or queues the playing library track for a maintainer, and skips it; null where nothing plays the music.</param>
    /// <param name="maintainer">Whether a Music publish token is present, which is what makes deleting the queued tracks reachable.</param>
    /// <param name="deleteRejects">Starts deleting the queued tracks and returns the reply; null where nothing deletes them.</param>
    /// <param name="rejectCount">How many tracks are queued for deletion.</param>
    /// <param name="rejectsRunning">Whether a deletion is running.</param>
    /// <param name="deleteRejectsNow">The Delete button's press: starts the deletion and says its replies.</param>
    /// <param name="watchRejects">Subscribes a refresh to changes in the queue and in the running deletion.</param>
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
        Func<string>? deleteRejects = null,
        Func<OwnAudioReport>? ownAudio = null,
        Func<int>? rejectCount = null,
        Func<bool>? rejectsRunning = null,
        Action? deleteRejectsNow = null,
        Func<Action, Action>? watchRejects = null) => new()
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
            .. drops is null ? Array.Empty<SettingRow>() : [DropsRow(drops)],
            .. ownAudio is null ? Array.Empty<SettingRow>() : OwnAudioRows(ownAudio, openFolder),
            TokenRow,
            RejectsRow(rejectCount, rejectsRunning, deleteRejectsNow, watchRejects, maintainer),
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
                    "with one of your own tracks playing, the file moves to the Recycle Bin and the next track "
                        + "starts. With a Music publish token set and a library track playing, the track is added to "
                        + "data\\music-rejects.tsv and the next track starts. Without a token a library track "
                        + "keeps playing.")),
        ],
        Tools = [ManageMusic(music), ThumbsDownMusic(thumbsDown), DeleteMusicRejects(deleteRejects, maintainer)],
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
            + "the environment, a library track is refused and keeps playing.",
        Advanced = true,
        Label = "Music publish token",
        Help =
            "For the maintainer of the D47 music library. With a token here, \"thumbs down this track\" queues the "
            + "library track that is playing for deletion, and skips it. Without one, it works only on your own tracks.\n\n"
            + "It is a fine-grained GitHub token with contents write on dseelinger/d47-music only. It is stored "
            + "encrypted for this Windows account and is write-only. Without one here, D47 reads "
            + "D47_MUSIC_PUBLISH_TOKEN from the environment.",
        Kind = SettingKind.Secret,
        Group = MusicGroup,
        SecretName = MusicPublishToken.SecretName,
        DocsAnchor = "music-publish-token",
    };

    public const string MusicRejectsKey = "audio.music.rejects";

    /// <summary>The queued tracks' count with a Delete button; drawn only with a Music publish token.</summary>
    private static SettingRow RejectsRow(
        Func<int>? count,
        Func<bool>? running,
        Action? delete,
        Func<Action, Action>? watch,
        Func<bool>? maintainer) => new()
    {
        Key = MusicRejectsKey,
        Exercise =
            ShowMusic + "with a Music publish token set, find Thumbs-down tracks after Music publish token. It reads "
            + "\"None queued\", \"1 track queued for deletion\" or \"3 tracks queued for deletion\" and rises when "
            + "you thumb down a library track. Press Delete, then press it again within a few seconds: the same "
            + "run as saying \"delete the thumbs-down tracks\" starts, the first reply is spoken, and a second reply "
            + "when it ends. The count falls as tracks are removed. The button is greyed out when none are queued "
            + "and while a deletion runs. Without a token the row is not drawn.",
        Advanced = true,
        Label = "Thumbs-down tracks",
        Help =
            "For the maintainer of the D47 music library. The library tracks you thumbed down are queued, and Delete "
            + "removes them from the library's download and from this PC. It does what saying \"delete the "
            + "thumbs-down tracks\" does.",
        Kind = SettingKind.Info,
        Group = MusicGroup,
        DocsAnchor = "delete-music-rejects",
        AppliesWhen = _ => count is not null && maintainer?.Invoke() == true,
        Binding = new SettingBinding { Read = _ => QueuedText(count?.Invoke() ?? 0) },
        PressLabel = "Delete",
        ConfirmPress = true,
        Destructive = true,
        PressEnabled = _ => count?.Invoke() > 0 && running?.Invoke() != true,
        Press = delete,
        Watch = watch,
    };

    /// <summary>How many tracks are queued, in words.</summary>
    internal static string QueuedText(int count) => count switch
    {
        <= 0 => "None queued",
        1 => "1 track queued for deletion",
        _ => $"{count} tracks queued for deletion",
    };

    /// <summary>
    /// Moves the playing own track to the Recycle Bin and skips it; with a token, queues a library track
    /// instead. Never offered to the model.
    /// </summary>
    private static ToolDefinition ThumbsDownMusic(Func<string>? thumbsDown) => new()
    {
        Name = ThumbsDownTool,
        Exercise =
            "With one of your own tracks playing, say \"thumbs down this track\". The reply names the track and "
            + "says it was deleted, the file is in the Recycle Bin, and the next track starts. With a Music publish "
            + "token set and a library track playing, the reply says the track is queued and how many are queued. "
            + "Without a token a library track is refused and keeps playing.",
        Description = "Moves the playing own music track to the Recycle Bin, or queues a library track for the maintainer, and skips it.",
        Protected = true,
        RefusalExample = "thumbs down this track",
        Commands =
        [
            new ToolCommandPhrase("thumbs down this track"),
            new ToolCommandPhrase("thumbs down"),
        ],
        Handler = (_, _) => Task.FromResult(
            thumbsDown is null
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
            + "maintainer of the music library. The Delete button on the Thumbs-down tracks row in Settings does the same.",
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
        ["previous"] = MusicAction.Previous,
    };

    /// <summary>Pause, resume, skip and step back for the ambient music. Levels and mutes stay out of the model's reach.</summary>
    private static ToolDefinition ManageMusic(Func<MusicAction, string>? music) => new()
    {
        Name = ManageMusicTool,
        Exercise =
            "With ambient music playing, say \"pause the music\" and it holds on the track. \"Resume "
            + "the music\" restarts it, \"next track\" starts another track from the same folder and "
            + "\"previous track\" plays the one before the current track. The reply says what the music did.",
        Description = "Pauses, resumes or skips D47's ambient music. Pause holds the track where it is; next "
                      + "starts another track from the same folder; previous plays the track before the current one.",
        Parameters =
        [
            ToolParameter.OneOf("action", "What to do to the music.", ["pause", "resume", "next", "previous"], required: true),
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
            Phrase("previous track", "previous"),
        ],
        Handler = (arguments, _) =>
        {
            if (!arguments.TryGetString("action", out var said) || !Actions.TryGetValue(said, out var action))
            {
                return Task.FromResult(ToolResult.Error("The action is pause, resume, next or previous."));
            }

            return Task.FromResult(music is null
                ? ToolResult.Error("Ambient music is not available.")
                : ToolResult.Ok(music(action)));
        },
    };

    public const string NowPlayingKey = "audio.music.now";

    public const string MusicLibraryKey = "audio.music.library";

    public const string NextTrackKey = "audio.music.next";

    public const string PreviousTrackKey = "audio.music.previous";

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

        yield return new SettingRow
        {
            Key = PreviousTrackKey,
            Exercise =
                ShowMusic + "find Previous track and press Previous while music plays: the track before the "
                + "current one in the folder's shuffle starts and Now playing shows its file name. On the "
                + "first track of a shuffle the same track starts again. The button is greyed out when nothing "
                + "plays or the channel is muted. Saying \"previous track\" does the same. Needs ambient music playing.",
            Advanced = true,
            Label = "Previous track",
            Help = "Plays the track before this one in the folder's shuffle.",
            Kind = SettingKind.Info,
            Group = MusicGroup,
            GroupHelp = what,
            DocsAnchor = "the-six-categories",
            PressLabel = "Previous",
            PressEnabled = s => CanControl(musicState(), s),
            Press = () => music(MusicAction.Previous),
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

    /// <summary>The whole account of the Commander's own folder in one string, for <c>get_setting</c>; the page draws <see cref="OwnAudioRows"/> instead.</summary>
    private static SettingRow DropsRow(Func<string> drops) => new()
    {
        Key = DropsKey,
        Advanced = true,
        DrawnElsewhere = true,
        Label = "Your own audio",
        Help = OwnAudioHelp,
        Kind = SettingKind.Info,
        Group = "Your own audio",
        DocsAnchor = "your-own-sounds",
        Binding = new SettingBinding { Read = _ => drops() },
    };

    private const string OwnAudioHelp =
        "Drop .mp3, .m4a, .aac, .wma, .flac or .wav files into data\\audio, beside d47.exe: cues/<state> "
        + "replaces a sound cue, beds/<name> adds a thinking bed, and music/<situation>/ is "
        + "ambience. Any sample rate and channel count. They are picked up without a restart.";

    /// <summary>The key of the Music status row, which opens the page.</summary>
    public const string OwnMusicKey = "audio.drops.music";

    /// <summary>The key of the Audio folder row, which opens the folder.</summary>
    public const string OwnFolderKey = "audio.drops.folder";

    public const string OwnCuesKey = "audio.drops.cues";

    public const string OwnBedsKey = "audio.drops.beds";

    private static IEnumerable<SettingRow> OwnAudioRows(Func<OwnAudioReport> report, Action? openFolder)
    {
        SettingRow Status(string key, string label, Func<string> read) => new()
        {
            Key = key,
            Advanced = true,
            Label = label,
            Help = OwnAudioHelp,
            Kind = SettingKind.Info,
            Group = "Your own audio",
            DocsAnchor = "your-own-sounds",
            Binding = new SettingBinding { Read = _ => read() },
        };

        string WithHint(string line, string? hint) => hint is null ? line : line + " " + hint;

        yield return Status(OwnMusicKey, "Music", () => report().MusicLine());
        yield return Status(OwnCuesKey, "Sound cues", () => WithHint(report().CuesLine(), report().CuesHint()));
        yield return Status(OwnBedsKey, "Thinking beds", () => WithHint(report().BedsLine(), report().BedsHint()));

        yield return Status(OwnFolderKey, "Audio folder", () => @"data\audio") with
        {
            PressLabel = openFolder is null ? null : "Open audio folder",
            Press = openFolder,
        };
    }

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
