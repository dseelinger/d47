using D47.Core.Conversation;
using D47.Core.Viewing;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Watching the videos on Media › Movies by voice.</summary>
public static class WatchCapability
{
    public const string Id = "watch";

    public const string WatchVideoTool = "watch_video";

    public const string PauseVideoTool = "pause_video";

    public const string ResumeVideoTool = "resume_video";

    public const string SkipVideoTool = "skip_video";

    public const string AddToWatchListTool = "add_to_watch_list";

    public const string WatchPhrase = "watch ";

    /// <summary>"Watch" and a title, one per title in Your list or Carry on watching.</summary>
    public static IEnumerable<DynamicCommand> WatchPhrases(WatchDesk? desk)
    {
        if (desk is null)
        {
            yield break;
        }

        foreach (var title in desk.Known())
        {
            yield return new DynamicCommand(
                WatchPhrase + title.Title,
                Id,
                WatchVideoTool,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["title"] = title.Title });
        }
    }

    /// <param name="desk">The desk, or null where nothing plays video.</param>
    /// <param name="storyPlaybackOpen">Whether a story playback is open, which takes bare "play" and "pause" first.</param>
    /// <param name="takesBare">Whether a video takes bare "play" and "pause" over a book, a podcast and the music.</param>
    /// <param name="takesSkip">Whether a video takes "back thirty seconds" and "forward thirty seconds" over a podcast.</param>
    public static CapabilityDescriptor Create(
        Func<WatchDesk?> desk,
        Func<bool>? storyPlaybackOpen = null,
        Func<bool>? takesBare = null,
        Func<bool>? takesSkip = null)
    {
        bool Bare() => storyPlaybackOpen?.Invoke() != true && takesBare?.Invoke() == true;

        bool Skips() => takesSkip?.Invoke() ?? desk()?.Active == true;

        return new CapabilityDescriptor
        {
            Id = Id,
            Group = "Voice",
            Name = "Watch",
            Summary = "Watch videos from Your list and the movie sources, pause and carry on, and skip within a video.",
            Examples = ["watch Big Buck Bunny", "pause the video", "carry on watching", "back thirty seconds"],
            Display = new CapabilityDisplay { PanelTitle = "Watch", Order = 92, ShowOnPanel = false },
            Tools =
            [
                Tool(
                    WatchVideoTool,
                    "Plays a video by title, looking in Your list first and then in the ticked movie sources.",
                    "Say \"watch\" followed by a title, such as \"watch Big Buck Bunny\": the video starts from where it was left and the reply is \"Watching Big Buck Bunny.\" A title in neither Your list nor the ticked sources is answered with that.",
                    desk,
                    (videos, arguments, token) => videos.Watch(arguments.Text("title"), token),
                    [],
                    [ToolParameter.Text("title", "The title of the video, or part of it.", required: true)]),
                Tool(
                    PauseVideoTool,
                    "Pauses the video that is playing.",
                    "While a video plays, say \"pause the video\" or \"pause\": the reply is \"Paused.\" and the picture stops. With none playing the reply is \"No video is playing.\"",
                    desk,
                    (videos, _, _) => Task.FromResult(videos.Pause()),
                    [Phrase("pause the video"), Phrase("pause video"), Phrase("pause") with { When = Bare }]),
                Tool(
                    ResumeVideoTool,
                    "Plays the paused video on, or with carry_on set the newest title in Carry on watching, or tries a stopped video again.",
                    "With a video paused, say \"carry on\" or \"play\": the reply is \"Resumed.\" Say \"carry on watching\" to play the newest title in Carry on watching from where it was left, or to try a video that stopped again.",
                    desk,
                    (videos, arguments, _) => Task.FromResult(arguments.Boolean("carry_on") == true ? videos.CarryOn() : videos.Resume()),
                    [
                        Phrase("carry on"),
                        Phrase("resume the video"),
                        Phrase("resume video"),
                        Phrase("play") with { When = Bare },
                        new ToolCommandPhrase("carry on watching", Arguments(("carry_on", "true"))),
                    ],
                    [ToolParameter.Flag("carry_on", "True to play the newest title in Carry on watching, or try a stopped video again. Optional.")]),
                Tool(
                    SkipVideoTool,
                    "Moves the video back or forward by a number of seconds.",
                    "While a video plays or is paused, say \"back thirty seconds\" or \"forward thirty seconds\": the picture moves and the reply is \"Back 30 seconds.\" or \"Forward 30 seconds.\"",
                    desk,
                    (videos, arguments, _) => Task.FromResult(videos.Skip(arguments.Int32("seconds") ?? 0)),
                    [
                        Skip("back thirty seconds", -30, Skips),
                        Skip("skip back thirty seconds", -30, Skips),
                        Skip("forward thirty seconds", 30, Skips),
                        Skip("skip forward thirty seconds", 30, Skips),
                    ],
                    [ToolParameter.Integer("seconds", "Seconds to move: negative for back, positive for forward.", required: true)]),
                Tool(
                    AddToWatchListTool,
                    "Adds a video to Your list. With no title it adds the video the player is on.",
                    "Say \"add this to my watch list\" while a video is open: the reply is \"Added\" and its title, and it appears in Your list on Media › Movies. A title already there is answered with that.",
                    desk,
                    (videos, arguments, token) => videos.AddToList(arguments.Text("title"), token),
                    [
                        Phrase("add this to my watch list"),
                        Phrase("add this to my watchlist"),
                        Phrase("add to my watch list"),
                        Phrase("add to watch list"),
                    ],
                    [ToolParameter.Text("title", "The title of the video, or part of it. Optional.")]),
            ],
        };
    }

    private static ToolCommandPhrase Phrase(string phrase) => new(phrase, Arguments());

    private static ToolCommandPhrase Skip(string phrase, int seconds, Func<bool> when) =>
        new(phrase, Arguments(("seconds", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)))) { When = when };

    private static Dictionary<string, string> Arguments(params (string Name, string Value)[] values) =>
        values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);

    private static ToolDefinition Tool(
        string name,
        string description,
        string exercise,
        Func<WatchDesk?> desk,
        Func<WatchDesk, ToolArguments, CancellationToken, Task<string>> act,
        IReadOnlyList<ToolCommandPhrase> commands,
        IReadOnlyList<ToolParameter>? parameters = null) => new()
    {
        Name = name,
        Description = description,
        Exercise = exercise,
        Parameters = parameters ?? [],
        Commands = commands,
        Handler = async (arguments, token) =>
            desk() is { } videos
                ? ToolResult.Ok(await act(videos, arguments, token).ConfigureAwait(false))
                : ToolResult.Error("Video is not available."),
    };
}
