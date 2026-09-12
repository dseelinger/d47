using System.Text.Json;

namespace D47.Core.Journal;

/// <summary>One line of a module's engineering, in real units.</summary>
public sealed record ShipModifier(string Label)
{
    /// <summary>The modified figure, for the modifiers that carry a number.</summary>
    public double? Value { get; init; }

    /// <summary>The same figure before engineering, so the change is arithmetic and not a claim.</summary>
    public double? OriginalValue { get; init; }

    /// <summary>
    /// The value for modifiers that are not numeric at all — a damage type reading "Thermal" rather
    /// than a quantity.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>Whether a smaller number is the better one — true for mass, heat and power draw.</summary>
    public bool LessIsGood { get; init; }

    /// <summary>How far engineering moved it, or null when there is nothing to subtract.</summary>
    public double? Change => Value is { } value && OriginalValue is { } original ? value - original : null;

    /// <summary>
    /// Attributes whose <see cref="Value"/> is already a percentage, so the proportional reading every
    /// other modifier gets is nonsense for them (#53).
    /// </summary>
    private static readonly HashSet<string> AlreadyPercentages = new(StringComparer.OrdinalIgnoreCase)
    {
        "KineticResistance",
        "ThermicResistance",
        "ExplosiveResistance",
        "CausticResistance",
        "DefenceModifierHealthMultiplier",
    };

    /// <inheritdoc cref="AlreadyPercentages"/>
    public bool IsPercentage => AlreadyPercentages.Contains(Label);

    /// <summary>
    /// Whether the change is an improvement, or null when it did not move or cannot be measured.
    /// </summary>
    public bool? IsImprovement => Change switch
    {
        null => null,
        > 0 => !LessIsGood,
        < 0 => LessIsGood,
        _ => null,
    };

    public static ShipModifier From(JsonElement element) => new(element.Named("Label") ?? "unknown")
    {
        Value = element.Double("Value"),
        OriginalValue = element.Double("OriginalValue"),

        // ValueStr for the non-numeric variant, and String("Value") behind it for a Value that arrives as a
        // string — which Double() answers null for, so without this the modifier would read as present and
        // empty rather than as the text it is.
        Text = element.Named("ValueStr") ?? element.String("Value"),

        // Number first, because that is what Elite actually writes.
        LessIsGood = element.Int("LessIsGood") == 1 || element.Bool("LessIsGood"),
    };
}

/// <summary>One fitted module, as the Loadout event describes it.</summary>
public sealed record ShipModule(string Slot, string Item, bool Powered, int? Health, long? Value)
{
    /// <summary>The engineering blueprint applied, or null for an unmodified module.</summary>
    public string? Blueprint { get; init; }

    public int? BlueprintLevel { get; init; }

    /// <summary>The experimental effect, which Elite reports separately from the blueprint.</summary>
    public string? Experimental { get; init; }

    /// <summary>
    /// The unlocalised symbol behind <see cref="Experimental"/> — what <see
    /// cref="Knowledge.BlueprintCatalogue.NameOf"/> actually joins against, since Elite localises the
    /// display name and the catalogue never does.
    /// </summary>
    public string? ExperimentalSymbol { get; init; }

    /// <summary>
    /// Progress through the grade, 0 to 1. 0.85 and above is finished, not 1.0 — see <see
    /// cref="Knowledge.EngineeringRules.CompleteAt"/>, which carries the evidence.
    /// </summary>
    public double? Quality { get; init; }

    /// <summary>Who rolled it, where Elite names them.</summary>
    public string? Engineer { get; init; }

    public long? EngineerId { get; init; }

    /// <summary>What the roll actually did, in real units.</summary>
    public IReadOnlyList<ShipModifier> Modifiers { get; init; } = [];

    public bool IsEngineered => Blueprint is not null;

