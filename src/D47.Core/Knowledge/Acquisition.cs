using System.Globalization;

namespace D47.Core.Knowledge;

/// <summary>What sort of thing an <see cref="Acquisition"/> describes.</summary>
public enum AcquisitionKind
{
    Ship,
    Module,
    Suit,
    HandWeapon,
    SuitTool,
    SuitModification,
    WeaponModification,
    Material,
    ShipLocker,
    Commodity,
    RareCommodity,
}

/// <summary>One way a Commander comes to hold something.</summary>
public enum AcquisitionMethod
{
    Shipyard,
    Outfitting,
    Market,
    RareMarket,
    PioneerSupplies,
    ComesWithSuit,
    MaterialTrader,
    Bartender,
    RingMining,
    SurfaceMining,
    SurfaceProspecting,
    Settlement,
    Salvage,
    Scanning,
    MissionReward,
    GuardianSite,
    TechBroker,
    Engineer,
    Unobtainable,
}

/// <summary>How one named thing is acquired.</summary>
public sealed record Acquisition
{
    public required string Name { get; init; }

    public required AcquisitionKind Kind { get; init; }

    public IReadOnlyList<AcquisitionMethod> Methods { get; init; } = [];

    /// <summary>What has to be reached or held before any method applies — "a Guardian tech broker".</summary>
    public string? Gate { get; init; }

    public string? Detail { get; init; }
}

/// <summary>How anything a Commander names is acquired, read from the catalogues already in the tree.</summary>
public static class AcquisitionGuide
{
    private static readonly string[] HumanTechBroker =
    [
        "ELITE_HORIZONS_V_PLASMASHOCK_",
        "ELITE_HORIZONS_V_FLECHETTE_",
        "ELITE_HORIZONS_V_CAUSTIC_",
        "ELITE_HORIZONS_V_CORROSIONCARGO_",
        "ELITE_HORIZONS_V_EXPERIMENTALSTABILISER_",
        "ELITE_HORIZONS_V_METAHULL",
    ];

    private static readonly Lazy<Index> Loaded = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private sealed record Index(IReadOnlyList<string> Names, IReadOnlyDictionary<string, Acquisition> ByName);

    private sealed record Gating(IReadOnlyList<AcquisitionMethod> Methods, string? Gate);

    /// <summary>
    /// The named thing, or null. An exact name beats a near one; among exact names a ship wins, then a
    /// module, on-foot equipment, a rare, and a material.
    /// </summary>
    public static Acquisition? For(string spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return null;
        }

        var index = Loaded.Value;

