using System.Globalization;
using System.Reflection;

namespace D47.Core.Knowledge;

/// <summary>What a hull can do before anybody outfits it.</summary>
public sealed record ShipSpecification
{
    /// <summary>The symbol the journal writes, lower case — <c>anaconda</c>, <c>krait_mkii</c>.</summary>
    public required string Symbol { get; init; }

    public required string Name { get; init; }

    public string? Manufacturer { get; init; }

    /// <summary>small, medium or large. The first thing anybody actually needs to know.</summary>
    public string? Pad { get; init; }

    public int? Speed { get; init; }

    public int? Boost { get; init; }

    /// <summary>Hull strength before any reinforcement.</summary>
    public int? Armour { get; init; }

    /// <summary>Base shield strength, before a generator's own rating is applied.</summary>
    public int? Shields { get; init; }

    /// <summary>How much incoming damage the hull shrugs off. Higher is tougher.</summary>
    public int? Hardness { get; init; }

    public int? HullMass { get; init; }

    public int? Crew { get; init; }

    /// <summary>How strongly it holds others out of supercruise.</summary>
    public int? MassLock { get; init; }

    public long? Cost { get; init; }

    /// <summary>Hardpoint sizes, largest first. Sizes rather than a count: "5 hardpoints" is not an answer.</summary>
    public IReadOnlyList<int> Hardpoints { get; init; } = [];

    /// <summary>Optional internal compartment sizes, largest first.</summary>
    public IReadOnlyList<int> Internals { get; init; } = [];
}

/// <summary>
/// One outfitting module, at one class and rating.
/// <para>
/// Keyed by the same symbol the journal's <c>Loadout</c> writes, so a module the Commander
/// already has fitted and a module they are asking about are the same lookup.
/// </para>
/// </summary>
public sealed record ModuleSpecification
{
    public required string Symbol { get; init; }

    public required string Name { get; init; }

    public int? Class { get; init; }

    public string? Rating { get; init; }

    /// <summary>Fixed, gimballed or turreted, for the ones that have a mount.</summary>
    public string? Mount { get; init; }

    /// <summary>
    /// What kind of thing it is, in the vocabulary <see cref="SlotTakes"/> is keyed on — `cpp`
    /// for a power plant, `isg` for a shield generator. Never shown; it exists so a slot can be
    /// asked what it accepts without anybody writing down a list of module names by hand.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// The hulls this module is restricted to, or empty for one every ship can carry. Armour is
    /// the common case — a Vulture's is not an Anaconda's — and the rest are fighter hangars,
    /// luxury cabins and the Mk II range.
    /// </summary>
    public IReadOnlyList<string> Hulls { get; init; } = [];

    /// <summary>
    /// Whether it has to fill its slot rather than merely fit in it. True of the SCO drive, which
    /// is the one module a Commander can buy that will not sit in a compartment larger than
    /// itself.
    /// </summary>
    public bool MustFillSlot { get; init; }

    public double? Mass { get; init; }

    public double? Power { get; init; }

    public int? Integrity { get; init; }

    public long? Cost { get; init; }

    /// <summary>The drive's optimal mass. Only a frame shift drive carries these four.</summary>
    public double? OptimalMass { get; init; }

    public double? MaxFuelPerJump { get; init; }

    public double? FuelPower { get; init; }

    public double? FuelMultiplier { get; init; }

    /// <summary>
    /// The fraction a bulkhead adds to the hull's own <see cref="ShipSpecification.Armour"/>, so
    /// 0.8 on a Sidewinder's 60 is the 108 the outfitting screen shows. Only a bulkhead carries
    /// this and the four resistances below.
    /// </summary>
    public double? HullBoost { get; init; }

    /// <summary>
    /// Kinetic resistance as a signed fraction. <b>Negative is a hole, not a saving</b> — every
    /// alloy below Mirrored is -0.2 against kinetic, meaning it takes 20% more of it.
    /// </summary>
    public double? KineticResistance { get; init; }

    public double? ThermalResistance { get; init; }

    public double? ExplosiveResistance { get; init; }

    public double? CausticResistance { get; init; }

