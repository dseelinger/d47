using D47.Core.Configuration;
using D47.Core.Memory;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// What d47 remembers about the Commander (Phase 31).
/// <para>
/// <b>One tool is advertised and two are not, and the split is arithmetic before it is anything
/// else.</b> The SRV profile — the largest, because it carries that vehicle's controls on top of
/// everything else — was at 39,639 bytes against <see cref="Conversation.ToolProfiles.ComfortableBytes"/>
/// of 40,000 when this phase began. That is room for one small tool, and it is spent on the write
/// path: a memory store the model cannot write to only ever fills up when somebody opens a panel,
/// and the moment a Commander says something worth keeping is a moment they are talking rather than
/// clicking. <b>Shipped at 39,914</b> — 275 bytes for the write tool and 86 left, which means the
/// next phase wanting an advertised tool has to do the deferred-loading work
/// <see cref="Conversation.ToolProfiles.ComfortableBytes"/> names before it can have one.
/// </para>
/// <para>
/// The other two are <see cref="ToolDefinition.Protected"/> with router phrases, which costs zero
/// advertised bytes — Phase 26's answer, applied where it is free. It also makes item 2's promise
/// keepable: <em>what do you remember about me</em> is argument-free, so the keyword router reaches
/// it with no model in the path and it answers from <b>the store</b>, while the prompt carries a
/// bounded <b>sample</b> (see <see cref="MemoryRecall"/>).
/// </para>
/// <para>
/// <b>Writing is not protected and does not need to be.</b> It presses no key and disables no
/// warning — the same reading <see cref="LoreCapability"/> records. What it does do is write
/// persistent local state that d47 later speaks unprompted, and the answer to that is a label rather
/// than a lock: an entry the model wrote is <see cref="MemoryTier.Inferred"/> and is read back as an
/// inference for as long as it exists, and the tier is not a parameter the call can set.
/// </para>
/// <para>
/// <b>Emptying the store is not here at all.</b> It is a <see cref="SettingRow.Press"/> on
/// <see cref="PrivacyCapability"/>, which is where a Commander looks for it, and it has no router
/// phrase — "forget everything about me" is a sentence a transcriber can produce out of a misheard
/// one, and it is the only action in this phase that cannot be undone.
/// </para>
/// </summary>
public static class MemoryCapability
{
    public const string Id = "memory";

    public const string EnabledKey = "memory.enabled";

    public const string ExpiryKey = "memory.expiryDays";

    /// <summary>
    /// The store itself, as a disclosure with the way into it beside it.
    /// <para>
    /// An <see cref="SettingKind.Info"/> row rather than anything writable, which puts adding a fact
    /// on the right side of the trust boundary for free: <see cref="SettingsService.Apply"/> refuses
    /// Info rows outright, so nothing reachable from the tool surface can reach this. It is the one
    /// route that produces <see cref="MemoryTier.Stated"/>, and it produces it because a person's
    /// hands were on it.
    /// </para>
    /// </summary>
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

            // Phrases, never bare words — "remember" and "memory" both turn up in half of what a
            // Commander says to a companion. The rule JournalCapability documents.
            Keywords =
            [
                "what do you remember about me",
                "what do you know about me",
                "what have you written down about me",
            ],

            // Beside Persona, which is the other section about who is talking to whom, and above
            // everything about the game. A Commander looking for what d47 knows about them is not
            // going to look for it under Knowledge.
            Display = new CapabilityDisplay { PanelTitle = "Memory", Order = 13 },
            Settings = [EnabledRow(), ExpiryRow(), StoreRow(book)],
            Tools =
            [
                // Argument-free and first, so "what do you remember about me" reaches it through
                // the keyword router with no model in the path — and answers from the store rather
                // than from the sample the prompt is carrying.
                new ToolDefinition
                {
                    Name = "get_memories",
                    Description =
                        "Read back everything D47 has written down about the Commander, each line saying whether "
                        + "the Commander said it, D47 noticed it, or D47 worked it out for itself.",

                    // Protected for room rather than for safety, and that is worth saying plainly:
                    // there is nothing dangerous about reading this back. It is not advertised
                    // because the ceiling has room for one tool and the write path needs it more,
                    // and the router reaches this without a model anyway.
                    Protected = true,
                    Commands =
                    [
                        new ToolCommandPhrase("what do you remember about me", new Dictionary<string, string>()),
                        new ToolCommandPhrase("what do you know about me", new Dictionary<string, string>()),
                    ],
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(
                        book is null
                            ? "I have nowhere to keep anything in this configuration."
                            : MemoryRecall.Describe(book.Mine))),
                },

