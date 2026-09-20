namespace D47.Core.Journal;

/// <summary>One suit the Commander owns, keyed on SuitID.</summary>
public sealed record OwnedSuit(
    long SuitId,
    string Symbol,
    int? Grade,
    IReadOnlyList<FittedModification> Modifications,
    DateTimeOffset SeenAt);

/// <summary>One hand weapon the Commander owns, keyed on SuitModuleID.</summary>
public sealed record OwnedWeapon(
    long ModuleId,
    string Symbol,
    int? Grade,
    IReadOnlyList<FittedModification> Modifications,
    DateTimeOffset SeenAt);

/// <summary>
/// Every suit and hand weapon the Commander owns, keyed on SuitID / SuitModuleID — Elite writes no
/// snapshot of this, only the event stream (#292). <see cref="ShipLoadouts"/> is the ship-side
/// equivalent this mirrors.
/// </summary>
public sealed record OwnedKit
{
    public static readonly OwnedKit Empty = new();

    /// <summary>The starting suit, free and unmodifiable, so it is not recorded as owned.</summary>
    private const string FlightSuit = "flightsuit";

    public IReadOnlyDictionary<long, OwnedSuit> Suits { get; init; } = new Dictionary<long, OwnedSuit>();

    public IReadOnlyDictionary<long, OwnedWeapon> Weapons { get; init; } = new Dictionary<long, OwnedWeapon>();

    public bool IsKnown => Suits.Count > 0 || Weapons.Count > 0;

    /// <summary>
    /// The event kinds that can change what is owned, so a caller deciding whether to write the file
    /// asks this rather than keeping a second copy of the list (#293).
    /// </summary>
    public static bool MayChange(JournalEvent journalEvent) =>
        journalEvent is not null && journalEvent.Kind
            is "BuySuit" or "BuyWeapon" or "SellSuit" or "SellWeapon" or "UpgradeSuit" or "UpgradeWeapon"
            or "SuitLoadout" or "SwitchSuitLoadout" or "CreateSuitLoadout" or "LoadoutEquipModule" or "NewCommander";

    /// <summary>These suits and weapons with <paramref name="newer"/>'s written over them, matched on id.</summary>
    public OwnedKit With(OwnedKit newer)
    {
        ArgumentNullException.ThrowIfNull(newer);

        if (newer.Suits.Count == 0 && newer.Weapons.Count == 0)
        {
            return this;
        }

        var suits = new Dictionary<long, OwnedSuit>(Suits);

        foreach (var (id, suit) in newer.Suits)
        {
            suits[id] = suit;
        }

        var weapons = new Dictionary<long, OwnedWeapon>(Weapons);

        foreach (var (id, weapon) in newer.Weapons)
        {
            weapons[id] = weapon;
        }

        return this with { Suits = suits, Weapons = weapons };
    }

    public OwnedKit Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        "BuySuit" => Bought(journalEvent),

        "BuyWeapon" => BoughtWeapon(journalEvent),

        "SellSuit" => journalEvent.Long("SuitID") is { } suitId
            ? this with { Suits = Without(Suits, suitId) }
            : this,

        "SellWeapon" => journalEvent.Long("SuitModuleID") is { } moduleId
            ? this with { Weapons = Without(Weapons, moduleId) }
            : this,

        "UpgradeSuit" => UpgradedSuit(journalEvent),

        "UpgradeWeapon" => UpgradedWeapon(journalEvent),

        "SuitLoadout" or "SwitchSuitLoadout" or "CreateSuitLoadout" => Snapshot(journalEvent),

        "LoadoutEquipModule" => Equipped(journalEvent),