    /// <summary>
    /// What separates this module from the next one of its kind, as name-and-value pairs in the
    /// order a Commander reads them (remediation.md 15, items 2b and 9).
    /// <para>
    /// <b>Per kind rather than one set for everything.</b> A weapon leads with damage per second
    /// and its damage type; a power distributor with three capacitors and how fast each refills.
    /// The reported complaint was that price was the only thing telling two modules apart, and the
    /// answer is not one list of figures but the right few for what the thing is.
    /// </para>
    /// <para>
    /// Damage per second is computed by coriolis's own formula rather than by arithmetic invented
    /// here — see <c>dps</c> in the generator, and the licence distinction that makes lifting it
    /// the allowed half.
    /// </para>
    /// </summary>
    public IReadOnlyList<(string Name, string Value)> Figures { get; init; } = [];

    /// <summary>
    /// Frontier's own description of the module, or null for one they do not describe.
    /// <para>
    /// <b>The answer to "what's special about a Guardian Distributor?" in their words</b> — that it
    /// speeds up capacitor recharge at the cost of smaller capacitors and more heat. 707 modules
    /// carry one. Attributing Frontier in their own words is what <c>NOTICE</c> already does and
    /// what the game-data invariant asks for.
    /// </para>
    /// </summary>
    public string? About { get; init; }

    public bool IsDrive => OptimalMass is not null;

    /// <summary>
    /// Per-hull armour. The one module kind with no class and no rating: the id list files every
    /// bulkhead as class 1 and rates the older hulls I and the newer ones A, B or C for the same
    /// five grades, so the generator drops both rather than speak a placeholder.
    /// <para>
    /// Read off the missing class rather than off <see cref="HullBoost"/>, because five bulkheads
    /// are named in the id list and absent from the figures — and a Lynx Highliner's armour is
    /// still armour when d47 has no numbers for it. The specification tests assert that no other
    /// module kind reaches the table without a class.
    /// </para>
    /// </summary>
    public bool IsBulkhead => Class is null && Rating is null;

    /// <summary>
    /// The size and rating as a Commander says it: "5A". A bulkhead has neither, so it falls back
    /// to the name — which already carries its hull, because forty-eight of them are called
    /// Lightweight Alloy.
    /// </summary>
    public string Size => Class is { } size && Rating is { } rating ? $"{size}{rating}" : Name;
}

/// <summary>
/// Ship and module figures (list.md Phase 14, "Elite Dangerous Ships").
/// <para>
/// <b>The table is derived, not written.</b> None of this is in the journal — the journal says
/// what <em>this</em> Commander is flying, not what a hull can do before they buy one — so the
/// choice was a table or no feature, and a hand-written one is exactly the confidently-invented
/// game data the guardrails exist to prevent. A wrong top speed reads identically to the feature
/// working. <c>tools/gen-elite-specs.py</c> builds it by joining EDCD/FDevIDs, which is the
/// naming authority and carries the symbols the journal writes, with EDCD/coriolis-data, which
/// carries the figures, on Frontier's own ids.
/// </para>
/// <para>
/// <b>Read on first use, never at startup.</b> Twelve hundred module rows is a parse nobody should
/// pay for unless they ask a specification question, which is what list.md means by a dataset
/// "lazy-queried at runtime". <see cref="Lazy{T}"/> rather than a static constructor so the
/// laziness is visible in the type rather than a property of where the field happens to sit.
/// </para>
/// </summary>
public static class EliteSpecifications
{
    private const string ResourceName = "D47.Core.EliteSpecifications";

    private static readonly Lazy<Tables> Loaded = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private sealed record Tables(
        IReadOnlyDictionary<string, ShipSpecification> Ships,
        IReadOnlyDictionary<string, ModuleSpecification> Modules,
        IReadOnlyList<string> KnownButUnmeasured,
        IReadOnlyDictionary<string, IReadOnlyList<ShipSlot>> Slots,
        IReadOnlyDictionary<string, IReadOnlyList<string>> SlotKinds);

    public static IReadOnlyCollection<ShipSpecification> Ships => [.. Loaded.Value.Ships.Values];

    public static IReadOnlyCollection<ModuleSpecification> Modules => [.. Loaded.Value.Modules.Values];

    /// <summary>
    /// Hulls the table knows exist and has no figures for.
    /// <para>
    /// Three of them when the table was last built. They are in the community's ship data and not
    /// yet in its id list, so nothing can key them to what the journal writes — which makes their
    /// figures unreachable but their <em>existence</em> certain. Carried through so d47 can say
    /// "that is a ship I know of and have no figures for", which is the difference between a
    /// table that is stale and one that is wrong.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> KnownButUnmeasured => Loaded.Value.KnownButUnmeasured;

