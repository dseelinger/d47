using System.Globalization;
using D47.Core.Configuration;
using D47.Core.Podcasts;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Playing the podcasts the Commander added, finding more, and the feed list.</summary>
public static class PodcastCapability
{
    public const string Id = "podcasts";

    public const string ManagePodcastTool = "manage_podcast";

    public const string ListPodcastsTool = "list_podcasts";

    public const string FindPodcastsTool = "find_podcasts";

    public const string AddPodcastTool = "add_podcast";

    public const string FeedsKey = "podcasts.feeds";

    /// <summary>The most results a search keeps, and so the most that can be added by number.</summary>
    public const int MostResults = SpokenList.Limit;

    private static readonly string[] ActionNames =
        ["play", "pause", "resume", "stop", "next", "skip_back", "skip_forward", "refresh"];

    private static readonly string[] Numbers = ["one", "two", "three"];

    /// <param name="desk">The desk, or null where nothing plays podcasts.</param>
    /// <param name="storyPlaybackOpen">Whether a story playback is open, which takes bare "play" and "pause" first.</param>
    public static CapabilityDescriptor Create(Func<PodcastDesk?> desk, Func<bool>? storyPlaybackOpen = null)
    {
        var gate = new Lock();
        IReadOnlyList<PodcastSearchResult> found = [];

        return new CapabilityDescriptor
        {
            Id = Id,
            Group = "Voice",
            Name = "Podcasts",
            Summary = "Play the podcasts you add, pick up where you left off, and find more shows.",
            Examples = ["play the podcast", "pause the podcast", "next episode", "skip back thirty seconds"],
            Display = new CapabilityDisplay { PanelTitle = "Podcasts", Order = 97, ShowOnPanel = false },
            Tools =
            [
                ManagePodcast(desk, storyPlaybackOpen),
                ListPodcasts(desk),
                FindPodcasts(
                    desk,
                    results =>
                    {
                        lock (gate)
                        {
                            found = results;
                        }
                    }),
                AddPodcast(
                    desk,
                    number =>
                    {
                        lock (gate)
                        {
                            return number >= 1 && number <= found.Count ? found[number - 1] : null;
                        }
                    }),
            ],
            Settings = [FeedsRow()],
        };
    }

    private static ToolCommandPhrase Phrase(string phrase, string action) =>
        new(phrase, new Dictionary<string, string>(StringComparer.Ordinal) { ["action"] = action });

