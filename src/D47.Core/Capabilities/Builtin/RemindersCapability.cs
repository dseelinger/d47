using D47.Core.Knowledge;
using D47.Core.Reminders;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Reminders the journal fires, set and cancelled by the Commander's own words (#643).</summary>
public static class RemindersCapability
{
    public const string Id = "reminders";

    public const string SetTool = "set_journal_reminder";

    public const string ListTool = "get_journal_reminders";

    public const string CancelTool = "cancel_journal_reminder";

    public const string AcknowledgeTool = "acknowledge_journal_reminder";

    public const string SnoozeTool = "snooze_journal_reminder";

    private const string SameTrigger = "same_trigger";

    private static readonly string[] Acknowledgements = ["noted", "got it", "thanks"];

    private static readonly string[] SameTriggerPhrases = ["remind me next time"];

    private static readonly string[] NextSessionPhrases = ["remind me tomorrow", "remind me next session"];

    private const string NothingFired = "No reminder has just gone off.";

    private const string NoCommander = "Nobody is flying yet, so there is nobody to remind.";

    private const string NoStore = "Nothing here keeps reminders.";

    private static readonly IReadOnlyDictionary<string, JournalTrigger> TriggerNames =
        new Dictionary<string, JournalTrigger>(StringComparer.OrdinalIgnoreCase)
        {
            ["next_docking"] = JournalTrigger.NextDocking,
            ["docking_at"] = JournalTrigger.DockingAt,
            ["arrival_in"] = JournalTrigger.ArrivalIn,
            ["own_carrier"] = JournalTrigger.OwnCarrier,
            ["hold_empty"] = JournalTrigger.HoldEmpty,
            ["hold_full"] = JournalTrigger.HoldFull,
            ["material_full"] = JournalTrigger.MaterialFull,
            ["next_session"] = JournalTrigger.NextSession,
        };

    /// <summary>The tool's name for a trigger.</summary>
    public static string NameOf(JournalTrigger trigger) => TriggerNames.First(pair => pair.Value == trigger).Key;

