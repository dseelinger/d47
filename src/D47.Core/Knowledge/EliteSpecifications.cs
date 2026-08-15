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
        IReadOnlyList<string> KnownButUnmeasured);

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
                []);
        }

        using var reader = new StreamReader(stream);

        var ships = new Dictionary<string, ShipSpecification>(StringComparer.Ordinal);
        var modules = new Dictionary<string, ModuleSpecification>(StringComparer.Ordinal);
        var unmeasured = new List<string>();

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

                case "[known-but-unmeasured]":
                    unmeasured.Add(line);
                    break;
            }
        }

        return new Tables(ships, modules, unmeasured);
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
    };

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

    /// <summary>Slot sizes, largest first. An unparsable entry is dropped rather than read as a 0 slot.</summary>
    private static IReadOnlyList<int> Sizes(string[] cells, int index) =>
        Text(cells, index) is not { } text
            ? []
            : [.. text.Split(',')
                .Select(size => int.TryParse(size, CultureInfo.InvariantCulture, out var value) ? value : 0)
                .Where(size => size > 0)
                .OrderDescending()];
}