    private static ToolDefinition ManagePodcast(Func<PodcastDesk?> desk, Func<bool>? storyPlaybackOpen)
    {
        bool Bare() => storyPlaybackOpen?.Invoke() != true && desk()?.Active == true;

        return new ToolDefinition
        {
            Name = ManagePodcastTool,
            Exercise =
                "With a podcast feed added, say \"play the podcast\": the newest unfinished episode starts from where "
                + "you left it and the ambient music stands aside. Say \"pause the podcast\" or \"pause\", then \"resume "
                + "the podcast\" or \"play\"; \"skip back thirty seconds\" and \"skip forward thirty seconds\" move "
                + "within the episode; \"next episode\" plays the next unfinished episode of the same show; \"stop the "
                + "podcast\" ends it and the music returns. \"Refresh podcasts\" fetches every feed again. With "
                + "nothing playing, \"next episode\" answers that no podcast is playing.",
            Description =
                "Plays, pauses, resumes or stops a podcast episode, skips 30 seconds back or forward, plays the next "
                + "episode of the same show, or refreshes the added feeds. Play with a show plays that show's newest "
                + "unfinished episode; without one it carries on the current episode or plays the newest unfinished "
                + "episode of any show. Each episode resumes where it was left.",
            Parameters =
            [
                ToolParameter.OneOf("action", "What to do.", ActionNames, required: true),
                ToolParameter.Text("show", "With play: the show title, or part of it. Optional."),
            ],
            Commands =
            [
                Phrase("play the podcast", "play"),
                Phrase("play podcast", "play"),
                Phrase("pause the podcast", "pause"),
                Phrase("pause podcast", "pause"),
                Phrase("resume the podcast", "resume"),
                Phrase("resume podcast", "resume"),
                Phrase("stop the podcast", "stop"),
                Phrase("stop podcast", "stop"),
                Phrase("next episode", "next"),
                Phrase("skip back thirty seconds", "skip_back"),
                Phrase("back thirty seconds", "skip_back"),
                Phrase("skip forward thirty seconds", "skip_forward"),
                Phrase("forward thirty seconds", "skip_forward"),
                Phrase("refresh podcasts", "refresh"),
                Phrase("play", "resume") with { When = Bare },
                Phrase("pause", "pause") with { When = Bare },
            ],
            Handler = async (arguments, _) =>
            {
                if (!arguments.TryGetString("action", out var said))
                {
                    return ToolResult.Error("The action is missing.");
                }

                if (desk() is not { } podcasts)
                {
                    return ToolResult.Error("Podcasts are not available.");
                }

                var reply = said.ToLowerInvariant() switch
                {
                    "play" => await podcasts.Play(arguments.Text("show")).ConfigureAwait(false),
                    "pause" => podcasts.Pause(),
                    "resume" => podcasts.Resume(),
                    "stop" => podcasts.Stop(),
                    "next" => await podcasts.Next().ConfigureAwait(false),
                    "skip_back" => podcasts.SkipBack(),
                    "skip_forward" => podcasts.SkipForward(),
                    "refresh" => await podcasts.Refresh().ConfigureAwait(false),
                    _ => null,
                };

                return reply is null
                    ? ToolResult.Error($"The action is one of {string.Join(", ", ActionNames)}.")
                    : ToolResult.Ok(reply);
            },
        };
    }

    private static ToolDefinition ListPodcasts(Func<PodcastDesk?> desk) => new()
    {
        Name = ListPodcastsTool,
        Exercise =
            "With feeds added, ask \"what podcasts do I have\". The reply names up to three shows and counts the rest; "
            + "the model is given every show with its unfinished episodes.",
        Description = "Lists the added podcast shows and each one's unfinished episodes, newest first.",
        Handler = (_, _) =>
        {
            if (desk() is not { } podcasts)
            {
                return Task.FromResult(ToolResult.Error("Podcasts are not available."));
            }

            var shows = podcasts.Shows();

            if (shows.Count == 0)
            {
                return Task.FromResult(ToolResult.Ok("No podcast shows are added, or none has been fetched yet."));
            }

            var content = string.Join(
                "\n",
                shows.Select(show => show.Unfinished.Count == 0
                    ? $"{show.Title}: every episode is finished."
                    : $"{show.Title}: {show.Unfinished.Count} unfinished, newest {show.Unfinished[0]}."));

            var spoken = shows.Count == 1
                ? $"One show: {shows[0].Title}."
                : $"{shows.Count} shows: {SpokenList.Names([.. shows.Select(show => show.Title)])}.";

            return Task.FromResult(ToolResult.Ok(content, spoken));
        },
    };

