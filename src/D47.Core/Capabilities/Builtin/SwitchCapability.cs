using System.Text;
using D47.Core.Hotas;

namespace D47.Core.Capabilities.Builtin;

/// <summary>What the switch capability needs from outside Core.</summary>
public sealed record SwitchSurface
{
    public required Func<IReadOnlyList<SwitchMapping>> Mappings { get; init; }

    /// <summary>Every mapped switch as it stands, from the reconciler.</summary>
    public required Func<IReadOnlyList<SwitchState>> States { get; init; }

    /// <summary>Why there is nothing to read, or null. A machine with no stick is not a fault.</summary>
    public required Func<string?> Unavailable { get; init; }

    /// <summary>Mappings the file carried and the loader refused, with the reason.</summary>
    public required Func<IReadOnlyList<SwitchProblem>> Problems { get; init; }

    /// <summary>A surface that reaches no hardware. What a test that is not about switches uses.</summary>
    public static SwitchSurface Inert => new()
    {
        Mappings = () => [],
        States = () => [],
        Unavailable = () => "D47 is not reading any controllers.",
        Problems = () => [],
    };
}

/// <summary>
/// HOTAS switches that mean a state rather than a press (list.md Phase 21).
/// <para>
/// <b>Assignment is reachable from the panel only, never from the tool surface.</b> The model
/// reads untrusted text — journal entries, in-game comms, web search and INARA — so a tool that
/// could remap a switch is a tool a hostile in-game message can use to give itself the
/// Commander's controls (architecture.md §7). The one tool here reports and changes nothing.
/// </para>
/// <para>
/// <b>The reporting tool exists because of where the Commander is.</b> Item 6 puts the
/// disagreement on the panel and on the VR surface, and the VR surface is a quad a metre away
/// showing a reduced content set. Being able to ask out loud is the same answer through the
/// channel that works with a headset on.
/// </para>
/// </summary>
public static class SwitchCapability
{
    public const string Id = "switches";

    public const string EnabledKey = "actions.switches";

    public const string ListKey = "switches.list";

    public static CapabilityDescriptor Create(SwitchSurface surface, Func<bool> keyboardEnabled) => new()
    {
        Id = Id,
        Group = "Acting on the game",
        Name = "HOTAS switches",
        Summary = "Make a maintained switch on your stick mean a state — gear down means down, "
                  + "whatever the game was already doing.",
        Examples = ["which switches disagree", "check my switches"],
        Keywords = ["which switches disagree", "check my switches", "switch state"],
        Display = new CapabilityDisplay { PanelTitle = "HOTAS switches", Order = 58 },
        Settings = [EnabledRow(keyboardEnabled), ListRow(surface)],
        Tools =
        [
            // Argument-free, so the model-free keyword router reaches it with nothing in the
            // path — which is what makes it answerable in a headset.
            new ToolDefinition
            {
                Name = "report_switches",
                Description =
                    "Report the Commander's mapped HOTAS switches: where each one is sitting, which "
                    + "ones currently disagree with the game, and any that need reassigning. Reports "
                    + "only; it cannot assign, change or clear a switch.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Describe(surface))),
            },
        ],
    };

    /// <summary>
    /// The live half, for prompt position 7 — the disagreements and nothing else. Null when
    /// there are none, so a Commander with switches that agree is not billed for a list of
    /// them every turn.
    /// </summary>
    public static string? Live(SwitchSurface surface)
    {
        var against = surface.States()
            .Where(state => state.Disagrees is not null)
            .Select(state => $"{state.Name} ({state.Disagrees})")
            .ToArray();

        return against.Length == 0
            ? null
            : "Switches currently sitting against the state of the game or the panel: "
              + string.Join("; ", against) + ".";
    }

    /// <summary>
    /// What both surfaces show, in one sentence: the switches that disagree, or nothing at all.
    /// <para>
    /// A stale switch is harmless under the reconcile rule and costs one extra flip, but only if
    /// the Commander can see it coming. This is the annunciator, and it is the answer real
    /// aviation reached rather than moving the switch — showing the state is cheap and moving it
    /// is not.
    /// </para>
    /// </summary>
    public static string? Annunciator(IReadOnlyList<SwitchState> states)
    {
        var against = states.Where(state => state.Disagrees is not null).ToList();

        if (against.Count == 0)
        {
            return null;
        }

        return string.Join("  ", against.Select(state => $"{state.Name}: {state.Disagrees}"));
    }

