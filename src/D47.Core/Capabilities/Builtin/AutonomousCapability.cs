using D47.Core.Actions;
using D47.Core.Configuration;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The autonomous-action category (Phase 10, items 2 and 3).</summary>
public static class AutonomousCapability
{
    public const string Id = "autonomous-actions";

    public const string HonkKey = "actions.honkOnArrival";

    public static CapabilityDescriptor Create(Func<string> describe) => new()
    {
        Id = Id,
        Group = "Acting on the game",
        Name = "Acting on its own",
        Summary = "Things D47 does to your ship without being asked. All off until you turn them on.",
        Examples = ["turn on the arrival honk", "stop honking on arrival"],
        Display = new CapabilityDisplay { PanelTitle = "Acting on its own", Order = 56 },
        Tools =
        [
            new ToolDefinition
            {
                Name = "describe_autonomous_actions",
                Description =
                    "Report which autonomous actions exist and which the Commander has switched on. "
                    + "Reports only; it cannot change any of them.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(describe())),
            },
        ],
        Settings = [HonkRow()],
    };

    /// <summary>Protected, like every row that reaches the keyboard.</summary>
    private static SettingRow HonkRow() => new()
    {
        Key = HonkKey,
        Advanced = true,
        Label = "Honk on arriving in a system",
        Help = "Fires the discovery scanner by itself after each jump, by holding your own fire "
               + "button for six seconds. Needs the scanner in your current fire group and "
               + "analysis mode on; D47 says so rather than switching modes for you.",
        Kind = SettingKind.Toggle,
        DefaultDisplay = "off",
        DocsAnchor = "the-arrival-honk",
        Protected = true,
        Commands =
        [
            new SettingCommandPhrase("honk when we arrive", "true"),
            new SettingCommandPhrase("turn on the arrival honk", "true"),
            new SettingCommandPhrase("stop honking on arrival", "false"),
            new SettingCommandPhrase("turn off the arrival honk", "false"),
        ],
        Binding = new SettingBinding
        {
            Read = s => s.Actions.HonkOnArrival ? "true" : "false",
            Write = (s, v) => s with { Actions = s.Actions with { HonkOnArrival = v is "true" } },
        },
    };

    /// <summary>What the reporting tool says.</summary>
    public static string Describe(AutonomousActionRunner runner)
    {
        if (runner.Actions.Count == 0)
        {
            return "There are no autonomous actions.";
        }

        var lines = runner.Actions.Select(action =>
            $"- {action.Label}: {(action.IsEnabled ? "on" : "off")}");

        return "Autonomous actions, each switched on separately:\n" + string.Join("\n", lines);
    }
}
