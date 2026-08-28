using D47.Core.Configuration;
using D47.Core.Habits;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// What d47 has worked out about the Commander from their own journals (Phase 32).
/// <para>
/// <b>Every tool here is <see cref="ToolDefinition.Protected"/>, and that is not the byte ceiling
/// talking.</b> Phase 31 shipped the SRV profile at 39,914 against
/// <see cref="Conversation.ToolProfiles.ComfortableBytes"/> of 40,000 and said the next phase
/// wanting an advertised tool would have to do the deferred-loading work first. This phase does not
/// want one — the arithmetic and the reasoning point the same way for once.
/// </para>
/// <para>
/// item 1 says <em>no journal reaches a model</em>. The conclusions are the same rule one step
/// further along: the thing that reads hostile in-game text is not the thing that gets handed a
/// list of the Commander's mistakes. So the whole phase is Core-side — the miner, the store, the
/// callout and the readback — and the model can see none of it while the Commander can see all of
/// it.
/// </para>
/// <para>
/// <b>The two phrases that matter are argument-free, and that is what makes them reachable.</b>
/// <see cref="Conversation.KeywordRouter.MatchToolCommand"/>'s grammar is closed — a declared phrase
/// carries declared arguments and extracts nothing from what was said — so <em>why did you say
/// that</em> and <em>stop telling me that</em> work only because both mean "the one you just said",
/// which <see cref="HabitBook.LastSpoken"/> knows. Phase 31 hit the same wall from the other side and
/// had to record that a memory is a sentence no phrase could carry.
/// </para>
/// <para>
/// <b>Mining is a <see cref="SettingRow.Press"/> on an <see cref="SettingKind.Info"/> row</b>, which
/// <see cref="SettingsService.Apply"/> refuses outright — so reading 376 MB of journals cannot be
/// started from the tool surface, by the mechanism that already exists rather than by a flag.
/// </para>
/// </summary>
public static class HabitsCapability
{
    public const string Id = "habits";

    /// <summary>The row that reads the store back, and carries the button that fills it.</summary>
    public const string StoreKey = "habits.store";

