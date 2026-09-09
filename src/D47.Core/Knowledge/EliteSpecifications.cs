using System.Globalization;
using System.Reflection;

namespace D47.Core.Knowledge;

/// <summary>What a hull can do before anybody outfits it.</summary>
public sealed record ShipSpecification
{
    /// <summary>The symbol the journal writes, lower case — <c>anaconda</c>, <c>krait_mkii</c>.</summary>
    public required string Symbol { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// Who builds it — Faulcon DeLacy, Lakon, Zorgon Peterson — from column three of the ships table.
    /// </summary>
    public string? Manufacturer { get; init; }

    /// <summary>
    /// The hull named, with its builder when there is one to name
    /// (https://github.com/dseelinger/d47/issues/108).
    /// </summary>
    public string Described() =>
        Manufacturer is { Length: > 0 } maker ? $"{Name}, by {maker}" : Name;

    /// <summary>small, medium or large.</summary>
    public string? Pad { get; init; }

    public int? Speed { get; init; }

    public int? Boost { get; init; }

    /// <summary>Hull strength before any reinforcement.</summary>
    public int? Armour { get; init; }

    /// <summary>Base shield strength, before a generator's own rating is applied.</summary>
    public int? Shields { get; init; }

    /// <summary>How much incoming damage the hull shrugs off.</summary>
    public int? Hardness { get; init; }

    public int? HullMass { get; init; }

    public int? Crew { get; init; }

    /// <summary>How strongly it holds others out of supercruise.</summary>
    public int? MassLock { get; init; }

    public long? Cost { get; init; }

    /// <summary>Hardpoint sizes, largest first.</summary>
    public IReadOnlyList<int> Hardpoints { get; init; } = [];

    /// <summary>Optional internal compartment sizes, largest first.</summary>
    public IReadOnlyList<int> Internals { get; init; } = [];
}

/// <summary>One outfitting module, at one class and rating.</summary>
public sealed record ModuleSpecification
{
    public required string Symbol { get; init; }

    public required string Name { get; init; }

    public int? Class { get; init; }

    public string? Rating { get; init; }

    /// <summary>Fixed, gimballed or turreted, for the ones that have a mount.</summary>
    public string? Mount { get; init; }

    /// <summary>
    /// What kind of thing it is, in the vocabulary <see cref="SlotTakes"/> is keyed on — `cpp` for a
    /// power plant, `isg` for a shield generator.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>The hulls this module is restricted to, or empty for one every ship can carry.</summary>
    public IReadOnlyList<string> Hulls { get; init; } = [];

    /// <summary>Whether it has to fill its slot rather than merely fit in it.</summary>
    public bool MustFillSlot { get; init; }

    public double? Mass { get; init; }

    public double? Power { get; init; }

    public int? Integrity { get; init; }

    public long? Cost { get; init; }

    /// <summary>The drive's optimal mass.</summary>
    public double? OptimalMass { get; init; }

    public double? MaxFuelPerJump { get; init; }

    public double? FuelPower { get; init; }

    public double? FuelMultiplier { get; init; }

    /// <summary>
    /// The fraction a bulkhead adds to the hull's own <see cref="ShipSpecification.Armour"/>, so 0.8 on
    /// a Sidewinder's 60 is the 108 the outfitting screen shows.
    /// </summary>
    public double? HullBoost { get; init; }

    /// <summary>Kinetic resistance as a signed fraction.</summary>
    public double? KineticResistance { get; init; }

    public double? ThermalResistance { get; init; }

    public double? ExplosiveResistance { get; init; }

    public double? CausticResistance { get; init; }

    /// <summary>
    /// What separates this module from the next one of its kind, as name-and-value pairs in the order a
    /// Commander reads them (remediation.md 15, items 2b and 9).
    /// </summary>
    public IReadOnlyList<(string Name, string Value)> Figures { get; init; } = [];

    /// <summary>
    /// The limited group this module belongs to, or null for one a ship may carry any number of.
    /// </summary>
    public string? Limit { get; init; }

    /// <summary>Frontier's own description of the module, or null for one they do not describe.</summary>
    public string? About { get; init; }

