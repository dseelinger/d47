namespace D47.Core.Journal;

/// <summary>What one hull's fighter hangar has been stocked with since it was fitted.</summary>
/// <param name="Nomad">The Nomad was stocked.</param>
/// <param name="Fighter">A fighter was stocked.</param>
/// <param name="Other">A vehicle d47 does not know was stocked.</param>
public sealed record HangarHolds(bool Nomad, bool Fighter, bool Other = false)
{
    /// <summary>Nothing stocked since the hangar was fitted.</summary>
    public static readonly HangarHolds NotKnown = new(false, false);

    /// <summary>The hangar holds the Nomad and nothing else.</summary>
    public bool NomadOnly => Nomad && !Fighter && !Other;

    /// <summary>Everything either side has seen stocked.</summary>
    public HangarHolds Or(HangarHolds other) => new(Nomad || other.Nomad, Fighter || other.Fighter, Other || other.Other);
}

/// <summary>Each hull's fighter hangar contents, keyed by <c>ShipID</c>, folded from <c>RestockVehicle</c>.</summary>
public sealed record ShipHangars
{
    private const string NomadVessel = "lander01";

    private const string Hangar = "int_fighterbay";

    private static readonly string[] SurfaceVehicles = ["testbuggy", "combat_multicrew_srv_01", "mev_rhino"];

    private static readonly string[] ItemFields =
        ["BuyItem", "SellItem", "StoredItem", "ReplacementItem", "FromItem", "ToItem", "RetrievedItem", "SwapOutItem"];

    public static readonly ShipHangars Empty = new();

    /// <summary>Every hull with a recorded entry, a cleared one included.</summary>
    public IReadOnlyDictionary<int, HangarHolds> Hulls { get; init; } = new Dictionary<int, HangarHolds>();

    public bool Any => Hulls.Count > 0;

    /// <summary>The hull's hangar contents, or null when none is recorded.</summary>
    public HangarHolds? For(int shipId) => Hulls.GetValueOrDefault(shipId);

    /// <summary>Folds one event. A <c>RestockVehicle</c> names no hull and applies to <paramref name="currentShipId"/>.</summary>
    public ShipHangars Apply(JournalEvent journalEvent, int? currentShipId) => journalEvent.Kind switch
    {
        "RestockVehicle" when currentShipId is { } shipId => Restocked(shipId, journalEvent.String("Type")),

        "ModuleBuy" or "ModuleSell" or "ModuleStore" or "ModuleSwap" or "ModuleRetrieve"
            when journalEvent.Int("ShipID") is { } shipId && NamesAHangar(journalEvent) => Set(shipId, HangarHolds.NotKnown),

        _ => this,
    };

    /// <summary>
    /// These hulls with <paramref name="live"/> laid over them. A hull in both keeps what either saw stocked,
    /// unless the live one was refitted since.
    /// </summary>
    public ShipHangars With(ShipHangars live)
    {
        if (!live.Any)
        {
            return this;
        }

        var hulls = new Dictionary<int, HangarHolds>(Hulls);

        foreach (var (shipId, holds) in live.Hulls)
        {
            hulls[shipId] = holds != HangarHolds.NotKnown && For(shipId) is { } walked ? walked.Or(holds) : holds;
        }

        return this with { Hulls = hulls };
    }

    private ShipHangars Restocked(int shipId, string? type)
    {
        if (type is null || SurfaceVehicles.Contains(type, StringComparer.OrdinalIgnoreCase))
        {
            return this;
        }

        var was = For(shipId) ?? HangarHolds.NotKnown;

        var now = string.Equals(type, NomadVessel, StringComparison.OrdinalIgnoreCase) ? was with { Nomad = true }
            : type.Contains("fighter", StringComparison.OrdinalIgnoreCase) ? was with { Fighter = true }
            : was with { Other = true };

        return Set(shipId, now);
    }

    private ShipHangars Set(int shipId, HangarHolds holds)
    {
        if (For(shipId) == holds)
        {
            return this;
        }

        return this with { Hulls = new Dictionary<int, HangarHolds>(Hulls) { [shipId] = holds } };
    }

    private static bool NamesAHangar(JournalEvent journalEvent) =>
        ItemFields.Any(field => journalEvent.String(field) is { } item
            && item.TrimStart('$').StartsWith(Hangar, StringComparison.OrdinalIgnoreCase));
}