    /// <summary>
    /// A hull, by the journal's symbol or by the name a Commander says.
    /// <para>
    /// Both, because both arrive: <c>get_ship</c> reports <c>krait_mkii</c> off the Loadout and a
    /// Commander asks about a "Krait Mark Two". The spoken form goes through
    /// <see cref="Catalogue.Match"/>, so it gets the same relaxed and unique-fragment handling
    /// every other name in this namespace does.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// A module, by symbol, or by name with a class and rating.
    /// <para>
    /// The name alone is not a module: there are eleven Frame Shift Drives and they differ by an
    /// order of magnitude in every figure worth quoting. So a name without a size returns the
    /// candidates, and the caller asks which one — rather than picking one and reporting its
    /// numbers as though the question had been answered.
    /// </para>
    /// </summary>
    public static ModuleSpecification? Module(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        return Loaded.Value.Modules.GetValueOrDefault(symbol.Trim().ToLowerInvariant());
    }

    /// <summary>
    /// Every variant of a named module, largest first. Empty for a name the table does not know.
    /// </summary>
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
    /// Every outfitting slot of one hull, in the outfitting screen's own order
    /// (remediation.md 12, item 3).
    /// <para>
    /// <b>Cosmetics are not in it.</b> A paint job and a bobble are slots in the journal and are
    /// not things anybody outfits, so the table simply does not carry them — which makes this the
    /// answer to "is this part of outfitting" as well, asked the one way that cannot go stale.
    /// </para>
    /// <para>
    /// Empty for a hull the table does not know, which is a real answer: it means the page falls
    /// back to what the journal reported rather than inventing a layout.
    /// </para>
    /// </summary>
    public static IReadOnlyList<ShipSlot> Slots(string? hull) =>
        string.IsNullOrWhiteSpace(hull)
            ? []
            : Loaded.Value.Slots.GetValueOrDefault(hull.Trim().ToLowerInvariant()) ?? [];

    /// <summary>
    /// Which kind of slot a name is on any hull at all, or null for one no hull outfits
    /// (remediation.md 12, item 2).
    /// <para>
    /// <b>The answer to "is this part of outfitting" for a hull the table does not know.</b> A
    /// paint job, a bobble and a ship kit are slots in the journal and are in no hull's layout, so
    /// a name absent from every one of them is a name nothing outfits — which keeps the cosmetics
    /// off the list even where the layout itself cannot be looked up.
    /// </para>
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

    /// <summary>
    /// What a fitted module is called, out loud (remediation.md 12, item 4).
    /// <para>
    /// Elite writes a module as a symbol, and the reading that only strips the decoration off it
    /// produces "int powerplant size6 class5" — ugly and true, which was the right answer while
    /// nothing shipped both spellings. This table does ship both, so a module it knows is said the
    /// way the outfitting screen says it: <b>6A Power Plant</b>.
    /// </para>
    /// <para>
    /// The ugly form is still the fallback rather than a shrug, because a module the table has
    /// never heard of is one Frontier has just added and the symbol still says what it is.
    /// </para>
    /// </summary>
    public static string? ModuleName(string? symbol)
    {
        if (Module(symbol) is not { } module)
        {
            return Journal.ModuleNames.ReadableOrNull(symbol);
        }

        var said = module.IsBulkhead ? module.Name : $"{module.Size} {module.Name}";

        return module.Mount is { Length: > 0 } mount ? $"{said}, {mount}" : said;
    }

