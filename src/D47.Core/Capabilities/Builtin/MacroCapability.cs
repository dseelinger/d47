using D47.Core.Actions;
using D47.Core.Conversation;
using D47.Core.Input;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Named multi-step sequences the Commander authored (list.md Phase 10, "Macros").
/// <para>
/// <b>Invocation is by voice; authoring is not.</b> That asymmetry is the checklist's, and it
/// is the one stated exception to "every setting can be set by voice" — composing a new action
/// sequence is the single input whose vocabulary cannot be closed in advance. Running one is
/// the opposite: the name is a word the Commander already chose, so it is the most closed
/// vocabulary there is.
/// </para>
/// <para>
/// <b>The macro name is not in the schema.</b> It cannot be: macro names change whenever the
/// Commander edits the file, and a schema that changed with them would invalidate the entire
/// cached prefix (architecture.md §6). The parameter is a free string, the names available now
/// ride in the live game-state block below the cache breakpoint, and the handler is what
/// actually enforces the list.
/// </para>
/// </summary>
public static class MacroCapability
{
    public const string Id = "macros";

    public const string ListKey = "macros.list";

    public static CapabilityDescriptor Create(MacroStore store, ActionSurface actions) => new()
    {
        Id = Id,
        Group = "Acting on the game",
        Name = "Macros",
        Summary = "Run one of your own named sequences of ship actions.",
        Examples = ["run docking prep", "what macros do I have"],
        Keywords = ["what macros do i have", "list my macros"],
        Display = new CapabilityDisplay { PanelTitle = "Macros", Order = 57 },
        Settings = [ListRow(store)],
        Tools =
        [
            // Argument-free and first, so "what macros do I have" reaches it through the
            // keyword router with no model in the path.
            new ToolDefinition
            {
                Name = "list_macros",
                Description = "Report the Commander's macros, their steps, and any that were refused.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Describe(store))),
            },

            new ToolDefinition
            {
                Name = "run_macro",
                Description =
                    "Run one of the Commander's own named macros. Only names listed in the current "
                    + "game state exist; anything else comes back saying so.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "name",
                        Type = ToolParameterType.String,
                        Description = "The macro's name, as the Commander wrote it.",
                        Required = true,
                    },
                ],
                Handler = (arguments, cancellationToken) => Run(arguments, store, actions, cancellationToken),
            },
        ],
    };

    /// <summary>
    /// The macro names as router phrases. Dynamic, because the Commander authors them — which
    /// is why the router takes these from a function rather than from the descriptor, whose
    /// contents are fixed at registration.
    /// </summary>
    public static IEnumerable<DynamicCommand> Phrases(MacroStore store) =>
        store.Macros.SelectMany(macro => new[]
        {
            Command(macro.Name),
            Command($"run {macro.Name}"),
        });

    private static DynamicCommand Command(string phrase) => new(
        phrase,
        Id,
        "run_macro",
        new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = phrase.StartsWith("run ", StringComparison.OrdinalIgnoreCase) ? phrase[4..] : phrase });

    /// <summary>
    /// The live half, for prompt position 7. Null when there are none, so a Commander with no
    /// macros is not billed for an empty list every turn.
    /// </summary>
    public static string? Live(MacroStore store)
    {
        var names = store.Macros.Select(macro => macro.Name).ToArray();

        return names.Length == 0
            ? null
            : "The Commander's macros, which run_macro accepts by name: " + string.Join(", ", names) + ".";
    }

    /// <summary>
    /// A read-only summary that carries the way into the editor. Not a value the Commander
    /// sets — the thing they set is a whole small program, which is not a settings row and is
    /// the checklist's stated exception to setting everything by voice.
    /// </summary>
    private static SettingRow ListRow(MacroStore store) => new()
    {
        Key = ListKey,
        Advanced = true,
        Label = "Your macros",
        Help = "Named sequences of ship actions. Say the name to run one. Authoring is here rather "
               + "than by voice, because a new sequence is the one thing D47 cannot offer you a "
               + "closed list of words for.",
        Kind = SettingKind.Info,
        DocsAnchor = "writing-one",
        Binding = new SettingBinding { Read = _ => Summarise(store) },
    };

    private static string Summarise(MacroStore store)
    {
        var names = store.Macros.Select(macro => macro.Name).ToArray();

        var line = names.Length == 0
            ? "No macros yet."
            : $"{names.Length} macro{(names.Length == 1 ? string.Empty : "s")}: {string.Join(", ", names)}.";

        return store.Problems.Count == 0
            ? line
            : line + $" {store.Problems.Count} refused — open the editor to see why.";
    }

    private static string Describe(MacroStore store)
    {
        var lines = new List<string>();

        if (store.Macros.Count == 0)
        {
            lines.Add("You have no macros.");
        }
        else
        {
            foreach (var macro in store.Macros)
            {
                var steps = string.Join(", ", macro.Steps.Select(step =>
                    step.State == DesiredState.Toggle
                        ? step.Action
                        : $"{step.Action} {step.State.ToString().ToLowerInvariant()}"));

                lines.Add($"- {macro.Name}: {steps}");
            }
        }

        // Reported, not swallowed. A refused macro is a phrase the Commander is saying into the
        // dark, and this is where they find out why.
        foreach (var problem in store.Problems)
        {
            lines.Add($"- Refused: {problem.Reason}");
        }

        return string.Join("\n", lines);
    }

    private static async Task<ToolResult> Run(
        ToolArguments arguments,
        MacroStore store,
        ActionSurface actions,
        CancellationToken cancellationToken)
    {
        if (!actions.Enabled())
        {
            return ToolResult.Error(
                "Pressing keys in Elite is switched off, so I cannot run a macro. "
                + "The Commander can turn it on in settings.");
        }

        if (!arguments.TryGetString("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Error("No macro was named.");
        }

        if (store.Find(name.Trim()) is not { } macro)
        {
            return ToolResult.Error($"There is no macro called \"{name.Trim()}\".");
        }

        var binds = actions.Binds();
        var context = actions.Context;
        var steps = new List<InputStep>();

        // Resolved as a whole before any of it is sent. A macro that pressed three keys and
        // then discovered the fourth was unreachable would leave the ship half-configured, in
        // a state the Commander did not ask for and has to work out for themselves.
        foreach (var step in macro.Steps)
        {
            var action = GameActions.Find(step.Action)!;
            var reach = ActionReachability.Resolve(action, binds, context);

            if (!reach.IsOffered)
            {
                return ToolResult.Error($"\"{macro.Name}\" did not run: {reach.Reason}");
            }

            if (action.AlreadyIn(step.State, actions.Status()) == true)
            {
                continue;
            }

            steps.AddRange(InputSequence.Tap(reach.Binding!));

            if (step.PauseMs > 0)
            {
                steps.Add(InputStep.Wait(TimeSpan.FromMilliseconds(step.PauseMs)));
            }
        }

        if (steps.Count == 0)
        {
            return ToolResult.Ok($"\"{macro.Name}\" had nothing left to do — everything is already set.");
        }

        var result = await actions.Input.SendAsync(steps, cancellationToken).ConfigureAwait(false);

        return result.Sent
            ? ToolResult.Ok($"Ran \"{macro.Name}\".")
            : ToolResult.Error($"\"{macro.Name}\" stopped part-way: {result.Reason}");
    }
}
