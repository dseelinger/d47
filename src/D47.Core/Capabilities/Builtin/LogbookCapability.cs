using D47.Core.Configuration;
using D47.Core.Logbook;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The Commander's log (Phase 33).</summary>
public static class LogbookCapability
{
    public const string Id = "logbook";

    public const string VoiceKey = "logbook.voice";

    public const string RangeKey = "logbook.range";

    public const string LengthKey = "logbook.length";

    /// <summary>The row that reads the folder back, and opens it.</summary>
    public const string StoreKey = "logbook.store";

    public static CapabilityDescriptor Create(LogbookBook? book) =>
        new()
        {
            Id = Id,
            Group = "Conversation",
            Name = "Commander's log",
            Summary = "Turn a session — or a week — into a readable log, written from the journal and nothing else.",
            Examples =
            [
                "write my commander's log",
                "write the log",
                "what logs have I written",
            ],

            // Phrases, never bare words. "log" alone would hijack every sentence a Commander says about d47's
            // own diagnostics, which live one folder away and are also called logs.
            Keywords =
            [
                new("write my commander's log", "estimate_log"),
                new("write up my session", "estimate_log"),
                new("what would a log cost", "estimate_log"),
            ],

            // The other thing d47 does with the journals rather than with the game — and last, because
            // nav_order is the registry index and inserting earlier shifts every page after it.
            Display = new CapabilityDisplay { PanelTitle = "Commander's log", Order = 15 },
            Settings = [VoiceRow(), RangeRow(), LengthRow(), StoreRow(book)],
            Tools =
            [
                new ToolDefinition
                {
                    Name = "estimate_log",
                    Description =
                        "Work out what a Commander's log would cover and what writing it would cost, and get "
                        + "ready to write it. Spends nothing.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "range",
                            Type = ToolParameterType.String,
                            Description = "What span to cover. Omit for whatever the settings say.",
                            AllowedValues = LogRanges.Ids,
                        },
                    ],
                    Protected = true,
                    Commands =
                    [
                        new ToolCommandPhrase("write my commander's log", new Dictionary<string, string>()),
                        new ToolCommandPhrase("write up my session", new Dictionary<string, string>()),
                        new ToolCommandPhrase("what would a log cost", new Dictionary<string, string>()),
                        new ToolCommandPhrase(
                            "write up my week",
                            new Dictionary<string, string> { ["range"] = "week" }),
                    ],
                    Handler = (arguments, _) => Task.FromResult(Estimate(book, arguments)),
                },

                new ToolDefinition
                {
                    Name = "write_log",
                    Description =
                        "Write the log that was just quoted, and save it. Refuses unless a quote has been "
                        + "given, because this is the one thing D47 does that costs real money on request.",
                    Protected = true,
                    Commands =
                    [
                        new ToolCommandPhrase("write the log", new Dictionary<string, string>()),
                        new ToolCommandPhrase("write it up", new Dictionary<string, string>()),
                        new ToolCommandPhrase("go ahead and write it", new Dictionary<string, string>()),
                    ],
                    Handler = async (_, cancellationToken) => await WriteAsync(book, cancellationToken)
                        .ConfigureAwait(false),
                },