    /// <summary>One slot of one hull, by the name the journal writes, or null for neither.</summary>
    public static ShipSlot? Slot(string? hull, string? slot) =>
        string.IsNullOrWhiteSpace(slot)
            ? null
            : Slots(hull).FirstOrDefault(candidate =>
                string.Equals(candidate.Name, slot.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Every module that can go in one slot, largest and best first
    /// (remediation.md 12, item 5).
    /// <para>
    /// <b>Derived from what the slot is</b> rather than from a list anybody wrote down: the kind
    /// decides which module types it takes, the size decides how big they may be, and a slot that
    /// restricts itself further says so. Four rules, and each one is a rule the outfitting screen
    /// enforces:
    /// </para>
    /// <list type="bullet">
    /// <item>a module bigger than its slot does not fit;</item>
    /// <item>life support and sensors must fill their slot exactly, and so must an SCO drive;</item>
    /// <item>a module restricted to some hulls is offered on those hulls only — which is what
    /// keeps a Vulture's armour off an Anaconda and a fighter hangar off a Sidewinder;</item>
    /// <item>a restricted compartment takes only the types it names.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<ModuleSpecification> ModulesFor(ShipSlot slot)
    {
        var takes = slot.Restrict.Count switch
        {
            0 => SlotTakes(slot),

            // A military compartment names the kind rather than the types, because the same list
            // is on every hull that has one and repeating it per slot would be a table that can
            // disagree with itself.
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

    /// <summary>
    /// The module types a slot accepts before its own restriction narrows it. A core slot is
    /// keyed on its journal name, because a power plant and a fuel tank are both core internals
    /// and neither goes in the other's socket.
    /// </summary>
    private static IReadOnlyList<string> SlotTakes(ShipSlot slot) =>
        Loaded.Value.SlotKinds.GetValueOrDefault(slot.Kind switch
        {
            ShipSlotKind.Hardpoint => "hardpoint",
            ShipSlotKind.Utility => "utility",
            ShipSlotKind.Core => slot.Name,
            _ => "optional",
        }) ?? [];

    /// <summary>
    /// Whether the module is the right size for the slot.
    /// <para>
    /// Undersizing is normal and is most of how a build is made — a 3A shield generator in a size
    /// 5 compartment is a real choice. The exceptions are the three the game makes: life support
    /// and sensors are the slot's size or nothing, and a module that says it must fill its slot
    /// means it.
    /// </para>
    /// </summary>
    private static bool Fits(ModuleSpecification module, ShipSlot slot)
    {
        // A bulkhead has no class at all — the id list files every one as 1 — so its fit is
        // decided by whose hull it is, which the caller already checked.
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

        return !exact || size == slot.Size;
    }

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
            // Nothing can be answered without it, and answering anyway is the failure this whole
            // table exists to avoid. Empty tables mean every lookup reports "I have no figures
            // for that", which is true.
            return new Tables(
                new Dictionary<string, ShipSpecification>(StringComparer.Ordinal),
                new Dictionary<string, ModuleSpecification>(StringComparer.Ordinal),
                [],
                new Dictionary<string, IReadOnlyList<ShipSlot>>(StringComparer.Ordinal),
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));
        }

        using var reader = new StreamReader(stream);

        var ships = new Dictionary<string, ShipSpecification>(StringComparer.Ordinal);
        var modules = new Dictionary<string, ModuleSpecification>(StringComparer.Ordinal);
        var unmeasured = new List<string>();
        var slots = new Dictionary<string, List<ShipSlot>>(StringComparer.Ordinal);
        var kinds = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

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
            kinds);
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

        // 10 is the reserve fuel tank, which nobody asks about and which the jump arithmetic
        // this table does not do would need. Read past rather than modelled.
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

    /// <summary>
    /// Mounts, in the spelling a Commander hears. A closed set of three rather than per-module
    /// data, so it lives here rather than in the generated file — the same split
    /// <see cref="Journal.MaterialGrades"/> makes for grade capacities.
    /// <para>
    /// A value outside the set answers null rather than being echoed. Losing the mount reads as a
    /// module with no mount, which is true of most of them; passing an unrecognised word through
    /// would put whatever the id list starts writing into something spoken aloud.
    /// </para>
    /// </summary>
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

    /// <summary>A space-separated cell, as a list. Empty for a cell the generator left blank.</summary>
    private static IReadOnlyList<string> Words(string[] cells, int index) =>
        Text(cells, index) is not { } text
            ? []
            : [.. text.Split(' ', StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>Slot sizes, largest first. An unparsable entry is dropped rather than read as a 0 slot.</summary>
    private static IReadOnlyList<int> Sizes(string[] cells, int index) =>
        Text(cells, index) is not { } text
            ? []
            : [.. text.Split(',')
                .Select(size => int.TryParse(size, CultureInfo.InvariantCulture, out var value) ? value : 0)
                .Where(size => size > 0)
                .OrderDescending()];
}
