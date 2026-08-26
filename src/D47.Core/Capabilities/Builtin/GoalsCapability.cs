using D47.Core.Configuration;
using D47.Core.Goals;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// The Commander's long arcs — the ambitions that outlive a checklist (list.md Phase 34).
/// <para>
/// <b>`get_goals` is advertised, and it is the first tool in four phases that is.</b> Phases 31, 32
/// and 33 shipped everything <see cref="ToolDefinition.Protected"/>, and Phase 32 recorded that the
/// arithmetic and the reasoning happened to point the same way. Here they do not: <em>how is the
/// Elite exploration push going</em> is a thing said out loud mid-flight, and an arc the model
/// cannot see is an arc d47 cannot be asked about — which would leave this phase's whole subject
/// reachable only by opening a panel.
/// </para>
/// <para>
/// The SRV profile measured 39,914 bytes against
/// <see cref="Conversation.ToolProfiles.ComfortableBytes"/> of 40,000 before this phase, so paying
/// for it meant trimming advertised descriptions elsewhere rather than raising the constant a third
/// time — which <see cref="Conversation.ToolProfiles"/> says outright is the wrong answer.
/// </para>
/// <para>
/// <b>Everything that writes stays Protected.</b> Setting an arc aside, inventing one, calling one
/// done and backfilling from the journals are all the Commander's acts, and the model is the one
/// component in d47 that consumes untrusted text. It reads the arcs and touches none of them.
/// </para>
/// <para>
/// <b>Reading the journals is a <see cref="SettingRow.Press"/> on an
/// <see cref="SettingKind.Info"/> row</b>, which <see cref="SettingsService.Apply"/> refuses — so a
/// pass over 900 files cannot be started from the tool surface, by the mechanism that already
/// exists rather than by a flag. The same arrangement Phases 32 and 33 use.
/// </para>
/// </summary>
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
                "set aside the mercenary goal",
            ],

            // Phrases, never bare words. "goals" alone would hijack every sentence about a
            // community goal, which is a different capability and a far commoner subject.
            Keywords =
            [
                "how are my goals going",
                "what are my goals",
                "how are my long term goals",
            ],

            // After the Commander's log, for the fourth time and the same reason: this is another
            // thing d47 does with a history rather than with the game, and the tail of the registry
            // is where a new capability costs two documentation pages a nav_order instead of thirty.
            Display = new CapabilityDisplay { PanelTitle = "Goals", Order = 16 },
            Settings = [StoreRow(book, backfill, now)],
            Tools =
            [
                // Argument-free and first, so the descriptor keywords above reach it through
                // KeywordRouter.Match with no model in the path.
                new ToolDefinition
                {
                    Name = "get_goals",
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

                    // Protected. It proposes rather than commits, so a hostile in-game message
                    // reaching this achieves a line the Commander declines — but a tool that writes
                    // the proposals file on somebody else's say-so is still not one the model needs.
                    Protected = true,
                    Handler = (arguments, _) => Task.FromResult(Step(book, arguments)),
                },

                new ToolDefinition
                {
                    Name = "set_goal_aside",
                    Description =
                        "Stop showing one long-running goal, or bring it back. The Commander's own decision.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "goal",
                            Type = ToolParameterType.String,
                            Description = "Which goal, by name or key.",
                            Required = true,
                        },
                        new ToolParameter
                        {
                            Name = "aside",
                            Type = ToolParameterType.Boolean,
                            Description = "True to set it aside, false to bring it back. Defaults to true.",
                        },
                    ],
                    Protected = true,
                    Handler = (arguments, _) => Task.FromResult(Aside(book, arguments)),
                },
            ],
        };

    /// <summary>
    /// The line the panel row reads. Lives here rather than in the App so the panel and the tool
    /// surface cannot describe the same store differently.
    /// </summary>
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

    private static ToolResult Aside(GoalBook? book, ToolArguments arguments)
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

        var aside = !arguments.TryGetBoolean("aside", out var wanted) || wanted;

        return ToolResult.Ok(book.SetAside(goal, aside));
    }

    /// <summary>
    /// The store, with the button that fills it.
    /// <para>
    /// <see cref="SettingKind.Info"/> with a <see cref="SettingRow.Press"/>, so
    /// <see cref="SettingsService.Apply"/> refuses it and nothing on the tool surface can start a
    /// pass over the Commander's journals. Same mechanism, same reason, third phase running.
    /// </para>
    /// </summary>
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
        DocsAnchor = "backfill",
        PressLabel = backfill() is null ? null : "Read my journals",
        Press = backfill() is null ? null : () => backfill()!(),
        Binding = new SettingBinding { Read = _ => Summarise(book, now()) },
    };
}
