using D47.Core.Conversation;
using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Switching which Commander d47 shows, by voice (#895).</summary>
public static class CommandersCapability
{
    public const string Id = "commanders";

    public const string SwitchTool = "switch_commander";

    /// <summary>The spoken reply to a switch; the same words as the Commander's own confirmation.</summary>
    public static string Switched(string name) =>
        $"Switched to CMDR {name}. I'll stay with this commander until you switch again.";

    /// <param name="commanders">Null until the history walk has listed them.</param>
    /// <param name="pick">Must hand the pick to the tick thread; game state is written nowhere else.</param>
    public static CapabilityDescriptor Create(
        Func<IReadOnlyDictionary<string, CommanderSighting>?> commanders,
        Func<string?> activeFrontierId,
        Action<CommanderIdentity> pick) => new()
    {
        Id = Id,
        Group = "Interface",
        Name = "Switch Commander",
        Summary = "Change which Commander d47 shows, by saying their name.",
        Examples = ["switch to commander Kestrel Vane", "switch to Kestrel Vane"],
        Display = new CapabilityDisplay { PanelTitle = "Switch Commander", Order = 48, ShowOnPanel = false },
        Tools =
        [
            new ToolDefinition
            {
                Name = SwitchTool,
                Description =
                    "Switch d47 to another Commander found in the journals. Reachable by spoken phrase, "
                    + "the panel or a hotkey, never by the model.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "frontier_id",
                        Type = ToolParameterType.String,
                        Description = "The Frontier ID of the Commander to switch to.",
                        Required = true,
                    },
                ],

                // A misheard sentence passed by the model would switch Commanders mid-conversation.
                Protected = true,
                RefusalExample = "switch to commander <name>",
                Handler = (arguments, _) => Task.FromResult(Switch(commanders(), activeFrontierId(), pick, arguments)),
            },
        ],
    };

    /// <summary>"Switch to commander X" and "switch to X", one pair per Commander other than the active one.</summary>
    public static IEnumerable<DynamicCommand> Phrases(
        Func<IReadOnlyDictionary<string, CommanderSighting>?> commanders,
        Func<string?> activeFrontierId)
    {
        var active = activeFrontierId();

        foreach (var sighting in commanders()?.Values ?? [])
        {
            if (string.Equals(sighting.FrontierId, active, StringComparison.Ordinal))
            {
                continue;
            }

            var arguments = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["frontier_id"] = sighting.FrontierId,
            };

            yield return new DynamicCommand($"switch to commander {sighting.Name}", Id, SwitchTool, arguments);
            yield return new DynamicCommand($"switch to {sighting.Name}", Id, SwitchTool, arguments);
        }
    }

    private static ToolResult Switch(
        IReadOnlyDictionary<string, CommanderSighting>? commanders,
        string? active,
        Action<CommanderIdentity> pick,
        ToolArguments arguments)
    {
        if (!arguments.TryGetString("frontier_id", out var fid)
            || commanders is null
            || !commanders.TryGetValue(fid, out var sighting))
        {
            return ToolResult.Error("I do not know that Commander.");
        }

        if (string.Equals(fid, active, StringComparison.Ordinal))
        {
            return ToolResult.Ok($"You are already CMDR {sighting.Name}.");
        }

        pick(new CommanderIdentity(sighting.FrontierId, sighting.Name));

        return ToolResult.Ok(Switched(sighting.Name));
    }
}