    private static string Describe(SwitchSurface surface)
    {
        var states = surface.States();
        var problems = surface.Problems();

        if (states.Count == 0 && problems.Count == 0)
        {
            return surface.Unavailable() is { Length: > 0 } why
                ? $"No switches are mapped. {why}"
                : "No switches are mapped. The Commander assigns one in settings by walking it through "
                  + "its positions.";
        }

        var text = new StringBuilder();

        foreach (var state in states)
        {
            text.Append(state.Name).Append(": ");

            text.Append(state.Health switch
            {
                SwitchHealth.Reassign => "needs reassigning",
                SwitchHealth.Paused => "paused",
                SwitchHealth.Unassigned => "captured but not assigned",
                SwitchHealth.Off => "not acting",
                SwitchHealth.Waiting => "waiting for D47 to find your controllers",
                _ => state.Position is { } position ? position.Describe() : "position unknown",
            });

            if (state.Disagrees is { } disagrees)
            {
                text.Append(" — disagrees with the game (").Append(disagrees).Append(')');
            }
            else if (state.Note is { Length: > 0 } note)
            {
                text.Append(" — ").Append(note);
            }

            text.AppendLine();
        }

        foreach (var problem in problems)
        {
            text.Append("Refused: ").Append(problem.Name).Append(" — ").AppendLine(problem.Reason);
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// Protected, for the reason architecture.md §7 gives: the model reads untrusted text, so a
    /// switch it could turn on to give a mapped toggle the keyboard is privilege escalation
    /// rather than a convenience. The panel, a hotkey and the model-free keyword router all
    /// reach this row; the LLM does not.
    /// </summary>
    private static SettingRow EnabledRow(Func<bool> keyboardEnabled) => new()
    {
        Key = EnabledKey,
        Advanced = true,
        Label = "Let a HOTAS switch operate the ship",
        Help = "When you flip a mapped switch, D47 asks whether Elite is already in the state that "
               + "position means and presses your own binding only if it is not. Between flips it "
               + "touches nothing. Off until you turn it on, and it does nothing while key injection "
               + "is off. A position that names a page of D47's own panel is not behind this row: "
               + "it presses nothing, so it works whether this is on or off.",
        Kind = SettingKind.Toggle,
        DefaultDisplay = "off",
        DocsAnchor = "turning-it-on",
        Protected = true,

        // Absent rather than greyed out when there is nothing for it to do. A row that cannot
        // work while a row above it is off should not assert that it can (list.md Phase 4).
        AppliesWhen = _ => keyboardEnabled(),
        Commands =
        [
            new SettingCommandPhrase("let my switches operate the ship", "true"),
            new SettingCommandPhrase("stop reading my switches", "false"),
            new SettingCommandPhrase("ignore my switches", "false"),
        ],
        Binding = new SettingBinding
        {
            Read = s => s.Actions.Switches ? "true" : "false",
            Write = (s, v) => s with { Actions = s.Actions with { Switches = v is "true" } },
        },
    };

    /// <summary>
    /// A read-only summary that carries the way into the walk. Not a value the Commander sets —
    /// what they set is a capture, which is not a settings value and never could be.
    /// </summary>
    private static SettingRow ListRow(SwitchSurface surface) => new()
    {
        Key = ListKey,
        Advanced = true,
        Label = "Your switches",
        Help = "Maintained toggles on your stick or throttle, each learned by walking it through its "
               + "positions. There is no device list to pick from: Windows reports every stick as "
               + "\"HID-compliant game controller\", so the walk is the only thing that can tell D47 "
               + "what you have.",
        Kind = SettingKind.Info,
        DocsAnchor = "assigning-a-switch",
        Binding = new SettingBinding { Read = _ => Summarise(surface) },
    };

    private static string Summarise(SwitchSurface surface)
    {
        var mappings = surface.Mappings();
        var states = surface.States();
        var problems = surface.Problems();

        if (mappings.Count == 0)
        {
            return problems.Count > 0
                ? $"None. {problems.Count} in the file were refused: "
                  + string.Join("; ", problems.Select(problem => $"{problem.Name} — {problem.Reason}"))
                : surface.Unavailable() ?? "None assigned yet.";
        }

        var parts = new List<string>();

        var reassign = states.Count(state => state.Health == SwitchHealth.Reassign);
        var paused = states.Count(state => state.Health == SwitchHealth.Paused);
        var against = states.Count(state => state.Disagrees is not null);

        parts.Add(mappings.Count == 1 ? "1 switch" : $"{mappings.Count} switches");

        if (reassign > 0)
        {
            parts.Add($"{reassign} needing reassignment");
        }

        if (paused > 0)
        {
            parts.Add($"{paused} paused");
        }

        if (against > 0)
        {
            parts.Add($"{against} sitting against the game");
        }

        if (problems.Count > 0)
        {
            parts.Add($"{problems.Count} refused");
        }

        return string.Join(", ", parts) + ".";
    }
}