    public static ShipModule From(JsonElement element)
    {
        var engineering = element.Object("Engineering");

        return new ShipModule(
            element.String("Slot") ?? "unknown",
            element.Named("Item") ?? "unknown",
            Powered: element.Bool("On"),
            Health: element.Double("Health") is { } health ? (int)Math.Round(health * 100) : null,
            Value: element.Long("Value"))
        {
            // "BlueprintName" is the current spelling. "Blueprint" is what journals written before 3.0 used;
            // there are zero of them in the 912-journal corpus, which starts seven years after that, so this
            // is carried on EDDiscovery's authority rather than on evidence and costs one fallback.
            Blueprint = engineering?.Named("BlueprintName") ?? engineering?.Named("Blueprint"),
            BlueprintLevel = engineering?.Int("Level"),

            // Loadout writes "ExperimentalEffect" and EngineerCraft writes "ApplyExperimentalEffect" for the
            // same thing.
            Experimental = Blank(engineering?.Named("ExperimentalEffect")
                                 ?? engineering?.Named("ApplyExperimentalEffect")),
            ExperimentalSymbol = Blank(engineering?.String("ExperimentalEffect")
                                       ?? engineering?.String("ApplyExperimentalEffect")),

            Quality = engineering?.Double("Quality"),
            Engineer = Blank(engineering?.String("Engineer")),
            EngineerId = engineering?.Long("EngineerID"),
            Modifiers = engineering is { } block
                ? [.. block.Items("Modifiers").Select(ShipModifier.From)]
                : [],
        };
    }

    /// <summary>Empty means absent everywhere in this file, and the two must not both circulate.</summary>
    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>
/// The ship the Commander is flying, and what the game says about it (Phase 7, "Ship's loadout" and
/// "Ship metrics").
/// </summary>
public sealed record ShipLoadout
{
    public static readonly ShipLoadout Unknown = new();

    /// <summary>The internal ship symbol — "Anaconda", "Krait_MkII".</summary>
    public string? Type { get; init; }

    /// <summary>The player-facing ship name where Elite localises it.</summary>
    public string? TypeName { get; init; }

    /// <summary>What the Commander christened it.</summary>
    public string? Name { get; init; }

    /// <summary>The ship ident painted on the hull.</summary>
    public string? Ident { get; init; }

    public int? ShipId { get; init; }

    public long? HullValue { get; init; }

    public long? ModulesValue { get; init; }

    public long? Rebuy { get; init; }

    /// <summary>Percent.</summary>
    public int? HullHealth { get; init; }

    public double? UnladenMass { get; init; }

    public int? CargoCapacity { get; init; }

    public double? FuelCapacity { get; init; }

    public double? ReserveCapacity { get; init; }

    /// <summary>Light years on a full tank with an empty hold, as Loadout reports it.</summary>
    public double? MaxJumpRange { get; init; }

    public IReadOnlyList<ShipModule> Modules { get; init; } = [];

    public bool IsKnown => Type is not null;

    /// <summary>Modules with a blueprint applied.</summary>
    public IReadOnlyList<ShipModule> Engineered =>
        [.. Modules.Where(module => module.IsEngineered)];

    /// <summary>Fitted but unpowered.</summary>
    public IReadOnlyList<ShipModule> Unpowered =>
        [.. Modules.Where(module => !module.Powered)];

    /// <summary>The Fuel Scoop symbol family.</summary>
    public const string FuelScoop = "int_fuelscoop";

    /// <summary>The Docking Computer symbol family, which matches the advanced variant as well.</summary>
    public const string DockingComputer = "int_dockingcomputer";

    /// <summary>
    /// Whether a module from a symbol family is fitted — <c>int_fuelscoop</c>,
    /// <c>int_shieldgenerator</c>, <c>int_dronecontrol_collection</c> — or null when this loadout
    /// cannot answer.
    /// </summary>
    public bool? Fitted(string family) =>
        IsKnown && Modules.Count > 0
            ? Modules.Any(module => module.Item.StartsWith(family, StringComparison.OrdinalIgnoreCase))
            : null;

    /// <summary>Hull plus modules.</summary>
    public long? TotalValue => HullValue is { } hull && ModulesValue is { } modules
        ? hull + modules
        : null;

    /// <summary>
    /// How the Commander refers to the ship: the name they gave it where there is one, and the type
    /// otherwise.
    /// </summary>
    public string? Describe() => (Name, TypeSaid) switch
    {
        ({ } name, { } type) => $"{name}, a {type}",
        (null, { } type) => type,
        ({ } name, null) => name,
        _ => null,
    };

