using System.Globalization;
using System.Text.RegularExpressions;
using D47.Core.Audio;
using D47.Core.CaptainsLog;
using D47.Core.Journal;
using D47.Core.Utilities;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Spoken log entries, kept as heard with the place and ship they were said in.</summary>
public static partial class CaptainsLogCapability
{
    public const string Id = "captains-log";

    public const string AddTool = "add_log_entry";

    public const string FindTool = "find_log_entries";

    public const string DeleteTool = "delete_log_entry";

    public const string ExportTool = "export_log_entries";

    /// <summary>The most entries read aloud in one answer.</summary>
    public const int ReadAloud = 3;

    /// <summary>What the exchange is recorded as, in place of the entry's words.</summary>
    public const string Asked = "captain's log entry";

    public const string NoCommander = "Nobody is flying, so there is nobody to log for.";

    public static CapabilityDescriptor Create(
        CaptainsLogStore? store,
        Func<string> frontierId,
        Func<CommanderGameState?> commander,
        Func<GameStatus>? status,
        Func<DateTimeOffset> now) => new()
    {
        Id = Id,
        Group = "Conversation",
        Name = "Captain's log",
        Summary = "Say \"captain's log\" and an entry, and it is kept word for word with where you were.",
        Examples = ["captain's log, the Guardian site is two kilometres north of the landing pad", "log entry: fuel is low"],
        Display = new CapabilityDisplay { PanelTitle = "Captain's log", Order = 45, ShowOnPanel = false },
        Tools =
        [
            new ToolDefinition
            {
                Name = AddTool,
                Exercise =
                    "Say \"captain's log\" followed by a sentence, such as \"captain's log, the survey went well\". "
                    + "D47 saves it with the system, body, station and ship you are at. The model cannot do this: it "
                    + "is refused if it asks.",
                Description =
                    "Save a log entry in the Commander's own words, with the system, body, station, ship and "
                    + "position known now. The Commander's own act: reached only by saying \"captain's log\", "
                    + "not offered to the model, and refused if it asks.",
                Protected = true,
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "text",
                        Type = ToolParameterType.String,
                        Description = "The entry, as the Commander said it.",
                        Required = true,
                    },
                ],
                Handler = (arguments, _) =>
                    Task.FromResult(Add(store, frontierId(), commander(), status?.Invoke(), now(), arguments)),
            },
            new ToolDefinition
            {
                Name = FindTool,
                Exercise =
                    "Say \"read my last log entry\". The reply reads back the newest entry, at most three in full. "
                    + "The model cannot do this: it is refused if it asks.",
                Description =
                    "Read the Commander's captain's log entries back, newest first, at most three in full. The "
                    + "Commander's own act: reached only by saying \"read my last log entry\" and the like, not "
                    + "offered to the model, and refused if it asks.",
                Protected = true,
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "span",
                        Type = ToolParameterType.String,
                        Description = "Which entries by time; all when omitted.",
                        AllowedValues = ["all", "last", "today", "yesterday", "week", "session"],
                    },
                    new ToolParameter
                    {
                        Name = "system",
                        Type = ToolParameterType.String,
                        Description = "Only entries made in this star system.",
                    },
                    new ToolParameter
                    {
                        Name = "words",
                        Type = ToolParameterType.String,
                        Description = "Only entries whose text holds every one of these words.",
                    },
                    new ToolParameter
                    {
                        Name = "count_only",
                        Type = ToolParameterType.Boolean,
                        Description = "Say how many entries match instead of reading them.",
                    },
                ],
                Handler = (arguments, _) =>
                    Task.FromResult(Find(store, frontierId(), commander(), now(), arguments)),
            },
            new ToolDefinition
            {
                Name = DeleteTool,
                Exercise =
                    "Say \"delete my last log entry\". The newest entry is deleted and the reply says so. The model "
                    + "cannot do this: it is refused if it asks.",
                Description =
                    "Delete the Commander's newest captain's log entry. The Commander's own act: reached only by "
                    + "saying \"delete my last log entry\", not offered to the model, and refused if it asks.",
                Protected = true,
                Parameters = [],
                Handler = (_, _) => Task.FromResult(DeleteLast(store, frontierId())),
            },
            new ToolDefinition
            {
                Name = ExportTool,
                Exercise =
                    "Say \"export my log entries\". D47 writes every entry to a Markdown file, oldest first, and "
                    + "says how many and where. The model cannot do this: it is refused if it asks.",
                Description =
                    "Write every captain's log entry of the Commander to a Markdown file, oldest first, and say how "
                    + "many and where. The Commander's own act: reached only by saying \"export my log entries\", "
                    + "not offered to the model, and refused if it asks.",
                Protected = true,
                Parameters = [],
                Handler = (_, _) => Task.FromResult(Export(store, frontierId(), now())),
            },
        ],
    };

    private static ToolResult Add(
        CaptainsLogStore? store,
        string frontierId,
        CommanderGameState? commander,
        GameStatus? status,
        DateTimeOffset now,
        ToolArguments arguments)
    {
        if (store is null)
        {
            return ToolResult.Error("Nothing here keeps a captain's log.");
        }

        if (string.IsNullOrWhiteSpace(frontierId))
        {
            return ToolResult.Error(NoCommander);
        }

        if (!arguments.TryGetString("text", out var text) || string.IsNullOrWhiteSpace(text))
        {
            return ToolResult.Error(CaptainsLogPhrase.NoEntry);
        }

        var location = commander?.Location;
        var ship = commander?.FlownShip;

        var entry = new CaptainsLogEntry(Guid.NewGuid().ToString("N"), text.Trim(), now.ToUniversalTime())
        {
            System = Known(location?.StarSystem),
            Body = Known(location?.Body) ?? Known(status?.BodyName),
            Station = location is { Docked: true } ? Known(location.StationName) : null,
            ShipType = Known(ship?.TypeName) ?? Known(ship?.Type),
            ShipName = Known(ship?.Name),
            Latitude = status is { HasPosition: true } ? status.Latitude : null,
            Longitude = status is { HasPosition: true } ? status.Longitude : null,
        };

        return store.Add(frontierId, entry)
            ? ToolResult.Ok("Logged.")
            : ToolResult.Error("I couldn't keep that entry. The log file may need fixing by hand.");
    }

    private static ToolResult Find(
        CaptainsLogStore? store,
        string frontierId,
        CommanderGameState? commander,
        DateTimeOffset now,
        ToolArguments arguments)
    {
        if (store is null)
        {
            return ToolResult.Error("Nothing here keeps a captain's log.");
        }

        var span = arguments.TryGetString("span", out var named) ? named.Trim().ToLowerInvariant() : "all";
        var today = now.ToUniversalTime().Date;
        DateTimeOffset? since = null;
        DateTimeOffset? before = null;

        switch (span)
        {
            case "today":
                since = new DateTimeOffset(today, TimeSpan.Zero);
                break;
            case "yesterday":
                since = new DateTimeOffset(today.AddDays(-1), TimeSpan.Zero);
                before = new DateTimeOffset(today, TimeSpan.Zero);
                break;
            case "week":
                since = new DateTimeOffset(today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), TimeSpan.Zero);
                break;
            case "session":
                if (commander?.CodexLedger.SessionStart is not { } start)
                {
                    return ToolResult.Error("I don't know when this session began.");
                }

                since = start;
                break;
        }

        var words = arguments.TryGetString("words", out var asked) ? Words(asked) : [];
        var system = arguments.TryGetString("system", out var place) && !string.IsNullOrWhiteSpace(place) ? place.Trim() : null;

        var matches = store.For(frontierId)
            .Where(entry => (since is null || entry.At >= since) && (before is null || entry.At < before))
            .Where(entry => system is null || string.Equals(entry.System, system, StringComparison.OrdinalIgnoreCase))
            .Where(entry => words.Count == 0 || Words(entry.Text).IsSupersetOf(words))
            .OrderByDescending(entry => entry.At)
            .ToList();

        if (arguments.TryGetBoolean("count_only", out var countOnly) && countOnly)
        {
            return ToolResult.Ok(matches.Count switch
            {
                0 => "You have no log entries.",
                1 => "You have one log entry.",
                _ => $"You have {Spell(matches.Count)} log entries.",
            });
        }

        if (matches.Count == 0)
        {
            return ToolResult.Ok("No log entries match.");
        }

        if (span == "last")
        {
            matches = [matches[0]];
        }

        var lines = matches.Take(ReadAloud).Select(Spoken).ToList();

        if (matches.Count > ReadAloud)
        {
            lines.Add($"and {Spell(matches.Count - ReadAloud)} more on the Captain's log page.");
        }

        return ToolResult.Ok(string.Join('\n', lines));
    }

    private static ToolResult Export(CaptainsLogStore? store, string frontierId, DateTimeOffset now)
    {
        if (store is null)
        {
            return ToolResult.Error("Nothing here keeps a captain's log.");
        }

        if (string.IsNullOrWhiteSpace(frontierId))
        {
            return ToolResult.Error(NoCommander);
        }

        var exported = store.Export(frontierId, now);

        if (exported.Count == 0)
        {
            return ToolResult.Ok("You have no log entries to export.");
        }

        return exported.Path is null
            ? ToolResult.Error("I couldn't write the export. Check that the data folder can be written to.")
            : ToolResult.Ok($"Exported {(exported.Count == 1 ? "one log entry" : $"{Spell(exported.Count)} log entries")} to {exported.Path}.");
    }

    private static ToolResult DeleteLast(CaptainsLogStore? store, string frontierId)
    {
        if (store is null)
        {
            return ToolResult.Error("Nothing here keeps a captain's log.");
        }

        if (store.For(frontierId).OrderByDescending(entry => entry.At).FirstOrDefault() is not { } newest)
        {
            return ToolResult.Error("You have no log entries to delete.");
        }

        var first = string.Join(' ', Whitespace().Replace(newest.Text, " ").Trim().Split(' ').Take(6));

        return store.Remove(frontierId, newest.Id)
            ? ToolResult.Ok($"Deleted the entry from {Dated(newest)}: {first}.")
            : ToolResult.Error("I couldn't delete that entry. The log file may need fixing by hand.");
    }

    /// <summary>What the exchange is recorded as for an answer to <see cref="FindTool"/>: how many entries were read, not their text.</summary>
    public static string AskedForRead(ToolResult answer)
    {
        var read = answer.IsError || !answer.Content.Contains(':', StringComparison.Ordinal)
            ? 0
            : answer.Content.Split('\n').Count(line => !line.StartsWith("and ", StringComparison.Ordinal));

        return read switch
        {
            0 => "read no captain's log entries",
            1 => "read one captain's log entry",
            _ => $"read {Spell(read)} captain's log entries",
        };
    }

    private static string Spoken(CaptainsLogEntry entry) =>
        $"{Dated(entry)}: {Whitespace().Replace(entry.Text, " ").Trim()}";

    private static string Dated(CaptainsLogEntry entry)
    {
        var galactic = GalacticTime.Galactic(entry.At.ToUniversalTime());
        var year = galactic.Year;
        var low = year % 100;
        var said = low switch
        {
            0 => $"{Spell(year / 100)} hundred",
            < 10 => $"{Spell(year / 100)} oh {Spell(low)}",
            _ => $"{Spell(year / 100)} {Spell(low)}",
        };
        var date = $"{galactic.ToString("MMMM", CultureInfo.InvariantCulture)} {Ordinals[galactic.Day - 1]} {said}";

        return string.IsNullOrWhiteSpace(entry.System) ? date : $"{date}, in {entry.System}";
    }

    private static string Spell(int number) => SpokenNumbers.Expand(number.ToString(CultureInfo.InvariantCulture));

    private static HashSet<string> Words(string text) =>
        [.. WordPattern().Matches(text).Select(match => match.Value.ToLowerInvariant())];

    private static readonly string[] Ordinals =
    [
        "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth",
        "eleventh", "twelfth", "thirteenth", "fourteenth", "fifteenth", "sixteenth", "seventeenth", "eighteenth",
        "nineteenth", "twentieth", "twenty-first", "twenty-second", "twenty-third", "twenty-fourth",
        "twenty-fifth", "twenty-sixth", "twenty-seventh", "twenty-eighth", "twenty-ninth", "thirtieth",
        "thirty-first",
    ];

    [GeneratedRegex(@"[\p{L}\p{N}']+")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private static string? Known(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
