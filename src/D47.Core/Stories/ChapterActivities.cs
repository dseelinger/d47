using D47.Core.Adventures;
using D47.Core.Journal;

namespace D47.Core.Stories;

/// <summary>The Commander's careers, as the <c>Statistics</c> event groups them with Smuggling folded into Crime.</summary>
public enum Career
{
    Combat,
    Trading,
    Mining,
    Exploration,
    Exobiology,
    Passengers,
    SearchAndRescue,
    Crime,
}

/// <summary>What the Commander and the ship aboard must have for an activity to be drawn.</summary>
[Flags]
public enum ActivityGate
{
    None = 0,
    MiningKit = 1,
    SurfaceScanner = 2,
    BuggyBay = 4,
    Cabin = 8,
    Cargo = 16,
    Odyssey = 32,
    Danger = 64,
    SoldierDanger = 128,
}

/// <summary>One drawable activity: a trigger kind, the mission family for a mission, its career, gates and count range.</summary>
public sealed record DrawableActivity(
    TriggerKind Kind,
    string? Family,
    Career Career,
    ActivityGate Gates,
    int Min,
    int Max,
    bool PlaceBefore = false,
    bool? Organic = null)
{
    /// <summary>The key the activity is refused and remembered under.</summary>
    public string? Key => RefusedActivities.Key(Kind, Family);

    /// <summary>Whether the count is tons capped at the cargo capacity.</summary>
    public bool IsTons => Kind is TriggerKind.Sell or TriggerKind.Mine;

    /// <summary>Whether the count is credits, in steps of <see cref="ChapterActivities.CreditStep"/>.</summary>
    public bool IsCredits => Kind == TriggerKind.DataSale;
}

/// <summary>The table <see cref="ChapterDraw"/> draws from, and the gates it applies.</summary>
public static class ChapterActivities
{
    public const int CreditStep = 100_000;

    public const int CombatRankNeeded = 3;

    public const int EngineeredModulesNeeded = 3;

    public const int SoldierRankNeeded = 2;

    private static readonly string[] MiningWeapons = ["hpt_mininglaser", "hpt_mining_", "hpt_miningtoolv2"];

    private static readonly string[] CargoMissions = ["Mission_Delivery", "Mission_Collect", "Mission_Mining", "Mission_Smuggle", "Mission_Piracy"];

    private static readonly string[] CombatMissions = ["Mission_Massacre", "Mission_Assassinate"];

    private static readonly string[] OnFootCombat = ["Massacre", "Onslaught", "Assassination", "Defence"];

    private static readonly string[] Passenger = ["Mission_PassengerBulk", "Mission_PassengerVIP", "Mission_Sightseeing", "Mission_LongDistanceExpedition"];

    /// <summary>Crime families, besides every <see cref="MissionFamilies.IsIllegal"/> family.</summary>
    private static readonly string[] Crimes =
    [
        "Mission_Smuggle", "Mission_OnFoot_Smuggle", "Mission_Piracy", "Mission_OnFoot_Heist", "Mission_OnFoot_ProductionHeist", "Mission_OnFoot_Sabotage",
    ];

    /// <summary>Every drawable activity, in table order.</summary>
    public static IReadOnlyList<DrawableActivity> All { get; } = Build();

    /// <summary>The gates the Commander and the ship aboard pass.</summary>
    public static ActivityGate Held(RankState ranks, ShipLoadout ship, bool odyssey)
    {
        ArgumentNullException.ThrowIfNull(ranks);
        ArgumentNullException.ThrowIfNull(ship);

        var held = ActivityGate.None;

        if (ship.Modules.Any(module => MiningWeapons.Any(weapon => Fitted(module, weapon))) && ship.Modules.Any(module => Fitted(module, "int_refinery")))
        {
            held |= ActivityGate.MiningKit;
        }

        if (ship.Modules.Any(module => Fitted(module, "int_detailedsurfacescanner")))
        {
            held |= ActivityGate.SurfaceScanner;
        }

        if (ship.Modules.Any(module => Fitted(module, "int_buggybay")))
        {
            held |= ActivityGate.BuggyBay;
        }

        if (ship.Modules.Any(module => Fitted(module, "int_passengercabin") || Fitted(module, "int_mkii_passengercabin")))
        {
            held |= ActivityGate.Cabin;
        }

        if (ship.CargoCapacity > 0)
        {
            held |= ActivityGate.Cargo;
        }

        if (odyssey)
        {
            held |= ActivityGate.Odyssey;
        }

        if ((ranks.For("Combat")?.Rank ?? 0) >= CombatRankNeeded && ship.Modules.Count(module => module.Blueprint is not null) >= EngineeredModulesNeeded)
        {
            held |= ActivityGate.Danger;
        }

        if ((ranks.For("Soldier")?.Rank ?? 0) >= SoldierRankNeeded)
        {
            held |= ActivityGate.SoldierDanger;
        }

        return held;
    }

    /// <summary>The row an objective of this kind, mission family and organic flag is drawn from, or null for a place or milestone objective.</summary>
    public static DrawableActivity? Find(TriggerKind kind, string? family, bool? organic) =>
        All.FirstOrDefault(row =>
            row.Kind == kind
            && (row.Family is null || string.Equals(row.Family, family?.Trim(), StringComparison.OrdinalIgnoreCase))
            && (row.Organic is null || organic is null || row.Organic == organic));

    /// <summary>Whether the row can only be done at a station or in a populated system: bounty, both bonds, conflict, sell, data sale, rescue and every mission.</summary>
    public static bool NeedsStation(DrawableActivity activity) =>
        activity.Kind is TriggerKind.Bounty or TriggerKind.Bond or TriggerKind.OnFootBond or TriggerKind.Conflict or TriggerKind.Sell
            or TriggerKind.DataSale or TriggerKind.Rescue or TriggerKind.Mission;