    /// <summary>
    /// The hull as it should be said: Frontier's localised name where they sent one, then the
    /// specification table, and only then the raw symbol.
    /// </summary>
    public string? TypeSaid =>
        Type is not { } symbol ? TypeName
        : TypeName is { } localised && !string.Equals(localised, symbol, StringComparison.Ordinal)
            ? localised
            : Knowledge.EliteSpecifications.HullSaid(symbol);

    public ShipLoadout Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        // The whole picture, rewritten from scratch.
        "Loadout" => new ShipLoadout
        {
            Type = journalEvent.String("Ship"),
            TypeName = journalEvent.Named("Ship"),
            Name = Blank(journalEvent.String("ShipName")),
            Ident = Blank(journalEvent.String("ShipIdent")),
            ShipId = journalEvent.Int("ShipID"),
            HullValue = journalEvent.Long("HullValue"),
            ModulesValue = journalEvent.Long("ModulesValue"),
            Rebuy = journalEvent.Long("Rebuy"),
            HullHealth = journalEvent.Double("HullHealth") is { } health
                ? (int)Math.Round(health * 100)
                : null,
            UnladenMass = journalEvent.Double("UnladenMass"),
            CargoCapacity = journalEvent.Int("CargoCapacity"),
            FuelCapacity = journalEvent.Object("FuelCapacity")?.Double("Main"),
            ReserveCapacity = journalEvent.Object("FuelCapacity")?.Double("Reserve"),
            MaxJumpRange = journalEvent.Double("MaxJumpRange"),
            Modules = [.. journalEvent.Items("Modules").Select(ShipModule.From)],
        },

        // Renaming does not re-emit Loadout, so without this the ship would keep answering to the name it had
        // when it was last outfitted.
        "SetUserShipName" => this with
        {
            Name = Blank(journalEvent.String("UserShipName")) ?? Name,
            // Both spellings.
            Ident = Blank(journalEvent.String("UserShipId"))
                    ?? Blank(journalEvent.String("UserShipID"))
                    ?? Ident,
        },

        // Engineering is the change that matters and does not re-emit Loadout.
        "EngineerCraft" => Rolled(journalEvent),

        // The id alone, and only while no Loadout has been read (<a
        // href=".com/dseelinger/d47/issues/337">#337</a>).
        "LoadGame" when !IsKnown => this with { ShipId = journalEvent.Int("ShipID") ?? ShipId },

        _ => this,
    };

    /// <summary>One slot, rewritten from the roll that just landed.</summary>
    private ShipLoadout Rolled(JournalEvent journalEvent)
    {
        if (Blank(journalEvent.String("Slot")) is not { } slot)
        {
            return this;
        }

        var index = Modules.ToList()
            .FindIndex(module => string.Equals(module.Slot, slot, StringComparison.OrdinalIgnoreCase));

        // A slot this loadout has never listed.
        if (index < 0)
        {
            return this;
        }

        var existing = Modules[index];
        var modifiers = journalEvent.Items("Modifiers").Select(ShipModifier.From).ToList();

        var rolled = existing with
        {
            Blueprint = journalEvent.Named("BlueprintName") ?? existing.Blueprint,
            BlueprintLevel = journalEvent.Int("Level") ?? existing.BlueprintLevel,
            Quality = journalEvent.Double("Quality") ?? existing.Quality,

            // Localised first, exactly as the Loadout path does it — a plan names "Fast Charge" and Elite
            // localises this one field.
            Experimental = Blank(journalEvent.Named("ExperimentalEffect")
                                 ?? journalEvent.Named("ApplyExperimentalEffect"))
                           ?? existing.Experimental,
            ExperimentalSymbol = Blank(journalEvent.String("ExperimentalEffect")
                                       ?? journalEvent.String("ApplyExperimentalEffect"))
                                  ?? existing.ExperimentalSymbol,

            Engineer = Blank(journalEvent.String("Engineer")) ?? existing.Engineer,
            EngineerId = journalEvent.Long("EngineerID") ?? existing.EngineerId,
            Modifiers = modifiers.Count > 0 ? modifiers : existing.Modifiers,
        };

        return this with { Modules = [.. Modules.Select((module, at) => at == index ? rolled : module)] };
    }

    /// <summary>Elite writes an empty string for a ship that has never been named.</summary>
    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
