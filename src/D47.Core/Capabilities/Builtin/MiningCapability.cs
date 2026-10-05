using System.Globalization;
using D47.Core.Journal;
using D47.Core.Mining;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The mining target the prospector callout judges each rock against, and the limpets a haul takes.</summary>
public static class MiningCapability
{
    public const string Id = "mining";

    public const string SetTool = "set_mining_target";

    public const string ClearTool = "clear_mining_target";

    public const string EstimateTool = "estimate_limpets";

    public static CapabilityDescriptor Create(
        MiningTargetStore? store,
        Func<string> frontierId,
        Func<HistoryState>? history = null,
        Func<string, IReadOnlyList<MiningRun>?>? pastRuns = null) => new()
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
            new ToolDefinition
            {
                Name = EstimateTool,
                Description =
                    "Estimate how many collector and prospector limpets it takes to mine a number of tonnes, "
                    + "from the Commander's own past mining runs. With a material, uses the runs that mined "
                    + "mostly that material when there are at least three.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "tonnes",
                        Type = ToolParameterType.Number,
                        Description = "The tonnes to mine.",
                        Required = true,
                    },
                    new ToolParameter
                    {
                        Name = "material",
                        Type = ToolParameterType.String,
                        Description = "The material to mine. Leave out for any.",
                        AllowedValues = MiningTarget.Materials,
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(
                    Estimate(frontierId(), history?.Invoke() ?? HistoryState.Done, pastRuns, arguments)),
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

    private static ToolResult Estimate(
        string frontierId,
        HistoryState history,
        Func<string, IReadOnlyList<MiningRun>?>? pastRuns,
        ToolArguments arguments)
    {
        if (!arguments.TryGetDouble("tonnes", out var tonnes) || tonnes <= 0)
        {
            return ToolResult.Error("Say how many tonnes, more than zero.");
        }

        string? material = null;

        if (arguments.TryGetString("material", out var spoken) && spoken.Length > 0)
        {
            material = MiningTarget.Match(spoken);

            if (material is null)
            {
                return ToolResult.Error(
                    $"I do not know that material. I can estimate for {string.Join(", ", MiningTarget.Materials)}.");
            }
        }

        switch (history)
        {
            case HistoryState.Pending or HistoryState.Running:
                return ToolResult.Ok("I have not finished reading the journal history yet, so I cannot estimate limpets.");
            case HistoryState.Failed or HistoryState.Stopped:
                return ToolResult.Ok("Reading the journal history did not finish, so I cannot estimate limpets.");
        }

        if (frontierId.Length == 0)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet, so there are no mining runs to estimate from.");
        }

        var runs = pastRuns?.Invoke(frontierId) ?? [];

        return ToolResult.Ok(LimpetEstimate.Describe(runs, tonnes, material) ?? "I have no mining runs to estimate from.");
    }
}