        _ => this,
    };

    private OwnedKit Bought(JournalEvent journalEvent)
    {
        if (journalEvent.Long("SuitID") is not { } suitId
            || JournalJson.Symbol(journalEvent.String("Name")) is not { } symbol
            || string.Equals(symbol, FlightSuit, StringComparison.Ordinal))
        {
            return this;
        }

        var suit = new OwnedSuit(
            suitId, symbol, GradeFromSymbol(symbol), Modifications(journalEvent.Raw, "SuitMods"), journalEvent.Timestamp);

        return this with { Suits = With(Suits, suitId, suit) };
    }

    private OwnedKit BoughtWeapon(JournalEvent journalEvent)
    {
        if (journalEvent.Long("SuitModuleID") is not { } moduleId
            || JournalJson.Symbol(journalEvent.String("Name")) is not { } symbol)
        {
            return this;
        }

        var weapon = new OwnedWeapon(
            moduleId, symbol, journalEvent.Int("Class"), Modifications(journalEvent.Raw, "WeaponMods"),
            journalEvent.Timestamp);

        return this with { Weapons = With(Weapons, moduleId, weapon) };
    }

    /// <summary>The event's own Name carries the suit's class before this upgrade, not after.</summary>
    private OwnedKit UpgradedSuit(JournalEvent journalEvent)
    {
        if (journalEvent.Long("SuitID") is not { } suitId
            || !Suits.TryGetValue(suitId, out var owned)
            || journalEvent.Int("Class") is not { } grade)
        {
            return this;
        }

        var family = (JournalJson.Symbol(journalEvent.String("Name")) ?? owned.Symbol).Split("_class")[0];

        return this with
        {
            Suits = With(Suits, suitId, owned with
            {
                Symbol = $"{family}_class{grade}",
                Grade = grade,
                SeenAt = journalEvent.Timestamp,
            }),
        };
    }

    private OwnedKit UpgradedWeapon(JournalEvent journalEvent)
    {
        if (journalEvent.Long("SuitModuleID") is not { } moduleId
            || !Weapons.TryGetValue(moduleId, out var owned)
            || journalEvent.Int("Class") is not { } grade)
        {
            return this;
        }

        return this with
        {
            Weapons = With(Weapons, moduleId, owned with { Grade = grade, SeenAt = journalEvent.Timestamp }),
        };
    }

    private OwnedKit Snapshot(JournalEvent journalEvent)
    {
        if (journalEvent.Long("SuitID") is not { } suitId
            || JournalJson.Symbol(journalEvent.String("SuitName")) is not { } symbol)
        {
            return this;
        }

        var kit = this;

        if (!string.Equals(symbol, FlightSuit, StringComparison.Ordinal))
        {
            var suit = new OwnedSuit(
                suitId, symbol, GradeFromSymbol(symbol), Modifications(journalEvent.Raw, "SuitMods"),
                journalEvent.Timestamp);

            kit = kit with { Suits = With(kit.Suits, suitId, suit) };
        }

        foreach (var module in journalEvent.Items("Modules"))
        {
            if (module.Long("SuitModuleID") is not { } moduleId
                || JournalJson.Symbol(module.String("ModuleName")) is not { } moduleSymbol)
            {
                continue;
            }

            var weapon = new OwnedWeapon(
                moduleId, moduleSymbol, module.Int("Class"), Modifications(module, "WeaponMods"),
                journalEvent.Timestamp);

            kit = kit with { Weapons = With(kit.Weapons, moduleId, weapon) };
        }

        return kit;
    }

    private OwnedKit Equipped(JournalEvent journalEvent)
    {
        if (journalEvent.Long("SuitModuleID") is not { } moduleId
            || JournalJson.Symbol(journalEvent.String("ModuleName")) is not { } symbol)
        {
            return this;
        }

        var weapon = new OwnedWeapon(
            moduleId, symbol, journalEvent.Int("Class"), Modifications(journalEvent.Raw, "WeaponMods"),
            journalEvent.Timestamp);

        return this with { Weapons = With(Weapons, moduleId, weapon) };
    }

    private static int? GradeFromSymbol(string symbol) =>
        symbol.Split("_class") is [_, { } grade] && int.TryParse(grade, out var value) ? value : null;

    private static IReadOnlyDictionary<long, T> With<T>(IReadOnlyDictionary<long, T> source, long key, T value) =>
        new Dictionary<long, T>(source) { [key] = value };

    private static IReadOnlyDictionary<long, T> Without<T>(IReadOnlyDictionary<long, T> source, long key)
    {
        if (!source.ContainsKey(key))
        {
            return source;
        }

        var copy = new Dictionary<long, T>(source);
        _ = copy.Remove(key);
        return copy;
    }

    private static IReadOnlyList<FittedModification> Modifications(
        System.Text.Json.JsonElement element, string property) =>
        [.. element.Items(property)
            .Select(entry => entry.GetString())
            .Select(JournalJson.Symbol)
            .Where(symbol => symbol is not null)
            .Select(symbol => new FittedModification(symbol!))];
}