    /// <summary>The arguments <see cref="SetTool"/> takes for one reading of the Commander's words.</summary>
    public static ToolArguments ArgumentsFor(JournalReminderReading.Set reading)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sentence"] = reading.Sentence,
            ["trigger"] = NameOf(reading.Trigger),
        };

        if (reading.Argument is { } argument)
        {
            values["argument"] = argument;
        }

        return new ToolArguments(values);
    }

    public static CapabilityDescriptor Create(
        JournalReminderStore? store,
        Func<string> frontierId,
        Func<DateTimeOffset> now) => new()
    {
        Id = Id,
        Group = "Interface",
        Name = "Journal reminders",
        Summary = "Reminders in your own words, said when the game reaches a moment.",
        Examples =
        [
            "remind me to buy limpets when I next dock",
            "when I arrive in Sol, remind me to sell data",
            "what reminders do I have",
            "cancel the reminder to buy limpets",
        ],
        Display = new CapabilityDisplay { PanelTitle = "Journal reminders", Order = 44 },
        Tools =
        [
            new ToolDefinition
            {
                Name = SetTool,
                Description =
                    "Store a reminder in the Commander's own words, said once when the journal reaches the "
                    + "trigger. The Commander's own act: reached only by saying \"remind me to\", not offered "
                    + "to the model, and refused if it asks.",
                Protected = true,
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "sentence",
                        Type = ToolParameterType.String,
                        Description = "What to say when it fires, as the Commander said it.",
                        Required = true,
                    },
                    new ToolParameter
                    {
                        Name = "trigger",
                        Type = ToolParameterType.String,
                        Description = "The moment in the game it waits for.",
                        Required = true,
                        AllowedValues = [.. TriggerNames.Keys],
                    },
                    new ToolParameter
                    {
                        Name = "argument",
                        Type = ToolParameterType.String,
                        Description = "The station, system or material the trigger names.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(Set(store, frontierId(), now(), arguments)),
            },

            new ToolDefinition
            {
                Name = ListTool,
                Description = "List the journal reminders the Commander has armed, each with the moment it waits for.",
                Commands =
                [
                    new ToolCommandPhrase("what reminders do I have", new Dictionary<string, string>(StringComparer.Ordinal)),
                    new ToolCommandPhrase("what are my reminders", new Dictionary<string, string>(StringComparer.Ordinal)),
                    new ToolCommandPhrase("list my reminders", new Dictionary<string, string>(StringComparer.Ordinal)),
                ],
                Handler = (_, _) => Task.FromResult(List(store, frontierId())),
            },

            new ToolDefinition
            {
                Name = CancelTool,
                Description =
                    "Cancel an armed journal reminder by words from its sentence. The Commander's own act: not "
                    + "offered to the model, and refused if it asks.",
                Protected = true,
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "words",
                        Type = ToolParameterType.String,
                        Description = "Words from the sentence of the reminder to cancel.",
                        Required = true,
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(Cancel(store, frontierId(), arguments)),
            },

            new ToolDefinition
            {
                Name = AcknowledgeTool,
                Description =
                    "Remove the journal reminder that has just gone off. The Commander's own answer: reached only "
                    + "by saying \"noted\", not offered to the model, and refused if it asks.",
                Protected = true,
                Commands = [.. Answers(store, frontierId, Acknowledgements, Nothing)],
                Handler = (_, _) => Task.FromResult(Acknowledge(store, frontierId())),
            },

            new ToolDefinition
            {
                Name = SnoozeTool,
                Description =
                    "Arm the journal reminder that has just gone off again, on its own trigger or at the next "
                    + "session. The Commander's own answer: not offered to the model, and refused if it asks.",
                Protected = true,
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "until",
                        Type = ToolParameterType.String,
                        Description = "When it goes off again: the same moment, or the next session.",
                        AllowedValues = [SameTrigger, "next_session"],
                    },
                ],
                Commands =
                [
                    .. Answers(store, frontierId, SameTriggerPhrases, Until(SameTrigger)),
                    .. Answers(store, frontierId, NextSessionPhrases, Until("next_session")),
                ],
                Handler = (arguments, _) => Task.FromResult(Snooze(store, frontierId(), now(), arguments)),
            },
        ],
    };

    private static readonly IReadOnlyDictionary<string, string> Nothing = new Dictionary<string, string>(StringComparer.Ordinal);

    private static Dictionary<string, string> Until(string until) => new(StringComparer.Ordinal) { ["until"] = until };

    /// <summary>Phrases that are answers only while a fired reminder is waiting.</summary>
    private static IEnumerable<ToolCommandPhrase> Answers(
        JournalReminderStore? store,
        Func<string> frontierId,
        string[] phrases,
        IReadOnlyDictionary<string, string> arguments) =>
        phrases.Select(phrase => new ToolCommandPhrase(phrase, arguments)
        {
            When = () => store?.LastFired(frontierId()) is not null,
        });

    /// <summary>When a reminder fires, addressed to the Commander: "when you next dock".</summary>
    public static string When(JournalTrigger trigger, string? argument) => trigger switch
    {
        JournalTrigger.NextDocking => "when you next dock",
        JournalTrigger.DockingAt => $"when you dock at {argument}",
        JournalTrigger.ArrivalIn => $"when you arrive in {argument}",
        JournalTrigger.OwnCarrier => "when you're back at your carrier",
        JournalTrigger.HoldEmpty => "when your hold is empty",
        JournalTrigger.HoldFull => "when your hold is full",
        JournalTrigger.MaterialFull => $"when your {MaterialName(argument)} is full",
        JournalTrigger.NextSession => "at the start of your next session",
        _ => "at a moment in the game",
    };

    /// <summary>"to buy limpets when you next dock".</summary>
    public static string Describe(JournalReminder reminder) =>
        $"{Lead(reminder.Sentence)} {When(reminder.Trigger, reminder.Argument)}";

    private static string Lead(string sentence) =>
        sentence.StartsWith("about ", StringComparison.OrdinalIgnoreCase)
        || sentence.StartsWith("of ", StringComparison.OrdinalIgnoreCase)
            ? sentence
            : $"to {sentence}";

    private static string MaterialName(string? symbol) =>
        MaterialCatalogue.Find(symbol)?.Name ?? symbol ?? "material";

    private static ToolResult Set(JournalReminderStore? store, string frontierId, DateTimeOffset now, ToolArguments arguments)
    {
        if (store is null)
        {
            return ToolResult.Error(NoStore);
        }

        if (string.IsNullOrWhiteSpace(frontierId))
        {
            return ToolResult.Error(NoCommander);
        }

        if (!arguments.TryGetString("sentence", out var sentence) || string.IsNullOrWhiteSpace(sentence))
        {
            return ToolResult.Error("A reminder needs something to say.");
        }

        if (!arguments.TryGetString("trigger", out var name) || !TriggerNames.TryGetValue(name, out var trigger))
        {
            return ToolResult.Error(JournalReminderPhrase.NoTrigger);
        }

        arguments.TryGetString("argument", out var argument);
        argument = string.IsNullOrWhiteSpace(argument) ? null : argument.Trim();

        if (trigger == JournalTrigger.MaterialFull)
        {
            if (MaterialCatalogue.Find(argument) is not { Ledger: MaterialLedger.Material, Grade: not null } material)
            {
                return ToolResult.Error($"I don't know a material called {argument}, so I can't tell when it's full.");
            }

            argument = material.Symbol;
        }

        if (sentence.Trim().Length > JournalReminderStore.MaxSentenceLength)
        {
            return ToolResult.Error("That is longer than a reminder can hold. Say it more briefly.");
        }

        var reminder = new JournalReminder(Guid.NewGuid().ToString("N"), sentence.Trim(), trigger, argument) { Set = now };

        if (store.For(frontierId).Count >= JournalReminderStore.MaxReminders)
        {
            return ToolResult.Error(
                $"You already have {JournalReminderStore.MaxReminders} reminders. Cancel one before setting another.");
        }

        return store.Add(frontierId, reminder)
            ? ToolResult.Ok($"I'll remind you {Describe(reminder)}.")
            : ToolResult.Error("I couldn't keep that reminder. The reminder file may need fixing by hand.");
    }

    private static ToolResult List(JournalReminderStore? store, string frontierId)
    {
        if (store is null)
        {
            return ToolResult.Error(NoStore);
        }

        var armed = store.For(frontierId).Where(reminder => reminder.State == JournalReminderState.Armed).ToArray();

        if (armed.Length == 0)
        {
            return ToolResult.Ok("No reminders are set. Say \"remind me to\" and a moment, such as \"when I next dock\".");
        }

        var described = armed.Select(Describe).ToArray();
        var count = armed.Length == 1 ? "One reminder is set" : $"{armed.Length} reminders are set";

        return ToolResult.Ok(
            $"{count}: {string.Join("; ", described)}.",
            $"{count}: {SpokenList.Names(described)}.");
    }

    private static ToolResult Acknowledge(JournalReminderStore? store, string frontierId)
    {
        if (store is null)
        {
            return ToolResult.Error(NoStore);
        }

        if (store.LastFired(frontierId) is not { } fired)
        {
            return ToolResult.Error(NothingFired);
        }

        return store.Remove(frontierId, fired.Id)
            ? ToolResult.Ok("Noted.")
            : ToolResult.Error("I couldn't clear that reminder. The reminder file may need fixing by hand.");
    }

    private static ToolResult Snooze(JournalReminderStore? store, string frontierId, DateTimeOffset now, ToolArguments arguments)
    {
        if (store is null)
        {
            return ToolResult.Error(NoStore);
        }

        if (store.LastFired(frontierId) is not { } fired)
        {
            return ToolResult.Error(NothingFired);
        }

        var nextSession = arguments.TryGetString("until", out var until)
            && string.Equals(until, "next_session", StringComparison.OrdinalIgnoreCase);

        if (!store.Rearm(frontierId, fired.Id, now, nextSession ? JournalTrigger.NextSession : null))
        {
            return ToolResult.Error("I couldn't arm that reminder again. The reminder file may need fixing by hand.");
        }

        var armed = store.For(frontierId).First(reminder => reminder.Id == fired.Id);

        return ToolResult.Ok($"I'll remind you {Describe(armed)}.");
    }

    private static ToolResult Cancel(JournalReminderStore? store, string frontierId, ToolArguments arguments)
    {
        if (store is null)
        {
            return ToolResult.Error(NoStore);
        }

        if (!arguments.TryGetString("words", out var words) || Words(words) is not { Count: > 0 } wanted)
        {
            return ToolResult.Error("Which reminder? Say some of its words.");
        }

        var matching = store.For(frontierId)
            .Where(reminder => reminder.State == JournalReminderState.Armed && wanted.IsSubsetOf(Words(reminder.Sentence)))
            .ToArray();

        switch (matching)
        {
            case []:
                return ToolResult.Error($"No reminder matches \"{words.Trim()}\".");

            case [var only]:
                return store.Remove(frontierId, only.Id)
                    ? ToolResult.Ok($"Cancelled the reminder {Describe(only)}.")
                    : ToolResult.Error("I couldn't cancel that reminder. The reminder file may need fixing by hand.");

            default:
                return ToolResult.Error(
                    $"That matches {matching.Length} reminders: {SpokenList.Names([.. matching.Select(Describe)])}. "
                    + "Say more of the one to cancel.");
        }
    }

    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase) { "a", "an", "the", "to", "my", "about", "of" };

    private static HashSet<string> Words(string text) =>
    [
        .. text.Split([' ', ',', '.', ';', ':', '!', '?'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.ToLowerInvariant())
            .Where(word => !Ignored.Contains(word)),
    ];
}