                // The one advertised tool in the phase, and every character of it is measured
                // against the 361 bytes the ceiling had left: one parameter, no tier, no tags, and
                // a description written to be short rather than to be thorough. It came to 275.
                //
                // No tier parameter is the security property rather than the saving: a call that
                // could declare its own entry the Commander's word is a call a hostile in-game
                // message could make (see MemoryTier).
                new ToolDefinition
                {
                    Name = "remember_about_me",
                    Description =
                        "Write down one lasting fact or preference about the Commander, not what happened "
                        + "today. Stored as unverified.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "fact",
                            Type = ToolParameterType.String,
                            Description = "The fact, in one sentence.",
                            Required = true,
                        },
                    ],
                    Handler = (arguments, _) => Task.FromResult(
                        Remember(book, settings.Current.Memory, now(), arguments)),
                },

                new ToolDefinition
                {
                    Name = "forget_memory",
                    Description = "Forget one thing D47 has written down about the Commander, by its key.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "key",
                            Type = ToolParameterType.String,
                            Description = "The key of the entry to forget, as read back by get_memories.",
                            Required = true,
                        },
                    ],

                    // Protected, and here it is safety rather than room. Reading a key back is easy
                    // and a key is exactly the kind of token a hostile in-game message can name —
                    // so removing one is the Commander's act, through the panel or a phrase they
                    // said themselves.
                    Protected = true,
                    Handler = (arguments, _) => Task.FromResult(Forget(book, arguments)),
                },
            ],
        };
    }

    /// <summary>
    /// The line the window's own header reads, and the row above the button. Lives here rather than
    /// in the App so the panel and the tool surface cannot describe the store differently.
    /// </summary>
    public static string Summarise(MemoryBook? book) =>
        book?.Summarise() ?? "Nothing is keeping memories in this configuration.";

    /// <summary>
    /// How long entries live, from the setting. <see cref="TimeSpan.Zero"/> is never, which
    /// <see cref="MemoryStore.Expire"/> takes as "expire nothing".
    /// </summary>
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
            // Said rather than silently dropped. A model told nothing happened will try again with
            // different words; a model told the Commander switched it off will not.
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
        Label = "Remember things about me",
        Help =
            "Keep facts between sessions, and send a few of the relevant ones with each question. "
            + "Off stops every new entry and stops any of it reaching the model — it does not erase "
            + "what is already there, which is its own action in [Privacy](privacy).",
        Kind = SettingKind.Toggle,
        DefaultDisplay = "on",
        DocsAnchor = "remembering",

        // Protected. Not caution about the model: this row decides whether facts about the
        // Commander are written to disk and put in a prompt, and untrusted text can reach the tool
        // surface — so it reaches the panel, the hotkeys and the router, and not the model.
        Protected = true,
        Commands =
        [
            new SettingCommandPhrase("stop remembering things about me", "false"),
            new SettingCommandPhrase("start remembering things about me", "true"),
        ],
        Binding = new SettingBinding
        {
            Read = s => s.Memory.Enabled ? "true" : "false",
            Write = (s, v) => s with { Memory = s.Memory with { Enabled = v is not "false" } },
        },
    };

    private static SettingRow ExpiryRow() => new()
    {
        Key = ExpiryKey,
        Advanced = true,
        Label = "Forget after",
        Help =
            "How long a fact lasts before D47 drops it. Anything you told D47 yourself is said out "
            + "loud as it goes, so an expiry can never quietly lose your own words.",
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
        DocsAnchor = "expiry",
        Protected = true,

        // One phrase per value, because a protected row is unreachable from the tool surface by
        // design and a closed value set with no phrases is a row a Commander flying in VR cannot
        // reach at all. Worded as instructions rather than as durations: nobody says "set my memory
        // expiry to ninety days".
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
        DocsAnchor = "store",
        Binding = new SettingBinding { Read = _ => Summarise(book) },
    };
}
