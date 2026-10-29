using D47.Core.Configuration;
using D47.Core.Memory;

namespace D47.Core.Capabilities.Builtin;

/// <summary>What d47 remembers about the Commander (Phase 31).</summary>
public static class MemoryCapability
{
    public const string Id = "memory";

    public const string EnabledKey = "memory.enabled";

    public const string ExpiryKey = "memory.expiryDays";

    /// <summary>The store itself, as a disclosure with the way into it beside it.</summary>
    public const string StoreKey = "memory.store";

    /// <summary>Never, and the three the row offers. "0" is never, and it is a real choice.</summary>
    private static readonly string[] ExpiryChoices = ["0", "30", "90", "365"];

    public static CapabilityDescriptor Create(MemoryBook? book, Func<DateTimeOffset> now, SettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new CapabilityDescriptor
        {
            Id = Id,
            Group = "Conversation",
            Name = "Memory",
            Summary = "Keep facts about the Commander between sessions, and say where each one came from.",
            Examples =
            [
                "what do you remember about me",
                "remember that I hate mining",
                "forget that",
            ],

            // Phrases, never bare words — "remember" and "memory" both turn up in half of what a Commander
            // says to a companion.
            Keywords =
            [
                "what do you remember about me",
                "what do you know about me",
                "what have you written down about me",
            ],

            // Beside Persona, which is the other section about who is talking to whom, and above everything
            // about the game.
            Display = new CapabilityDisplay { PanelTitle = "Memory", Order = 13 },
            Settings = [EnabledRow(), ExpiryRow(), StoreRow(book)],
            Tools =
            [
                // Argument-free and first, so "what do you remember about me" reaches it through the keyword
                // router with no model in the path — and answers from the store rather than from the sample
                // the prompt is carrying.
                new ToolDefinition
                {
                    Name = "get_memories",
                    Exercise =
                        "Say \"what do you remember about me\". The reply reads back every entry, "
                        + "each marked as said by you, noticed by D47, or worked out by D47. The "
                        + "model cannot do this: it is refused if it asks.",
                    Description =
                        "Read back everything D47 has written down about the Commander, each line saying whether "
                        + "the Commander said it, D47 noticed it, or D47 worked it out for itself.",

                    // Protected for room rather than for safety, and that is worth saying plainly: there is
                    // nothing dangerous about reading this back.
                    Protected = true,
                    Commands =
                    [
                        new ToolCommandPhrase("what do you remember about me"),
                        new ToolCommandPhrase("what do you know about me"),
                    ],
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(
                        book is null
                            ? "I have nowhere to keep anything in this configuration."
                            : MemoryRecall.Describe(book.Mine))),
                },

                // The one advertised tool in the phase, and every character of it is measured against the 361
                // bytes the ceiling had left: one parameter, no tier, no tags, and a description written to
                // be short rather than to be thorough.
                new ToolDefinition
                {
                    Name = "remember_about_me",
                    Exercise =
                        "Say \"remember that I hate mining\". The reply is \"Written down as\" a key, "
                        + "\"unverified:\" and the fact, and \"what do you remember about me\" then lists "
                        + "it. With remembering switched off in Settings it is refused with that "
                        + "reason.",
                    Description =
                        "Write down one lasting fact or preference about the Commander, not what happened "
                        + "today. Stored as unverified.",
                    Parameters =
                    [
                        ToolParameter.Text("fact", "The fact, in one sentence.", required: true),
                    ],
                    Handler = (arguments, _) => Task.FromResult(
                        Remember(book, settings.Current.Memory, now(), arguments)),
                },

                new ToolDefinition
                {
                    Name = "forget_memory",
                    Exercise =
                        "No phrase or button calls this tool. The Memory dialog's Forget button "
                        + "removes the same entry, after which \"what do you remember about me\" no "
                        + "longer lists it. The model cannot call it: it is refused if it asks.",
                    Description = "Forget one thing D47 has written down about the Commander, by its key.",
                    Parameters =
                    [
                        ToolParameter.Text(
                            "key",
                            "The key of the entry to forget, as read back by get_memories.",
                            required: true),
                    ],

                    // Protected, and here it is safety rather than room.
                    Protected = true,
                    Handler = (arguments, _) => Task.FromResult(Forget(book, arguments)),
                },
            ],
        };
    }

    /// <summary>The line the window's own header reads, and the row above the button.</summary>
    public static string Summarise(MemoryBook? book) =>
        book?.Summarise() ?? "Nothing is keeping memories in this configuration.";

    /// <summary>How long entries live, from the setting.</summary>
    public static TimeSpan ExpiryOf(MemorySettings memory) =>
        memory.ExpiryDays <= 0 ? TimeSpan.Zero : TimeSpan.FromDays(memory.ExpiryDays);

    private static ToolResult Remember(
        MemoryBook? book,
        MemorySettings memory,
        DateTimeOffset now,
        ToolArguments arguments)
    {
        if (book is null)
        {
            return ToolResult.Error("I have nowhere to keep anything in this configuration.");
        }

        if (!memory.Enabled)
        {
            // Said rather than silently dropped.
            return ToolResult.Error("Remembering is switched off, so I am not writing anything down.");
        }

        if (!arguments.TryGetString("fact", out var fact) || string.IsNullOrWhiteSpace(fact))
        {
            return ToolResult.Error("A memory needs something in it.");
        }

        var entry = book.Remember(fact, MemoryArrival.Model, now);

        return ToolResult.Ok($"Written down as {entry.Key}, unverified: {entry.Fact}");
    }

    private static ToolResult Forget(MemoryBook? book, ToolArguments arguments)
    {
        if (book is null)
        {
            return ToolResult.Error("I have nowhere to keep anything in this configuration.");
        }

        if (!arguments.TryGetString("key", out var key) || string.IsNullOrWhiteSpace(key))
        {
            return ToolResult.Error("Which one? Every entry has a key.");
        }

        return book.Forget(key.Trim())
            ? ToolResult.Ok($"Forgotten: {key.Trim()}.")
            : ToolResult.Error($"Nothing here is keyed {key.Trim()}.");
    }

    private static SettingRow EnabledRow() => new()
    {
        Key = EnabledKey,
        Exercise =
            "Open Settings › You and your records › Memory, notes and debrief, group Memory and "
            + "debrief, and find Remember things about me (on by default). On, say \"remember that I "
            + "hate mining\": the tool answers \"Written down as\", a key, \"unverified:\" and the fact, "
            + "and \"what do you remember about me\" then lists it. Turn the row off, or say \"stop "
            + "remembering things about me\", and ask the same again: the tool answers \"Remembering "
            + "is switched off, so I am not writing anything down.\" and nothing is added; entries "
            + "already there are still listed. \"start remembering things about me\" turns it on. The "
            + "model cannot change the row.",
        Label = "Remember things about me",
        Help =
            "Keep facts between sessions, and send a few of the relevant ones with each question. "
            + "Off stops every new entry and stops any of it reaching the model — it does not erase "
            + "what is already there, which is its own action in [Privacy](privacy).",
        Kind = SettingKind.Toggle,
        DefaultDisplay = "on",
        DocsAnchor = "what-is-stored-and-what-is-not",

        // Protected.
        Protected = true,
        Commands =
        [
            new SettingCommandPhrase("stop remembering things about me", "false"),
            new SettingCommandPhrase("start remembering things about me", "true"),
        ],
        Binding = SettingBinding.Toggle(
            s => s.Memory.Enabled,
            (s, on) => s with { Memory = s.Memory with { Enabled = on } }),
    };

    private static SettingRow ExpiryRow() => new()
    {
        Key = ExpiryKey,
        Exercise =
            InterfaceCapability.AdvancedPrefix
            + "Settings › You and your records › Memory, notes and debrief, group Memory and "
            + "debrief, and find Forget after. The choices are Never, A month, Three months (the "
            + "default) and A year. Saying \"never forget anything about me\", \"forget things about "
            + "me after a month\", \"forget things about me after three months\" or \"forget things "
            + "about me after a year\" sets each one. To see it act, choose A month and close D47. "
            + "In memories.json in the data folder (Open data folder under Settings › Privacy and "
            + "this install › Updates and install) set the added date of an entry you told D47 "
            + "yourself to more than a month ago, and start D47: the entry is gone from \"what do "
            + "you remember about me\", and the panel reads \"I have forgotten something you told me, "
            + "because it was past its expiry:\" followed by the fact. Never expires nothing, and an "
            + "entry with no added date never expires.",
        Advanced = true,
        Label = "Forget after",
        Help =
            "How long a fact lasts before D47 drops it. Anything you told D47 yourself is said out "
            + "loud as it goes, so an expiry can never silently lose your own words.",
        Kind = SettingKind.Choice,
        Choices = ExpiryChoices,
        ChoiceLabel = value => value switch
        {
            "0" => "Never",
            "30" => "A month",
            "365" => "A year",
            _ => "Three months",
        },
        DefaultDisplay = "Three months",
        DocsAnchor = "forgetting",
        Protected = true,

        // One phrase per value, because a protected row is unreachable from the tool surface by design and a
        // closed value set with no phrases is a row a Commander flying in VR cannot reach at all.
        Commands =
        [
            new SettingCommandPhrase("never forget anything about me", "0"),
            new SettingCommandPhrase("forget things about me after a month", "30"),
            new SettingCommandPhrase("forget things about me after three months", "90"),
            new SettingCommandPhrase("forget things about me after a year", "365"),
        ],
        Binding = new SettingBinding
        {
            Read = s => s.Memory.ExpiryDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Write = (s, v) => s with
            {
                Memory = s.Memory with
                {
                    ExpiryDays = int.TryParse(v, out var days) && days >= 0 ? days : s.Memory.ExpiryDays,
                },
            },
        },
    };

    private static SettingRow StoreRow(MemoryBook? book) => new()
    {
        Key = StoreKey,
        Advanced = true,
        Label = "What D47 remembers",
        Help = "Everything written down, where it came from, and the one place a fact can be added by hand.",
        Kind = SettingKind.Info,
        DocsAnchor = "where-it-lives",
        Binding = new SettingBinding { Read = _ => Summarise(book) },
    };
}
