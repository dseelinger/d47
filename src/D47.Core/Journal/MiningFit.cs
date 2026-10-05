namespace D47.Core.Journal;

/// <summary>Whether a remembered loadout can mine tritium, with the fitted modules or the gap as evidence.</summary>
public sealed record MiningFit(bool IsFit, string Evidence)
{
    private static readonly string[] Miners = ["hpt_mininglaser", "hpt_miningtoolv2"];
    private const string Collector = "int_dronecontrol_collection";
    private const string Refinery = "int_refinery";

    /// <summary>Null when the loadout lists no modules, so it cannot say either way.</summary>
    public static MiningFit? For(ShipLoadout loadout)
    {
        if (loadout.Fitted(Refinery) is null)
        {
            return null;
        }

        var miners = loadout.Modules.Where(module => Miners.Any(miner => Starts(module, miner))).ToList();
        var collectors = loadout.Modules.Count(module => Starts(module, Collector));
        var refineries = loadout.Modules.Count(module => Starts(module, Refinery));

        if (miners.Count > 0 && collectors > 0 && refineries > 0)
        {
            var laser = string.Join(" and ", miners.Select(Said).Distinct());

            return new MiningFit(
                true,
                $"carries {laser}, {Counted(collectors, "collector controller")} and {Counted(refineries, "refinery", "refineries")}");
        }

        List<string> missing = [];

        if (miners.Count == 0)
        {
            missing.Add("no mining laser");
        }

        if (collectors == 0)
        {
            missing.Add("no collector controller");
        }

        if (refineries == 0)
        {
            missing.Add("no refinery");
        }

        return new MiningFit(false, string.Join(", ", missing));
    }

    private static bool Starts(ShipModule module, string family) =>
        module.Item.StartsWith(family, StringComparison.OrdinalIgnoreCase);

    private static string Said(ShipModule module) =>
        Knowledge.EliteSpecifications.ModuleName(module.Item) ?? ModuleNames.Readable(module.Item);

    private static string Counted(int count, string singular, string? plural = null) => count switch
    {
        1 => $"a {singular}",
        2 => $"two {plural ?? singular + "s"}",
        3 => $"three {plural ?? singular + "s"}",
        4 => $"four {plural ?? singular + "s"}",
        _ => $"{count} {plural ?? singular + "s"}",
    };
}