    private static ToolDefinition FindPodcasts(Func<PodcastDesk?> desk, Action<IReadOnlyList<PodcastSearchResult>> keep) => new()
    {
        Name = FindPodcastsTool,
        Exercise =
            "Ask \"find podcasts about deep space\". The reply numbers up to three shows from Apple's podcast directory. "
            + "Say \"add podcast one\" (or two, or three) to add that result.",
        Description =
            "Searches the podcast directory for shows and returns up to three, numbered one to three. The Commander "
            + "adds one by saying its number; the model cannot add a show.",
        Parameters = [ToolParameter.Text("words", "What to search for.", required: true)],
        Handler = async (arguments, _) =>
        {
            if (!arguments.TryGetString("words", out var words) || string.IsNullOrWhiteSpace(words))
            {
                return ToolResult.Error("Say what to search for.");
            }

            if (desk() is not { } podcasts)
            {
                return ToolResult.Error("Podcasts are not available.");
            }

            var outcome = await podcasts.Find(words).ConfigureAwait(false);

            if (outcome.Problem is { } problem)
            {
                return ToolResult.Error(problem);
            }

            var results = outcome.Results.Take(MostResults).ToList();
            keep(results);

            if (results.Count == 0)
            {
                return ToolResult.Ok($"The directory has no shows for {words.Trim()}.");
            }

            return ToolResult.Ok(string.Join(
                "\n",
                results.Select((result, index) =>
                    $"{index + 1}. {result.Title}"
                    + (result.Author.Length > 0 ? $" by {result.Author}" : string.Empty)
                    + (result.EpisodeCount is { } count ? $", {count} episodes" : string.Empty))));
        },
    };

    private static ToolDefinition AddPodcast(Func<PodcastDesk?> desk, Func<int, PodcastSearchResult?> result) => new()
    {
        Name = AddPodcastTool,
        Exercise =
            "After a search, say \"add podcast two\": the second result's feed is added and fetched, and the reply says "
            + "how many episodes it holds. With no search yet, or a number past the results, the reply says so.",
        Description = "Adds search result one, two or three from the last podcast search. Adding a feed adds a host the app contacts.",
        Parameters = [ToolParameter.Integer("result", "The number of the search result, one to three.", required: true)],
        Protected = true,
        RefusalExample = "add podcast one",
        Commands =
        [
            .. Numbers.Select((word, index) => new ToolCommandPhrase(
                $"add podcast {word}",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["result"] = (index + 1).ToString(CultureInfo.InvariantCulture),
                })),
        ],
        Handler = async (arguments, _) =>
        {
            if (!arguments.TryGetInt32("result", out var number))
            {
                return ToolResult.Error("Say which result to add, one to three.");
            }

            if (desk() is not { } podcasts)
            {
                return ToolResult.Error("Podcasts are not available.");
            }

            return result(number) is { } chosen
                ? ToolResult.Ok(await podcasts.Add(chosen).ConfigureAwait(false))
                : ToolResult.Error("Search for a show first, then add result one, two or three.");
        },
    };

    private static string? NotWebAddress(string? value) =>
        (value ?? string.Empty)
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(token => !Uri.TryCreate(token, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"));

    /// <summary>The feeds, as the addresses separated by spaces.</summary>
    private static SettingRow FeedsRow() => new()
    {
        Key = FeedsKey,
        Exercise =
            "With Show every setting on (at the top of the Settings page, or say \"show me every setting\"), open "
            + "Settings › Hearing and sound › Sounds and levels, group Podcasts, and find Podcast feeds. Paste a feed "
            + "address and save: the feed is fetched, and \"list podcasts\" names the show. An address that is not "
            + "http or https is refused.",
        Advanced = true,
        Label = "Podcast feeds",
        Help = "The addresses of the podcast feeds to play, separated by spaces. Saving fetches any feed that is new. "
               + "Only you can change this; the model cannot, because each feed adds a host D47 contacts.",
        Kind = SettingKind.Text,
        Group = "Podcasts",
        GroupHelp = "Episodes from the podcast feeds you add. Pauses while D47 speaks.",
        DocsAnchor = "the-feed-list",
        Protected = true,
        Refuses = (_, value) => NotWebAddress(value) is { } bad ? $"{bad} is not an http or https address." : null,
        Binding = new SettingBinding
        {
            Read = s => string.Join(' ', s.Podcasts.FeedUris().Select(feed => feed.AbsoluteUri)),
            Write = (s, v) => s with
            {
                Podcasts = s.Podcasts with
                {
                    Feeds =
                    [
                        .. (v ?? string.Empty)
                            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                            .Distinct(StringComparer.Ordinal),
                    ],
                },
            },
        },
    };
}