    /// <summary>
    /// The pledge, permit or unlock a Commander needs before they can buy it, exactly as
    /// <c>outfitting.csv</c> names the gate — or null for a module anyone may fit (Phase 38).
    /// </summary>
    public string? Entitlement { get; init; }

    /// <summary>Whether buying this needs a Powerplay pledge (Phase 38).</summary>
    public bool NeedsPledge =>
        Entitlement is { Length: > 0 } gate
        && gate.StartsWith("ELITE_SPECIFIC_V_POWER", StringComparison.OrdinalIgnoreCase);

    /// <summary>What a power plant makes, in megawatts.</summary>
    public double? PowerCapacity { get; init; }

    /// <summary>The light years a Guardian FSD Booster adds to a jump, flat.</summary>
    public double? JumpBoost { get; init; }

    /// <summary>Whether this is a frame shift drive — the module a jump range is computed from.</summary>
    public bool IsDrive => OptimalMass is not null && MaxFuelPerJump is not null;

    /// <summary>Per-hull armour.</summary>
    public bool IsBulkhead => Class is null && Rating is null;

    /// <summary>The size and rating as a Commander says it: "5A".</summary>
    public string Size => Class is { } size && Rating is { } rating ? $"{size}{rating}" : Name;

    /// <summary>One named figure off this module's own row, where it carries one and it is a number.</summary>
    public double? Figure(string name)
    {
        foreach (var (label, value) in Figures)
        {
            if (string.Equals(label, name, StringComparison.OrdinalIgnoreCase)
                && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}

/// <summary>Ship and module figures (Phase 14, "Elite Dangerous Ships").</summary>
public static class EliteSpecifications
{
    private const string ResourceName = "D47.Core.EliteSpecifications";

    private static readonly Lazy<Tables> Loaded = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private sealed record Tables(
        IReadOnlyDictionary<string, ShipSpecification> Ships,
        IReadOnlyDictionary<string, ModuleSpecification> Modules,
        IReadOnlyList<string> KnownButUnmeasured,
        IReadOnlyDictionary<string, IReadOnlyList<ShipSlot>> Slots,
        IReadOnlyDictionary<string, IReadOnlyList<string>> SlotKinds,
        IReadOnlyDictionary<string, int> Limits,
        IReadOnlyDictionary<string, string> Unnamed,
        IReadOnlyDictionary<string, string> HullNames);

    public static IReadOnlyCollection<ShipSpecification> Ships => [.. Loaded.Value.Ships.Values];

    public static IReadOnlyCollection<ModuleSpecification> Modules => [.. Loaded.Value.Modules.Values];

    /// <summary>Hulls the table knows exist and has no figures for.</summary>
    public static IReadOnlyList<string> KnownButUnmeasured => Loaded.Value.KnownButUnmeasured;

    /// <summary>What to call a hull the journal named, or null for a symbol nothing knows.</summary>
    public static string? HullName(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        var key = symbol.Trim().ToLowerInvariant();

        return Loaded.Value.Ships.TryGetValue(key, out var measured)
            ? measured.Name
            : Loaded.Value.HullNames.GetValueOrDefault(key);
    }

    /// <summary>The symbol for a hull named any way Elite names it, or null for one nothing here knows.</summary>
    public static string? HullSymbol(string? hull) =>
        string.IsNullOrWhiteSpace(hull) ? null : Symbols.Value.GetValueOrDefault(Plain(hull));

    /// <summary>Every spelling of every hull, to its symbol.</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Symbols = new(
        () =>
        {
            var loaded = Loaded.Value;
            var symbols = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var (symbol, name) in loaded.HullNames)
            {
                symbols[Plain(name)] = symbol;
            }

            foreach (var (symbol, ship) in loaded.Ships)
            {
                symbols[Plain(ship.Name)] = symbol;
            }

            foreach (var symbol in loaded.HullNames.Keys.Concat(loaded.Ships.Keys))
            {
                symbols[Plain(symbol)] = symbol;
            }

            return symbols;
        },
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Letters and digits, lower case.</summary>
    private static string Plain(string said) =>
        string.Concat(said.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    /// <summary>
    /// The whole ladder, ending in what was handed in: the measured row, then the name read off the
    /// hull's armour, then a spoken match for a caller holding Frontier's localised spelling rather
    /// than a symbol, and failing all three the string itself.
    /// </summary>
    public static string HullSaid(string hull) =>
        HullName(hull) ?? Ship(hull)?.Name ?? hull;

    /// <summary>A hull, by the journal's symbol or by the name a Commander says.</summary>
    public static ShipSpecification? Ship(string? spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return null;
        }

        var ships = Loaded.Value.Ships;
        var wanted = spoken.Trim();

        if (ships.TryGetValue(wanted.ToLowerInvariant(), out var bySymbol))
        {
            return bySymbol;
        }

        var names = ships.Values.Select(ship => ship.Name).ToArray();

        return Catalogue.Match(names, wanted) is { } name
            ? ships.Values.First(ship => ship.Name == name)
            : null;
    }

    /// <summary>Hull names close enough to offer back when nothing matched.</summary>
    public static IReadOnlyList<string> NearShips(string spoken) =>
        Catalogue.Near([.. Loaded.Value.Ships.Values.Select(ship => ship.Name)], spoken);

    /// <summary>A module, by symbol, or by name with a class and rating.</summary>
    public static ModuleSpecification? Module(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        return Loaded.Value.Modules.GetValueOrDefault(symbol.Trim().ToLowerInvariant());
    }

    /// <summary>Every variant of a named module, largest first.</summary>
    public static IReadOnlyList<ModuleSpecification> ModulesNamed(string? spoken, int? size, string? rating)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return [];
        }

        var modules = Loaded.Value.Modules.Values;

        var names = modules.Select(module => module.Name).Distinct(StringComparer.Ordinal).ToArray();
        var name = Catalogue.Match(names, spoken);

        if (name is null)
        {
            return [];
        }

        return
        [
            .. modules
                .Where(module => module.Name == name)
                .Where(module => size is null || module.Class == size)
                .Where(module => rating is null
                                 || string.Equals(module.Rating, rating, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(module => module.Class)
                .ThenBy(module => module.Rating, StringComparer.Ordinal)
                .ThenBy(module => module.Mount, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Every outfitting slot of one hull, in the outfitting screen's own order (remediation.md 12, item
    /// 3).
    /// </summary>
    public static IReadOnlyList<ShipSlot> Slots(string? hull) =>
        hull is not { Length: > 0 }
            ? []
            : Loaded.Value.Slots.GetValueOrDefault(
                (Ship(hull)?.Symbol ?? hull).Trim().ToLowerInvariant()) ?? [];

    /// <summary>
    /// Which kind of slot a name is on any hull at all, or null for one no hull outfits (remediation.md
    /// 12, item 2).
    /// </summary>
    public static ShipSlotKind? KindOf(string? slot) =>
        !string.IsNullOrWhiteSpace(slot)
        && Kinds.Value.TryGetValue(slot.Trim(), out var kind)
            ? kind
            : null;

    private static readonly Lazy<IReadOnlyDictionary<string, ShipSlotKind>> Kinds =
        new(
            () => Loaded.Value.Slots.Values
                .SelectMany(slots => slots)
                .GroupBy(slot => slot.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Kind, StringComparer.OrdinalIgnoreCase),
            LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>What a fitted module is called, out loud (remediation.md 12, item 4).</summary>
    public static string? ModuleName(string? symbol)
    {
        if (Module(symbol) is not { } module)
        {
            return Newer(symbol) ?? Journal.ModuleNames.ReadableOrNull(symbol);
        }

        var said = module.IsBulkhead ? module.Name : $"{module.Size} {module.Name}";

        return module.Mount is { Length: > 0 } mount ? $"{said}, {mount}" : said;
    }

    /// <summary>
    /// How many modules of one limit group a ship may carry, or null for a group nothing limits.
    /// </summary>
    public static int? MostOf(string? group) =>
        group is { Length: > 0 } named && Loaded.Value.Limits.TryGetValue(named, out var most)
            ? most
            : null;

    /// <summary>
    /// A module Frontier has shipped and no naming source covers, said by its group (reported
    /// 2026-08-20: "not the right name for this optional internal module", against a Mk II Fighter
    /// Hangar that reached the Commander as <c>int fighterbaymk2 size5 class1 free</c>).
    /// </summary>
    private static string? Newer(string? symbol)
    {
        if (symbol is not { Length: > 0 } wanted
            || !Loaded.Value.Unnamed.TryGetValue(wanted, out var type))
        {
            return null;
        }

        var family = Loaded.Value.Modules.Values
            .Where(module => string.Equals(module.Type, type, StringComparison.OrdinalIgnoreCase))
            .Select(module => module.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        return family is [var only] ? $"{only} (newer than my table)" : null;
    }

    /// <summary>One slot of one hull, by the name the journal writes, or null for neither.</summary>
    public static ShipSlot? Slot(string? hull, string? slot) =>
        string.IsNullOrWhiteSpace(slot)
            ? null
            : Slots(hull).FirstOrDefault(candidate =>
                string.Equals(candidate.Name, slot.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Every module that can go in one slot, largest and best first (remediation.md 12, item 5).
    /// </summary>
    public static IReadOnlyList<ModuleSpecification> ModulesFor(ShipSlot slot)
    {
        var takes = slot.Restrict.Count switch
        {
            0 => SlotTakes(slot),

            // A military compartment names the kind rather than the types, because the same list is on every
            // hull that has one and repeating it per slot would be a table that can disagree with itself.
            _ => [.. slot.Restrict.SelectMany(name =>
                Loaded.Value.SlotKinds.TryGetValue(name, out var listed) ? listed : [name])],
        };

        if (takes.Count == 0)
        {
            return [];
        }

        var wanted = new HashSet<string>(takes, StringComparer.OrdinalIgnoreCase);

        return
        [
            .. Loaded.Value.Modules.Values
                .Where(module => module.Type is { } type && wanted.Contains(type))
                .Where(module => Fits(module, slot))
                .Where(module => module.Hulls.Count == 0
                                 || module.Hulls.Contains(slot.Hull, StringComparer.OrdinalIgnoreCase))
                .OrderBy(module => module.Name, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(module => module.Class)
                .ThenBy(module => module.Rating, StringComparer.Ordinal)
                .ThenBy(module => module.Mount, StringComparer.Ordinal),
        ];
    }

    /// <summary>The module types a slot accepts before its own restriction narrows it.</summary>
    private static IReadOnlyList<string> SlotTakes(ShipSlot slot) =>
        Loaded.Value.SlotKinds.GetValueOrDefault(slot.Kind switch
        {
            ShipSlotKind.Hardpoint => "hardpoint",
            ShipSlotKind.Utility => "utility",
            ShipSlotKind.Core => slot.Name,
            _ => "optional",
        }) ?? [];

    /// <summary>Whether the module is the right size for the slot.</summary>
    private static bool Fits(ModuleSpecification module, ShipSlot slot)
    {
        // A bulkhead has no class at all — the id list files every one as 1 — so its fit is decided by whose
        // hull it is, which the caller already checked.
        if (module.Class is not { } size)
        {
            return true;
        }

        if (size > slot.Size)
        {
            return false;
        }

        var exact = module.MustFillSlot
                    || (slot.Kind == ShipSlotKind.Core
                        && slot.Name is "LifeSupport" or "Radar");

        return (!exact || size == slot.Size) && CanLift(module, slot);
    }

    /// <summary>
    /// Whether a thruster can move this hull at all (reported 2026-08-20: "If a ship cannot buy
    /// Enhanced Performance Thrusters do not provide it as an option").
    /// </summary>
    private static bool CanLift(ModuleSpecification module, ShipSlot slot)
    {
        if (module.Type is not "ct" || Figure(module, "maximum mass") is not { } most)
        {
            return true;
        }

        return Ship(slot.Hull)?.HullMass is not { } mass || mass <= most;
    }

    /// <summary>One named figure off a module's own row.</summary>
    private static double? Figure(ModuleSpecification module, string name) => module.Figure(name);

    /// <summary>Module names close enough to offer back when nothing matched.</summary>
    public static IReadOnlyList<string> NearModules(string spoken) =>
        Catalogue.Near(
            [.. Loaded.Value.Modules.Values.Select(module => module.Name).Distinct(StringComparer.Ordinal)],
            spoken);

    private static Tables Load()
    {
        using var stream = typeof(EliteSpecifications).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            // Nothing can be answered without it, and answering anyway is the failure this whole table exists
            // to avoid.
            return new Tables(
                new Dictionary<string, ShipSpecification>(StringComparer.Ordinal),
                new Dictionary<string, ModuleSpecification>(StringComparer.Ordinal),
                [],
                new Dictionary<string, IReadOnlyList<ShipSlot>>(StringComparer.Ordinal),
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                new Dictionary<string, int>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        using var reader = new StreamReader(stream);

        var ships = new Dictionary<string, ShipSpecification>(StringComparer.Ordinal);
        var modules = new Dictionary<string, ModuleSpecification>(StringComparer.Ordinal);
        var unmeasured = new List<string>();
        var slots = new Dictionary<string, List<ShipSlot>>(StringComparer.Ordinal);
        var kinds = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var limits = new Dictionary<string, int>(StringComparer.Ordinal);
        var unnamed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var section = string.Empty;

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (line[0] == '[')
            {
                section = line;
                continue;
            }

            switch (section)
            {
                case "[ships]" when !line.StartsWith("symbol\t", StringComparison.Ordinal):
                    var ship = ReadShip(line.Split('\t'));
                    ships[ship.Symbol] = ship;
                    break;

                case "[modules]" when !line.StartsWith("symbol\t", StringComparison.Ordinal):
                    var module = ReadModule(line.Split('\t'));
                    modules[module.Symbol] = module;
                    break;

                case "[known-but-unnamed]" when !line.StartsWith("symbol	", StringComparison.Ordinal):
                    var nameless = line.Split('	');

                    if (nameless.Length > 1)
                    {
                        unnamed[nameless[0]] = nameless[1];
                    }

                    break;

                case "[module-limits]" when !line.StartsWith("group	", StringComparison.Ordinal):
                    var limit = line.Split('	');

                    if (limit.Length > 1 && int.TryParse(limit[1], out var most))
                    {
                        limits[limit[0]] = most;
                    }

                    break;

                case "[slot-kinds]" when !line.StartsWith("kind	", StringComparison.Ordinal):
                    var kind = line.Split('	');
                    kinds[kind[0]] = Words(kind, 1);
                    break;

                case "[slots]" when !line.StartsWith("hull	", StringComparison.Ordinal):
                    var slot = ReadSlot(line.Split('	'));

                    if (!slots.TryGetValue(slot.Hull, out var hull))
                    {
                        slots[slot.Hull] = hull = [];
                    }

                    hull.Add(slot);
                    break;

                case "[known-but-unmeasured]":
                    unmeasured.Add(line);
                    break;
            }
        }

        return new Tables(
            ships,
            modules,
            unmeasured,
            slots.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<ShipSlot>)entry.Value,
                StringComparer.Ordinal),
            kinds,
            limits,
            unnamed,
            NamesFromArmour(ships, modules));
    }

    /// <summary>What a hull is called, for hulls with no measured row — read off their own armour.</summary>
    private static Dictionary<string, string> NamesFromArmour(
        IReadOnlyDictionary<string, ShipSpecification> ships,
        IReadOnlyDictionary<string, ModuleSpecification> modules)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);

        var armour = modules.Values
            .Where(module => module.Hulls.Count == 1 && module.Symbol.Contains("_armour_", StringComparison.Ordinal))
            .GroupBy(module => module.Hulls[0], StringComparer.Ordinal);

        foreach (var hull in armour)
        {
            if (ships.ContainsKey(hull.Key))
            {
                continue;
            }

            // Two at the least.
            var names = hull.Select(module => module.Name).Where(name => name.Length > 0).ToList();

            if (names.Count < 2)
            {
                continue;
            }

            var shared = names.Aggregate(CommonPrefix);
            var cut = shared.LastIndexOf(' ');

            if (cut > 0)
            {
                found[hull.Key] = shared[..cut].TrimEnd();
            }
        }

        return found;
    }

    private static string CommonPrefix(string left, string right)
    {
        var length = 0;

        while (length < left.Length && length < right.Length && left[length] == right[length])
        {
            length++;
        }

        return left[..length];
    }

    private static ShipSpecification ReadShip(string[] cells) => new()
    {
        Symbol = Text(cells, 0) ?? "unknown",
        Name = Text(cells, 1) ?? "an unnamed ship",
        Manufacturer = Text(cells, 2),
        Pad = Text(cells, 3),
        Speed = Integer(cells, 4),
        Boost = Integer(cells, 5),
        Armour = Integer(cells, 6),
        Shields = Integer(cells, 7),
        Hardness = Integer(cells, 8),
        HullMass = Integer(cells, 9),

        // 10 is the reserve fuel tank, which nobody asks about and which the jump arithmetic this table does
        // not do would need.
        Crew = Integer(cells, 11),
        MassLock = Integer(cells, 12),
        Cost = Long(cells, 13),
        Hardpoints = Sizes(cells, 14),
        Internals = Sizes(cells, 15),
    };

    private static ModuleSpecification ReadModule(string[] cells) => new()
    {
        Symbol = Text(cells, 0) ?? "unknown",
        Name = Text(cells, 1) ?? "an unnamed module",
        Class = Integer(cells, 2),
        Rating = Text(cells, 3),
        Mount = Mounts.GetValueOrDefault(Text(cells, 4) ?? string.Empty),
        Figures = Pairs(cells, 21),
        About = Text(cells, 22),
        Limit = Text(cells, 23),
        Mass = Real(cells, 5),
        Power = Real(cells, 6),
        Integrity = Integer(cells, 7),
        Cost = Long(cells, 8),
        OptimalMass = Real(cells, 9),
        MaxFuelPerJump = Real(cells, 10),
        FuelPower = Real(cells, 11),
        FuelMultiplier = Real(cells, 12),
        HullBoost = Real(cells, 13),
        KineticResistance = Real(cells, 14),
        ThermalResistance = Real(cells, 15),
        ExplosiveResistance = Real(cells, 16),
        CausticResistance = Real(cells, 17),
        Type = Text(cells, 18),
        Hulls = Words(cells, 19),
        MustFillSlot = Text(cells, 20) is not null,

        // Appended by Phase 38, after `limit`, for the reason `limit` itself was: this reader indexes by
        // position, so a new column goes on the end and nothing above it moves.
        Entitlement = Text(cells, 24),
        PowerCapacity = Real(cells, 25),
        JumpBoost = Real(cells, 26),
    };

    private static ShipSlot ReadSlot(string[] cells) => new(
        Text(cells, 0) ?? "unknown",
        Text(cells, 1) ?? "unknown",
        Text(cells, 2) switch
        {
            "hardpoint" => ShipSlotKind.Hardpoint,
            "utility" => ShipSlotKind.Utility,
            "core" => ShipSlotKind.Core,
            _ => ShipSlotKind.Optional,
        },
        Integer(cells, 3) ?? 0,
        Words(cells, 4));

    /// <summary>Mounts, in the spelling a Commander hears.</summary>
    private static readonly Dictionary<string, string> Mounts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Fixed"] = "fixed",
        ["Gimballed"] = "gimballed",
        ["Turreted"] = "turreted",
    };

    /// <summary>The `name=value;name=value` bag the generator writes, in the order it wrote it.</summary>
    private static IReadOnlyList<(string, string)> Pairs(string[] cells, int index) =>
        Text(cells, index) is not { } text
            ? []
            : [.. text.Split(';')
                .Select(part => part.Split('=', 2))
                .Where(parts => parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
                .Select(parts => (parts[0], parts[1]))];

    private static string? Text(string[] cells, int index) =>
        index < cells.Length && cells[index].Length > 0 ? cells[index] : null;

    private static int? Integer(string[] cells, int index) =>
        Text(cells, index) is { } text && int.TryParse(text, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static long? Long(string[] cells, int index) =>
        Text(cells, index) is { } text && long.TryParse(text, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static double? Real(string[] cells, int index) =>
        Text(cells, index) is { } text
        && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    /// <summary>A space-separated cell, as a list.</summary>
    private static IReadOnlyList<string> Words(string[] cells, int index) =>
        Text(cells, index) is not { } text
            ? []
            : [.. text.Split(' ', StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>Slot sizes, largest first.</summary>
    private static IReadOnlyList<int> Sizes(string[] cells, int index) =>
        Text(cells, index) is not { } text
            ? []
            : [.. text.Split(',')
                .Select(size => int.TryParse(size, CultureInfo.InvariantCulture, out var value) ? value : 0)
                .Where(size => size > 0)
                .OrderDescending()];
}