        return Catalogue.Match(index.Names, spoken) is { } name ? index.ByName[name] : null;
    }

    /// <summary>Names from every catalogue close enough to offer back when nothing matched.</summary>
    public static IReadOnlyList<string> Near(string spoken) =>
        string.IsNullOrWhiteSpace(spoken) ? [] : Catalogue.Near(Loaded.Value.Names, spoken);

    /// <summary>Whether a rule here says how a module behind this outfitting gate is acquired.</summary>
    public static bool Places(string? entitlement) => Gated(entitlement) is not null;

    private static Index Load()
    {
        var names = new List<string>();
        var byName = new Dictionary<string, Acquisition>(StringComparer.Ordinal);

        void Add(Acquisition acquisition)
        {
            if (byName.TryAdd(acquisition.Name, acquisition))
            {
                names.Add(acquisition.Name);
            }
        }

        foreach (var ship in EliteSpecifications.Ships)
        {
            Add(new Acquisition { Name = ship.Name, Kind = AcquisitionKind.Ship, Methods = [AcquisitionMethod.Shipyard] });
        }

        foreach (var variants in EliteSpecifications.Modules.GroupBy(module => module.Name, StringComparer.Ordinal))
        {
            Add(Module(variants.Key, [.. variants]));
        }

        var onFoot = OnFootCatalogue.All
            .Where(entry => entry.Kind != OnFootKind.Unknown)
            .GroupBy(entry => entry.Name, StringComparer.Ordinal)
            .Select(same => same.OrderBy(entry => entry.Grade ?? 0).First());

        foreach (var entry in onFoot)
        {
            Add(OnFoot(entry));
        }

        foreach (var rare in RareCatalogue.All)
        {
            Add(new Acquisition
            {
                Name = rare.Name,
                Kind = AcquisitionKind.RareCommodity,
                Methods = [AcquisitionMethod.RareMarket],
                Detail = $"{rare.Station}, {rare.System}",
            });
        }

        var materials = MaterialCatalogue.All
            .GroupBy(entry => entry.Name, StringComparer.Ordinal)
            .Select(same => same.OrderBy(entry => entry.Ledger).First());

        foreach (var material in materials)
        {
            Add(Material(material));
        }

        return new Index(names, byName);
    }

    private static Acquisition Module(string name, IReadOnlyList<ModuleSpecification> variants)
    {
        var gatings = variants.Select(variant => Gated(variant.Entitlement) ?? new Gating([], null)).ToList();
        var methods = gatings.SelectMany(gating => gating.Methods).Distinct().Order().ToList();

        return new Acquisition
        {
            Name = name,
            Kind = AcquisitionKind.Module,
            Methods = methods,
            Gate = gatings.Select(gating => gating.Gate).OfType<string>().FirstOrDefault(),
            Detail = methods.Contains(AcquisitionMethod.TechBroker) ? TechBrokerCost(name) : null,
        };
    }

    private static Gating? Gated(string? entitlement) => entitlement switch
    {
        null or "" or "ELITE_HORIZONS_V_PLANETARY_LANDINGS" => new([AcquisitionMethod.Outfitting], null),
        "removed" => new([AcquisitionMethod.Unobtainable], null),
        "?" => new([], null),
        _ when entitlement.StartsWith("ELITE_HORIZONS_V_GUARDIAN_", StringComparison.Ordinal) =>
            new([AcquisitionMethod.TechBroker], "a Guardian tech broker"),
        _ when HumanTechBroker.Any(prefix => entitlement.StartsWith(prefix, StringComparison.Ordinal)) =>
            new([AcquisitionMethod.TechBroker], "a human tech broker"),
        _ when entitlement.StartsWith("ELITE_SPECIFIC_V_POWER_", StringComparison.Ordinal) =>
            new([AcquisitionMethod.Outfitting], "a Powerplay pledge"),
        _ when entitlement.StartsWith("ELITE_V_", StringComparison.Ordinal) => new([AcquisitionMethod.Outfitting], null),
        _ => null,
    };

    /// <summary>The ingredients of the first tech broker unlock named for this module.</summary>
    private static string? TechBrokerCost(string module) =>
        BlueprintCatalogue.All.FirstOrDefault(blueprint =>
                blueprint.Kind == BlueprintKind.TechBroker
                && blueprint.Name.StartsWith(module, StringComparison.OrdinalIgnoreCase))
            is { Ingredients.Count: > 0 } unlock
            ? Ingredients(unlock)
            : null;

    private static Acquisition OnFoot(OnFootEntry entry) => entry.Kind switch
    {
        OnFootKind.Suit or OnFootKind.Weapon => new Acquisition
        {
            Name = entry.Name,
            Kind = entry.Kind == OnFootKind.Suit ? AcquisitionKind.Suit : AcquisitionKind.HandWeapon,
            Methods = [AcquisitionMethod.PioneerSupplies],
            Detail = OnFootCatalogue.BasePrice(entry) is { } price
                ? $"{price.ToString("N0", CultureInfo.InvariantCulture)} credits"
                : null,
        },

        OnFootKind.Tool => new Acquisition
        {
            Name = entry.Name,
            Kind = AcquisitionKind.SuitTool,
            Methods = [AcquisitionMethod.ComesWithSuit],
            Detail = SuitsCarrying(entry.Name),
        },

        _ => new Acquisition
        {
            Name = entry.Name,
            Kind = entry.Kind == OnFootKind.SuitModification
                ? AcquisitionKind.SuitModification
                : AcquisitionKind.WeaponModification,
            Methods = [AcquisitionMethod.Engineer],
            Detail = Recipes(entry.Symbol),
        },
    };

    /// <summary>The suits whose own row lists this tool.</summary>
    private static string? SuitsCarrying(string tool)
    {
        var suits = OnFootCatalogue.All
            .Where(entry => entry.Kind == OnFootKind.Suit
                            && entry.Detail is { } detail
                            && detail.Split(',').Any(part =>
                                string.Equals(part.Trim(), tool, StringComparison.OrdinalIgnoreCase)))
            .Select(entry => entry.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return suits.Count > 0 ? string.Join(", ", suits) : null;
    }

    /// <summary>Each recipe for a modification: who offers it and what it costs.</summary>
    private static string? Recipes(string symbol)
    {
        var recipes = OnFootCatalogue.RecipesFor(symbol);

        return recipes.Count == 0
            ? null
            : string.Join("; ", recipes.Select(recipe => recipe.Engineers.Count > 0
                ? $"{recipe.Name} from {string.Join(" or ", recipe.Engineers)}: {Ingredients(recipe)}"
                : $"{recipe.Name}: {Ingredients(recipe)}"));
    }

    private static string Ingredients(Blueprint blueprint) =>
        string.Join(
            ", ",
            blueprint.Ingredients.Select(ingredient =>
                $"{ingredient.Size} × {ingredient.Material?.Name ?? ingredient.Symbol}"));

    private static Acquisition Material(MaterialEntry entry)
    {
        var methods = new List<AcquisitionMethod>();

        switch (entry.Ledger)
        {
            case MaterialLedger.Material when entry.Line is not null:
                methods.Add(AcquisitionMethod.MaterialTrader);
                break;

            case MaterialLedger.ShipLocker:
                if (entry.Settlements.Count > 0 || entry.Buildings.Count > 0)
                {
                    methods.Add(AcquisitionMethod.Settlement);
                }

                if (entry.BarterCost is not null)
                {
                    methods.Add(AcquisitionMethod.Bartender);
                }

                break;

            case MaterialLedger.Cargo
                when BodyCatalogue.MatchRingSignal(entry.Name) is { } ring
                     && string.Equals(ring, entry.Name, StringComparison.OrdinalIgnoreCase):
                methods.Add(AcquisitionMethod.RingMining);
                break;
        }

        foreach (var method in entry.Methods)
        {
            if (!methods.Contains(method))
            {
                methods.Add(method);
            }
        }

        return new Acquisition
        {
            Name = entry.Name,
            Kind = entry.Ledger switch
            {
                MaterialLedger.Material => AcquisitionKind.Material,
                MaterialLedger.ShipLocker => AcquisitionKind.ShipLocker,
                MaterialLedger.RareCargo => AcquisitionKind.RareCommodity,
                _ => AcquisitionKind.Commodity,
            },
            Methods = methods,
            Detail = entry.Origins.Count > 0 ? string.Join("; ", entry.Origins) : null,
        };
    }
}
