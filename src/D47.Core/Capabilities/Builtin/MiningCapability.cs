using System.Globalization;
using D47.Core.Mining;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The mining target the prospector callout judges each rock against.</summary>
public static class MiningCapability
{
    public const string Id = "mining";

    public const string SetTool = "set_mining_target";

    public const string ClearTool = "clear_mining_target";

    public static CapabilityDescriptor Create(MiningTargetStore? store, Func<string> frontierId) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Mining",
        Summary = "Set the material you are mining for, so prospector results say whether a rock is worth it.",
        Examples = ["mining target painite", "mining target platinum above twenty five percent", "clear the mining target"],
        Display = new CapabilityDisplay { PanelTitle = "Mining", Order = 51, ShowOnPanel = false },
        Tools =
        [
            new ToolDefinition
            {
                Name = SetTool,
                Description =
                    "Set the material the Commander is mining for, and optionally the percentage a "
                    + "prospected rock has to hold. While it is set, prospector results name only that "
                    + "material and say whether the rock is above or below the percentage.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "material",
                        Type = ToolParameterType.String,
                        Description = "The material to mine.",
                        Required = true,
                        AllowedValues = MiningTarget.Materials,
                    },
                    new ToolParameter
                    {
                        Name = "percent",
                        Type = ToolParameterType.Number,
                        Description = "The proportion of the rock, 0 to 100, at or above which it is worth mining. Leave out for any amount.",
                    },
                ],
                Commands =
                [
                    .. MiningTarget.Materials.Select(material => new ToolCommandPhrase(
                        $"mining target {material.ToLowerInvariant()}",
                        new Dictionary<string, string>(StringComparer.Ordinal) { ["material"] = material })),
                ],
                Handler = (arguments, _) => Task.FromResult(Set(store, frontierId(), arguments)),
            },
            new ToolDefinition
            {
                Name = ClearTool,
                Description = "Clear the mining target, so prospector results name every material in the rock again.",
                Commands =
                [
                    new ToolCommandPhrase("clear the mining target", new Dictionary<string, string>(StringComparer.Ordinal)),
                    new ToolCommandPhrase("clear mining target", new Dictionary<string, string>(StringComparer.Ordinal)),
                ],
                Handler = (_, _) => Task.FromResult(Clear(store, frontierId())),
            },
        ],
    };

    private static ToolResult Set(MiningTargetStore? store, string frontierId, ToolArguments arguments)
    {
        if (store is null || frontierId.Length == 0)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet, so there is nobody to set a target for.");
        }

        if (!arguments.TryGetString("material", out var spoken) || MiningTarget.Match(spoken) is not { } material)
        {
            return ToolResult.Error(
                $"I do not know that material. I can target {string.Join(", ", MiningTarget.Materials)}.");
        }

        double? percent = null;

        if (arguments.TryGetDouble("percent", out var given))
        {
            if (given is < 0 or > 100)
            {
                return ToolResult.Error("The percentage has to be between 0 and 100.");
            }

            percent = given;
        }

        store.Set(frontierId, new MiningTarget(material, percent));

        return ToolResult.Ok(percent is { } floor
            ? $"Mining target: {material} above {floor.ToString("0.#", CultureInfo.InvariantCulture)}%."
            : $"Mining target: {material}.");
    }

    private static ToolResult Clear(MiningTargetStore? store, string frontierId)
    {
        if (store is null || frontierId.Length == 0)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet, so there is no target to clear.");
        }

        return ToolResult.Ok(store.Clear(frontierId) ? "Mining target cleared." : "There was no mining target set.");
    }
}