    public static CapabilityDescriptor Create(HabitBook? book, Func<Action?> mine) =>
        new()
        {
            Id = Id,
            Group = "Conversation",
            Name = "Habits",
            Summary = "Notice what the Commander keeps doing, from their own journals, with the count behind it.",
            Examples =
            [
                "what have you noticed about me",
                "why did you say that",
                "stop telling me that",
            ],

            // Phrases, never bare words. "habits" alone would hijack any sentence containing it,
            // which is the rule JournalCapability documents — and "noticed" is a word a Commander
            // uses about the game far more often than about themselves.
            Keywords =
            [
                "what have you noticed about me",
                "what have you noticed about my flying",
                "what are my habits",
            ],

            // Immediately after Memory, which is the other section about what d47 knows about the
            // person rather than about the game — and late in the registry, because nav_order is
            // the registry index and inserting early shifts every page after it.
            Display = new CapabilityDisplay { PanelTitle = "Habits", Order = 14 },
            Settings = [StoreRow(book, mine)],
            Tools =
            [
                // Argument-free and first, so the descriptor keywords above reach it through
                // KeywordRouter.Match with no model in the path.
                new ToolDefinition
                {
                    Name = "get_habits",
                    Description =
                        "Read back what D47 has noticed the Commander keeps doing, each one with how often it "
                        + "happened, out of how many chances, and over what window.",

                    // Protected. Not because reading this back is dangerous, but because the model
                    // is the one component in d47 that consumes untrusted text, and a list of a
                    // Commander's mistakes is not something it needs to hold.
                    Protected = true,
                    Commands =
                    [
                        new ToolCommandPhrase("what have you noticed about me", new Dictionary<string, string>()),
                        new ToolCommandPhrase("what are my habits", new Dictionary<string, string>()),
                    ],
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(
                        book is null
                            ? "I am not reading your journals in this configuration."
                            : book.Describe())),
                },

                new ToolDefinition
                {
                    Name = "explain_habit",
                    Description =
                        "Say why D47 made the observation it just made — the count, the sample and the window "
                        + "behind it.",
                    Protected = true,

                    // Phase 32 item 3: "it says why it fired if asked". Whole-utterance
                    // matching is what keeps this from swallowing "why did you say that about the
                    // fuel", which is a question for the model.
                    Commands =
                    [
                        new ToolCommandPhrase("why did you say that", new Dictionary<string, string>()),
                        new ToolCommandPhrase("why do you say that", new Dictionary<string, string>()),
                    ],
                    Handler = (_, _) => Task.FromResult(Explain(book)),
                },

                new ToolDefinition
                {
                    Name = "dismiss_habit",
                    Description =
                        "Stop D47 mentioning one thing it noticed, permanently. With no key, the one it just said.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "key",
                            Type = ToolParameterType.String,

                            // Optional, which is the whole trick: the phrases carry no arguments
                            // and mean "the one you just said", and the panel names one.
                            Description = "Which one, as read back by get_habits. Omit for the last one spoken.",
                        },
                    ],
                    Protected = true,
                    Commands =
                    [
                        new ToolCommandPhrase("stop telling me that", new Dictionary<string, string>()),
                        new ToolCommandPhrase("never mention that again", new Dictionary<string, string>()),
                    ],
                    Handler = (arguments, _) => Task.FromResult(Dismiss(book, arguments)),
                },
            ],
        };

    /// <summary>
    /// The line the panel row reads. Lives here rather than in the App so the panel and the tool
    /// surface cannot describe the same store differently.
    /// </summary>
    public static string Summarise(HabitBook? book) =>
        book?.Summarise() ?? "D47 is not reading your journals in this configuration.";

    private static ToolResult Explain(HabitBook? book)
    {
        if (book is null)
        {
            return ToolResult.Error("I am not reading your journals in this configuration.");
        }

        return book.LastSpoken is { } claim
            ? ToolResult.Ok(claim.Explained())
            : ToolResult.Error("I have not said anything about you this session.");
    }

    private static ToolResult Dismiss(HabitBook? book, ToolArguments arguments)
    {
        if (book is null)
        {
            return ToolResult.Error("I am not reading your journals in this configuration.");
        }

        arguments.TryGetString("key", out var key);

        var dropped = book.Dismiss(key);

        return dropped is null
            ? ToolResult.Error(
                string.IsNullOrWhiteSpace(key)
                    ? "I have not said anything about you this session."
                    : $"Nothing I have noticed is keyed {key.Trim()}.")
            : ToolResult.Ok($"Dropped, for good: {dropped.Subject}. I will not bring it up again.");
    }

    /// <summary>
    /// The store, with the button that fills it.
    /// <para>
    /// <see cref="SettingKind.Info"/> with a <see cref="SettingRow.Press"/>, so
    /// <see cref="SettingsService.Apply"/> refuses it and nothing on the tool surface can start a
    /// pass over the Commander's journals. The same mechanism emptying the memory store uses, and
    /// for a related reason: this one reads every journal on the disk, and that is a thing a person
    /// asks for rather than a thing that happens.
    /// </para>
    /// </summary>
    private static SettingRow StoreRow(HabitBook? book, Func<Action?> mine) => new()
    {
        Key = StoreKey,
        Advanced = true,
        Label = "What D47 has noticed",
        Help =
            "D47 reads through the journals already on this disk and looks for things you keep doing. "
            + "Nothing leaves the machine, no journal is sent to a model, and every claim comes with "
            + "how often it happened and out of how many chances.",
        Kind = SettingKind.Info,
        DocsAnchor = "mining",
        PressLabel = mine() is null ? null : "Read my journals",
        Press = mine() is null ? null : () => mine()!(),
        Binding = new SettingBinding { Read = _ => Summarise(book) },
    };
}
