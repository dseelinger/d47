using System.Text.Json;

namespace D47.Core.Journal;

/// <summary>One fitted module, as the Loadout event describes it.</summary>
public sealed record ShipModule(string Slot, string Item, bool Powered, int? Health, long? Value)
{
    /// <summary>The engineering blueprint applied, or null for an unmodified module.</summary>
    public string? Blueprint { get; init; }

    public int? BlueprintLevel { get; init; }

    /// <summary>The experimental effect, which Elite reports separately from the blueprint.</summary>
    public string? Experimental { get; init; }

    public bool IsEngineered => Blueprint is not null;

    public static ShipModule From(JsonElement element) => new(
        element.String("Slot") ?? "unknown",
        element.Named("Item") ?? "unknown",
        Powered: element.Bool("On"),
        Health: element.Double("Health") is { } health ? (int)Math.Round(health * 100) : null,
        Value: element.Long("Value"))
    {
        Blueprint = element.Object("Engineering")?.Named("BlueprintName"),
        BlueprintLevel = element.Object("Engineering")?.Int("Level"),
        Experimental = element.Object("Engineering")?.Named("ExperimentalEffect"),
    };
}

/// <summary>
/// The ship the Commander is flying, and what the game says about it (list.md Phase 7,
/// "Ship's loadout" and "Ship metrics").
/// <para>
/// <b>Every number here is reported by the Loadout event, not computed from a table of ship
/// specifications.</b> That is the checklist's wording and it is the right constraint: a spec
/// table is a second source of truth that goes stale every balance pass, and it cannot know
/// about engineering, which is precisely what makes one Commander's Anaconda differ from
/// another's. MaxJumpRange out of Loadout already accounts for the modules actually fitted.
/// </para>
/// </summary>
public sealed record ShipLoadout
{
    public static readonly ShipLoadout Unknown = new();

    /// <summary>The internal ship symbol — "Anaconda", "Krait_MkII". Speak <see cref="TypeName"/>.</summary>
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

    /// <summary>Percent. Loadout reports a 0-1 fraction; this is the readable form of it.</summary>
    public int? HullHealth { get; init; }

    public double? UnladenMass { get; init; }

    public int? CargoCapacity { get; init; }

    public double? FuelCapacity { get; init; }

    public double? ReserveCapacity { get; init; }

    /// <summary>
    /// Light years on a full tank with an empty hold, as Loadout reports it. This is the "base
    /// jump range" the checklist asks for: the game has already done the mass-and-FSD
    /// arithmetic, and redoing it here would only be a way to disagree with it.
    /// </summary>
    public double? MaxJumpRange { get; init; }

    public IReadOnlyList<ShipModule> Modules { get; init; } = [];

    public bool IsKnown => Type is not null;

    /// <summary>Modules with a blueprint applied. What a Commander means by "my engineering".</summary>
    public IReadOnlyList<ShipModule> Engineered =>
        [.. Modules.Where(module => module.IsEngineered)];

    /// <summary>
    /// Fitted but unpowered. Worth being able to answer directly, because an unpowered module
    /// is a module the Commander believes they have.
    /// </summary>
    public IReadOnlyList<ShipModule> Unpowered =>
        [.. Modules.Where(module => !module.Powered)];

    /// <summary>
    /// Hull plus modules. Loadout reports the two separately and never their sum, so this is
    /// arithmetic on reported values rather than a figure from anywhere else.
    /// </summary>
    public long? TotalValue => HullValue is { } hull && ModulesValue is { } modules
        ? hull + modules
        : null;

    /// <summary>
    /// How the Commander refers to the ship: the name they gave it where there is one, and the
    /// type otherwise.
    /// </summary>
    public string? Describe() => (Name, TypeName ?? Type) switch
    {
        ({ } name, { } type) => $"{name}, a {type}",
        (null, { } type) => type,
        ({ } name, null) => name,
        _ => null,
    };

    public ShipLoadout Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        // The whole picture, rewritten from scratch. Loadout fires on every change that matters
        // — outfitting, module swap, ship swap — so folding it rather than replacing it would
        // leave modules from the previous ship in the list.
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

        // Renaming does not re-emit Loadout, so without this the ship would keep answering to
        // the name it had when it was last outfitted.
        "SetUserShipName" => this with
        {
            Name = Blank(journalEvent.String("UserShipName")) ?? Name,
            // Both spellings. Elite is not consistent about the casing of this field, and it
            // uses "ShipID" for the numeric id in the same event — so a single guess here is a
            // rename that silently does not take.
            Ident = Blank(journalEvent.String("UserShipId"))
                    ?? Blank(journalEvent.String("UserShipID"))
                    ?? Ident,
        },

        _ => this,
    };

    /// <summary>
    /// Elite writes an empty string for a ship that has never been named. Null is what the rest
    /// of this type means by "not named", and the two must not both be in circulation.
    /// </summary>
    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