    /// <summary>Whether every gate named in a step event row's <c>needs</c> is in <paramref name="held"/>; a name that is no gate is never held.</summary>
    public static bool Passes(IReadOnlyList<string> needs, ActivityGate held) =>
        needs.All(name => Enum.TryParse<ActivityGate>(name, out var gate) && gate != ActivityGate.None && Enum.IsDefined(gate) && (gate & ~held) == ActivityGate.None);

    /// <summary>Whether the row's gates are all in <paramref name="held"/>.</summary>
    public static bool Passes(DrawableActivity activity, ActivityGate held) => (activity.Gates & ~held) == ActivityGate.None;

    private static bool Fitted(ShipModule module, string prefix) => module.Item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static List<DrawableActivity> Build()
    {
        List<DrawableActivity> rows =
        [
            new(TriggerKind.Bounty, null, Career.Combat, ActivityGate.None, 3, 8),
            new(TriggerKind.Bond, null, Career.Combat, ActivityGate.Danger, 5, 15),
            new(TriggerKind.OnFootBond, null, Career.Combat, ActivityGate.Odyssey, 5, 15),
            new(TriggerKind.Conflict, null, Career.Combat, ActivityGate.Danger, 1, 2, PlaceBefore: true),
            new(TriggerKind.Sell, null, Career.Trading, ActivityGate.Cargo, 10, 40),
            new(TriggerKind.Mine, null, Career.Mining, ActivityGate.MiningKit | ActivityGate.Cargo, 10, 40),
            new(TriggerKind.Map, null, Career.Exploration, ActivityGate.SurfaceScanner, 2, 5),
            new(TriggerKind.Signal, null, Career.Exploration, ActivityGate.None, 2, 4),
            new(TriggerKind.Codex, null, Career.Exploration, ActivityGate.None, 3, 6),
            new(TriggerKind.NewCodexPicture, null, Career.Exploration, ActivityGate.None, 1, 1),
            new(TriggerKind.CodexPicture, null, Career.Exploration, ActivityGate.None, 1, 1),
            new(TriggerKind.Wreck, null, Career.Exploration, ActivityGate.None, 1, 2),
            new(TriggerKind.Uss, null, Career.Exploration, ActivityGate.None, 2, 4),
            new(TriggerKind.Srv, null, Career.Exploration, ActivityGate.BuggyBay, 1, 3),
            new(TriggerKind.Boost, null, Career.Exploration, ActivityGate.None, 1, 2),
            new(TriggerKind.Discover, null, Career.Exploration, ActivityGate.None, 3, 8),
            new(TriggerKind.Footfall, null, Career.Exploration, ActivityGate.Odyssey, 1, 3),
            new(TriggerKind.DataSale, null, Career.Exploration, ActivityGate.None, 1_000_000, 5_000_000, Organic: false),
            new(TriggerKind.Organic, null, Career.Exobiology, ActivityGate.Odyssey, 3, 6),
            new(TriggerKind.DataSale, null, Career.Exobiology, ActivityGate.Odyssey, 1_000_000, 5_000_000, Organic: true),
            new(TriggerKind.Rescue, null, Career.SearchAndRescue, ActivityGate.None, 1, 4),
            new(TriggerKind.Salvage, null, Career.SearchAndRescue, ActivityGate.Cargo, 2, 6),
        ];

        foreach (var family in CombatMissions)
        {
            rows.Add(Mission(family, Career.Combat));
        }

        foreach (var family in OnFootCombat)
        {
            rows.Add(Mission("Mission_OnFoot_" + family, Career.Combat));
        }

        foreach (var family in new[] { "Mission_Courier", "Mission_Delivery", "Mission_Collect", "Mission_OnFoot_Delivery", "Mission_OnFoot_Collect" })
        {
            rows.Add(Mission(family, Career.Trading));
        }

        rows.Add(Mission("Mission_Mining", Career.Mining));

        foreach (var family in Passenger)
        {
            rows.Add(Mission(family, Career.Passengers));
        }

        foreach (var family in new[] { "Mission_Rescue", "Mission_Salvage", "Mission_OnFoot_Salvage", "Mission_OnFoot_Reboot", "Mission_OnFoot_RebootRestore" })
        {
            rows.Add(Mission(family, Career.SearchAndRescue));
        }

        foreach (var family in Crimes.Concat(MissionFamilies.Offered.Where(MissionFamilies.IsIllegal)).Distinct(StringComparer.Ordinal))
        {
            rows.Add(Mission(family, Career.Crime));
        }

        return rows;
    }

    private static DrawableActivity Mission(string family, Career career)
    {
        var gates = ActivityGate.None;
        var onFoot = family.StartsWith("Mission_OnFoot_", StringComparison.Ordinal);

        if (family == "Mission_Mining")
        {
            gates |= ActivityGate.MiningKit;
        }

        if (Passenger.Contains(family))
        {
            gates |= ActivityGate.Cabin;
        }

        if (CargoMissions.Contains(family))
        {
            gates |= ActivityGate.Cargo;
        }

        if (onFoot)
        {
            gates |= ActivityGate.Odyssey;
        }

        if (CombatMissions.Contains(family))
        {
            gates |= ActivityGate.Danger;
        }
        else if (onFoot && OnFootCombat.Any(word => family.Contains(word, StringComparison.Ordinal)))
        {
            gates |= ActivityGate.SoldierDanger;
        }

        return new DrawableActivity(TriggerKind.Mission, family, career, gates, 2, 4, PlaceBefore: career == Career.Crime);
    }
}
