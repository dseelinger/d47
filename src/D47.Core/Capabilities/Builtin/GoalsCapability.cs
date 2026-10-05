using D47.Core.Configuration;
using D47.Core.Goals;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The Commander's long arcs — the ambitions that outlive a checklist (Phase 34).</summary>
public static class GoalsCapability
{
    public const string Id = "goals";

    /// <summary>The row that reads the arcs back, and carries the button that ages them.</summary>
    public const string StoreKey = "goals.store";

    public static CapabilityDescriptor Create(GoalBook? book, Func<Action?> backfill, Func<DateTimeOffset> now) =>
        new()
        {
            Id = Id,
            Group = "Conversation",
            Name = "Goals",
            Summary = "Track the campaigns that take months, derive their progress from the journal, and say what to do about one today.",
            Examples =
            [
                "how are my goals going",
                "what should I do about the engineers goal",
                "remove the mercenary goal",
                "recover the mercenary goal",
            ],

            // Phrases, never bare words. "goals" alone would hijack every sentence about a community goal,
            // which is a different capability and a far commoner subject.
            Keywords =
            [
                "how are my goals going",
                "what are my goals",
                "how are my long term goals",
            ],

            // After the Commander's log, for the fourth time and the same reason: this is another thing d47
            // does with a history rather than with the game, and the tail of the registry is where a new
            // capability costs two documentation pages a nav_order instead of thirty.
            Display = new CapabilityDisplay { PanelTitle = "Goals", Order = 16 },
            Settings = [StoreRow(book, backfill, now)],
            Tools =
            [
                // Argument-free and first, so the descriptor keywords above reach it through
                // KeywordRouter.Match with no model in the path.
                new ToolDefinition
                {
                    Name = "get_goals",
                    AlwaysLoaded = true,
                    Description =
                        "Read back the Commander's long-running goals: how far along each is, how long it has "
                        + "been running, and where the figure came from.",
                    Commands =
                    [
                        new ToolCommandPhrase("how are my goals going", new Dictionary<string, string>()),
                        new ToolCommandPhrase("what are my goals", new Dictionary<string, string>()),
                    ],
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(
                        book is null
                            ? "I am not tracking goals in this configuration."
                            : book.Describe(now()))),
                },

                new ToolDefinition
                {
                    Name = "get_goal_step",
                    Description =
                        "Say what to do about one long-running goal today, and offer it as a checklist line.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "goal",
                            Type = ToolParameterType.String,
                            Description = "Which goal, by name or key, as read back by get_goals.",
                            Required = true,
                        },
                    ],

                    // Protected.
                    Protected = true,
                    Handler = (arguments, _) => Task.FromResult(Step(book, arguments)),
                },

                new ToolDefinition
                {
                    Name = "remove_goal",
                    Description = "Take one long-running goal off the Goals page. The Commander's own decision.",
                    Parameters = GoalParameter(),
                    Commands = [.. Phrases("remove")],
                    Protected = true,
                    Handler = (arguments, _) => Task.FromResult(Change(book, arguments, remove: true)),
                },

                new ToolDefinition
                {
                    Name = "recover_goal",
                    Description = "Put a removed long-running goal back on the Goals page. The Commander's own decision.",
                    Parameters = GoalParameter(),
                    Commands = [.. Phrases("recover")],
                    Protected = true,
                    Handler = (arguments, _) => Task.FromResult(Change(book, arguments, remove: false)),
                },
            ],
        };

    /// <summary>The line the panel row reads.</summary>
    public static string Summarise(GoalBook? book, DateTimeOffset now) =>
        book?.Summarise(now) ?? "D47 is not tracking goals in this configuration.";

    private static ToolResult Step(GoalBook? book, ToolArguments arguments)
    {
        if (book is null)
        {
            return ToolResult.Error("I am not tracking goals in this configuration.");
        }

        arguments.TryGetString("goal", out var goal);

        return string.IsNullOrWhiteSpace(goal)
            ? ToolResult.Error("Which goal?")
            : ToolResult.Ok(book.Promote(goal));
    }

    /// <summary>What a Commander calls each built-in goal, and the key it reaches.</summary>
    private static readonly (string Word, string Key)[] Nicknames =
    [
        ("combat", "rank.combat"),
        ("trade", "rank.trade"),
        ("exploration", "rank.explore"),
        ("explorer", "rank.explore"),
        ("mercenary", "rank.soldier"),
        ("exobiology", "rank.exobiologist"),
        ("imperial navy", "rank.empire"),
        ("federal navy", "rank.federation"),
        ("powerplay", GoalCatalogue.Powerplay),
        ("engineers", GoalCatalogue.Engineers),
        ("ship collection", GoalCatalogue.Ships),
    ];

    private static IEnumerable<ToolCommandPhrase> Phrases(string verb) =>
        Nicknames.Select(nickname => new ToolCommandPhrase(
            $"{verb} the {nickname.Word} goal",
            new Dictionary<string, string> { ["goal"] = nickname.Key }));

    private static IReadOnlyList<ToolParameter> GoalParameter() =>
    [
        new ToolParameter
        {
            Name = "goal",
            Type = ToolParameterType.String,
            Description = "Which goal, by name or key.",
            Required = true,
        },
    ];

    private static ToolResult Change(GoalBook? book, ToolArguments arguments, bool remove)
    {
        if (book is null)
        {
            return ToolResult.Error("I am not tracking goals in this configuration.");
        }

        arguments.TryGetString("goal", out var goal);

        if (string.IsNullOrWhiteSpace(goal))
        {
            return ToolResult.Error("Which goal?");
        }

        return ToolResult.Ok(remove ? book.Remove(goal) : book.Recover(goal));
    }

    /// <summary>The store, with the button that fills it.</summary>
    private static SettingRow StoreRow(GoalBook? book, Func<Action?> backfill, Func<DateTimeOffset> now) => new()
    {
        Key = StoreKey,
        Advanced = true,
        Label = "Your long goals",
        Help =
            "Campaigns that take months — Elite in each career, every engineer, the ship collection, "
            + "the exploration milestones. Progress is worked out from your journal rather than typed, "
            + "and reading back through the journals already on this disk is what gives each one its age. "
            + "Nothing leaves the machine.",
        Kind = SettingKind.Info,
        DocsAnchor = "ages-come-from-your-journals",
        PressLabel = backfill() is null ? null : "Read my journals",
        Press = backfill() is null ? null : () => backfill()!(),
        Binding = new SettingBinding { Read = _ => Summarise(book, now()) },
    };
}