                new ToolDefinition
                {
                    Name = "list_logs",
                    Description = "Read back which Commander's logs have been written, and where they are.",
                    Protected = true,
                    Commands =
                    [
                        new ToolCommandPhrase("what logs have I written", new Dictionary<string, string>()),
                        new ToolCommandPhrase("show me my logs", new Dictionary<string, string>()),
                    ],
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(Summarise(book))),
                },
            ],
        };

    /// <summary>The line the panel row reads.</summary>
    public static string Summarise(LogbookBook? book) =>
        book?.Describe() ?? "D47 is not set up to write logs in this configuration.";

    private static ToolResult Estimate(LogbookBook? book, ToolArguments arguments)
    {
        if (book is null)
        {
            return ToolResult.Error("I am not set up to write logs in this configuration.");
        }

        arguments.TryGetString("range", out var range);
        book.Estimate(range, from: null, to: null, out var message);

        return ToolResult.Ok(message);
    }

    private static async Task<ToolResult> WriteAsync(LogbookBook? book, CancellationToken cancellationToken)
    {
        if (book is null)
        {
            return ToolResult.Error("I am not set up to write logs in this configuration.");
        }

        var outcome = await book.WriteAsync(cancellationToken).ConfigureAwait(false);

        return outcome.Ok ? ToolResult.Ok(outcome.Message) : ToolResult.Error(outcome.Message);
    }

    /// <summary>Whose log it is.</summary>
    private static SettingRow VoiceRow() => new()
    {
        Key = VoiceKey,
        Advanced = true,
        Label = "Whose log it is",
        Help =
            "Your own account in the first person, or D47 writing about you in the personality you have "
            + "chosen. With personality switched off, D47 writes plainly rather than writing as somebody else.",
        Kind = SettingKind.Choice,
        Choices = LogVoices.Ids,
        ChoiceLabel = LogVoices.LabelOf,
        DefaultDisplay = "You write it, in your own words",
        DocsAnchor = "whose-log-it-is",
        Protected = true,
        Commands =
        [
            new SettingCommandPhrase("write my logs in my own voice", "first-person"),
            new SettingCommandPhrase("write my logs yourself", "ships-ai"),
            new SettingCommandPhrase("write my logs with your commentary", "first-person-with-commentary"),
        ],
        Binding = new SettingBinding
        {
            Read = settings => settings.Logbook.Voice,
            Write = (settings, value) => settings with
            {
                Logbook = settings.Logbook with { Voice = LogVoices.IdOf(LogVoices.Parse(value)) },
            },
        },
    };

    private static SettingRow RangeRow() => new()
    {
        Key = RangeKey,
        Advanced = true,
        Label = "What a log covers",
        Help =
            "The span a log covers when you have not said. Two exact dates are available from this panel; "
            + "a phrase always means one of these.",
        Kind = SettingKind.Choice,
        Choices = LogRanges.Ids,
        ChoiceLabel = LogRanges.LabelOf,
        DefaultDisplay = "the last session",
        DocsAnchor = "what-a-log-covers",
        Binding = new SettingBinding
        {
            Read = settings => settings.Logbook.Range,
            Write = (settings, value) => settings with
            {
                Logbook = settings.Logbook with { Range = LogRanges.IdOf(LogRanges.Parse(value)) },
            },
        },
    };

    private static SettingRow LengthRow() => new()
    {
        Key = LengthKey,
        Advanced = true,
        Label = "How long a log runs",
        Help =
            "Longer logs cost more, and the estimate says how much before anything is written. This is most "
            + "of what that figure is pricing.",
        Kind = SettingKind.Choice,
        Choices = LogLengths.Ids,
        ChoiceLabel = LogLengths.LabelOf,
        DefaultDisplay = "standard — about a page",
        DocsAnchor = "it-costs-money-and-says-so-first",
        Binding = new SettingBinding
        {
            Read = settings => settings.Logbook.Length,
            Write = (settings, value) => settings with
            {
                Logbook = settings.Logbook with { Length = LogLengths.IdOf(LogLengths.Parse(value)) },
            },
        },
    };

    /// <summary>The folder, and the button that opens the window over it.</summary>
    private static SettingRow StoreRow(LogbookBook? book) => new()
    {
        Key = StoreKey,
        Advanced = true,
        Label = "Your logs",
        Help =
            "Logs are written as plain markdown beside D47, in data/commander-log. They are yours — D47 "
            + "never overwrites one, and nothing is written until you ask and have seen what it costs.",
        Kind = SettingKind.Info,
        DocsAnchor = "writing-one",
        Binding = new SettingBinding { Read = _ => Summarise(book) },
    };
}
