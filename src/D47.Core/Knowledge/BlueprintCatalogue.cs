using System.Globalization;
using System.Reflection;

namespace D47.Core.Knowledge;

/// <summary>
/// Which of the six things EDEngineer keeps in one list a recipe actually is.
/// <para>
/// <b>Not a taxonomy for its own sake.</b> Only <see cref="Modification"/> has per-application
/// ingredients, so only it may be multiplied by a roll count. Doing that arithmetic to a
/// synthesis recipe produces a number with no meaning, delivered with the same confidence as a
/// correct one.
/// </para>
/// </summary>
public enum BlueprintKind
{
    /// <summary>A module blueprint. Ingredients are the cost of <b>one application</b>.</summary>
    Modification,

    /// <summary>An experimental effect. A one-off cost, and the recipe varies by module type.</summary>
    Experimental,

    /// <summary>Ammunition, refills, limpets, SRV repair. A one-off cost, not a roll.</summary>
    Synthesis,

    /// <summary>Guardian and human tech broker unlocks.</summary>
    TechBroker,

    /// <summary>An engineer's invitation tribute.</summary>
    Unlock,

    /// <summary>Odyssey suit upgrades and mods.</summary>
    Suit,

    /// <summary>Odyssey hand weapon upgrades and mods.</summary>
    Weapon,

    /// <summary>Odyssey on-foot vendor stock — the merchant and bartender rows.</summary>
    Vendor,

    /// <summary>A kind the table does not recognise. Never multiplied by anything.</summary>
    Unknown,
}

/// <summary>One ingredient of a recipe, keyed to a <see cref="MaterialCatalogue"/> symbol.</summary>
public readonly record struct BlueprintIngredient(string Symbol, int Size)
{
    /// <summary>The material row, so a caller gets the name, ledger and grade in one hop.</summary>
    public MaterialEntry? Material => MaterialCatalogue.Find(Symbol);
}

/// <summary>What a blueprint changes, in the wording EDEngineer publishes.</summary>
public readonly record struct BlueprintEffect(string Property, string Change, bool IsGood);

/// <summary>One recipe: a blueprint at a grade, an experimental effect, or a synthesis.</summary>
public sealed record Blueprint
{
    public required BlueprintKind Kind { get; init; }

    /// <summary>The module kind it applies to — "Frame Shift Drive", "Shield Booster".</summary>
    public required string Module { get; init; }

    /// <summary>The blueprint name a Commander sees — "Increased FSD Range".</summary>
    public required string Name { get; init; }

    /// <summary>1 to 5 for a modification; null for everything that has no grade.</summary>
    public int? Grade { get; init; }

    /// <summary>Who offers it. Empty for the recipes no engineer is involved in.</summary>
    public IReadOnlyList<string> Engineers { get; init; } = [];

    /// <summary>
    /// What it costs. <b>Per application for <see cref="BlueprintKind.Modification"/> only</b> —
    /// use <see cref="TotalFor"/> rather than multiplying by hand.
    /// </summary>
    public IReadOnlyList<BlueprintIngredient> Ingredients { get; init; } = [];

    public IReadOnlyList<BlueprintEffect> Effects { get; init; } = [];

    /// <summary>coriolis-data's id for the same recipe, where the two sources are joinable.</summary>
    public string? CoriolisGuid { get; init; }

    /// <summary>
    /// Frontier's own names for this blueprint — <c>Engine_Dirty</c>, <c>Weapon_LongRange</c>.
    /// <para>
    /// <b>This is what the journal writes.</b> <c>Engineering.BlueprintName</c> on a fitted module
    /// is exactly one of these, which is what lets a roll the Commander already has be named
    /// properly instead of read back as its own symbol with the underscore taken out.
    /// </para>
    /// <para>
    /// More than one, occasionally, because coriolis files a single recipe under several names.
    /// Ten rows carry two; every other named row carries one.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> Symbols { get; init; } = [];

    /// <summary>
    /// The module types this recipe belongs to, in the vocabulary
    /// <see cref="ModuleSpecification.Type"/> speaks.
    /// <para>
    /// <b>A shared blueprint has one row per module kind and they are not interchangeable</b> —
    /// "Double Braced" has eight recipes and "Lightweight" fifteen. Matching on the name alone
    /// drew all fifteen for one module, so this says which one is actually its own.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> ModuleTypes { get; init; } = [];

    /// <summary>
    /// What a full grade actually costs a Commander at a given rank, or null when the rank
    /// cannot reach the grade or the recipe is not per-application.
    /// <para>
    /// This is the arithmetic list.md asked for and the reason the hedging in the original items
    /// is unnecessary: <c>ingredients × rolls(grade, rank)</c> is exact. Null rather than the
    /// per-application list, because silently returning a one-roll cost as a total is the
    /// mistake the <see cref="Kind"/> column exists to prevent.
    /// </para>
    /// </summary>
    public IReadOnlyList<BlueprintIngredient>? TotalFor(int rank)
    {
        if (Kind != BlueprintKind.Modification || Grade is not { } grade)
        {
            return null;
        }

        return EngineeringRules.RollsFor(grade, rank) is not { } rolls
            ? null
            : [.. Ingredients.Select(i => i with { Size = i.Size * rolls })];
    }
}

/// <summary>
/// Every engineering recipe: what it does, what it costs per application, who offers it, and
/// which experimental effects a module can take (list.md Phase 14, <c>#102</c>).
/// <para>
/// <b>Derived, not written.</b> <c>tools/gen-blueprints.py</c> builds it from msarilar/EDEngineer
/// and cross-checks it against EDCD/coriolis-data on the guid the two share.
/// </para>
/// <para>
/// <b>Coriolis is the check and never the authority</b>, and not because it is stale: eleven
/// experimental effects have a different recipe per module type — "Double Braced" has eight —
/// and coriolis models one recipe each. 27 blueprint and 11 experimental disagreements are
/// <em>printed by the generator</em> rather than resolved, because a generator that picked a
/// winner would be inventing game data with a straight face.
/// </para>
/// <para>
/// <b>Read on first use, never at startup</b>, the same way <see cref="EliteSpecifications"/> and
/// <see cref="MaterialCatalogue"/> are.
/// </para>
/// </summary>
public static class BlueprintCatalogue
{
    private const string ResourceName = "D47.Core.Blueprints";

    private static readonly Lazy<IReadOnlyList<Blueprint>> Loaded =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> Offers =
        new(LoadOffers, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<Blueprint> All => Loaded.Value;

    /// <summary>
    /// What a module type can be engineered with, or null for a type the table does not know.
    /// <para>
    /// <b>Three answers, not two</b> (remediation.md 15, item 6). An empty list means the module
    /// <em>genuinely takes no engineering</em> — a fuel tank is the reported case — and null means
    /// d47 has never heard of the type. Those were indistinguishable before, so the panel fell back
    /// to offering all forty blueprints in both, which is how a Type-10's armour came to be offered
    /// Dirty Drive Tuning and a fuel tank anything at all.
    /// </para>
    /// <para>
    /// Keyed on EDSY's <c>mtype</c>, which <see cref="ModuleSpecification.Type"/> already carries
    /// against every module. Frontier's blueprint names come back, so this joins to
    /// <see cref="Blueprint.Symbols"/> by id and never by a name.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string>? OfferedTo(string? moduleType) =>
        moduleType is { Length: > 0 } type && Offers.Value.TryGetValue(type, out var offered)
            ? offered
            : null;

    /// <summary>
    /// Every recipe a module can take, by the module's own specification. Empty where it takes
    /// none, and null where the module's type is unknown — see <see cref="OfferedTo"/>.
    /// </summary>
    public static IReadOnlyList<Blueprint>? For(ModuleSpecification? module)
    {
        if (module is null || OfferedTo(module.Type) is not { } offered)
        {
            return null;
        }

        var wanted = offered.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Both halves, and each catches what the other cannot. The type says which rows are *this
        // module's own*, because a shared blueprint has one row per kind carrying different
        // ingredients. The offer says which of that module's rows Frontier actually lets it take.
        return
        [
            .. Loaded.Value
                .Where(recipe => recipe.ModuleTypes.Contains(module.Type, StringComparer.OrdinalIgnoreCase))
                .Where(recipe => recipe.Symbols.Any(wanted.Contains))
                .OrderBy(recipe => recipe.Name, StringComparer.Ordinal)
                .ThenBy(recipe => recipe.Grade),
        ];
    }

    /// <summary>
    /// What the Commander calls the blueprint the journal named, or null for one nothing knows.
    /// <para>
    /// <b>The join that nothing shipped</b> (remediation.md 15, item 10). A fitted module's
    /// <c>Engineering.BlueprintName</c> is <c>PowerDistributor_PrioritySystems</c>, and until this
    /// existed it reached the Commander as "PowerDistributor PrioritySystems" — the symbol with its
    /// underscore taken out, which <see cref="Checklists.ChecklistNaming"/> called "ugly and true".
    /// It is called <b>System Focused</b>.
    /// </para>
    /// </summary>
    public static string? NameOf(string? symbol) =>
        symbol is not { Length: > 0 } wanted
            ? null
            : Loaded.Value
                .FirstOrDefault(recipe => recipe.Symbols.Contains(wanted, StringComparer.OrdinalIgnoreCase))
                ?.Name;

    /// <summary>Module kinds anything can be engineered on, as a Commander would say them.</summary>
    public static IReadOnlyList<string> Modules =>
        [.. Loaded.Value.Select(b => b.Module).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>
    /// Every grade of one named blueprint, lowest first.
    /// <para>
    /// Matched through <see cref="Catalogue.Match"/> so a spoken "increased range" reaches
    /// "Increased FSD Range" the same way every other name in this namespace does. Narrowed by
    /// module because one name can belong to several — "Lightweight" exists on armour, sensors
    /// and a dozen other things with different recipes.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Blueprint> Named(string? spoken, string? module = null)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return [];
        }

        var candidates = module is null
            ? Loaded.Value
            : [.. Loaded.Value.Where(b => Same(b.Module, module))];

        if (candidates.Count == 0)
        {
            return [];
        }

        var names = candidates.Select(b => b.Name).Distinct(StringComparer.Ordinal).ToArray();

        return Catalogue.Match(names, spoken) is not { } name
            ? []
            : [.. candidates.Where(b => b.Name == name).OrderBy(b => b.Module, StringComparer.Ordinal)
                .ThenBy(b => b.Grade)];
    }

    /// <summary>Every modification a module kind can take, and to what grade.</summary>
    public static IReadOnlyList<Blueprint> ForModule(string? spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return [];
        }

        var modules = Modules;

        return Catalogue.Match(modules, spoken) is not { } module
            ? []
            : [.. Loaded.Value.Where(b => Same(b.Module, module)).OrderBy(b => b.Name, StringComparer.Ordinal)
                .ThenBy(b => b.Grade)];
    }

    /// <summary>
    /// The experimental effects a module kind can take.
    /// <para>
    /// Per module kind rather than globally, because that is how the game works and how coriolis
    /// gets it wrong — the recipe for "Double Braced" on a beam laser is not the recipe on a
    /// power plant.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Blueprint> ExperimentalsFor(string? spoken) =>
        [.. ForModule(spoken).Where(b => b.Kind == BlueprintKind.Experimental)];

    /// <summary>Blueprint names close enough to offer back when nothing matched.</summary>
    public static IReadOnlyList<string> Near(string spoken) =>
        Catalogue.Near([.. Loaded.Value.Select(b => b.Name).Distinct(StringComparer.Ordinal)], spoken);

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The <c>[mtypes]</c> section: what each module type can be engineered with.
    /// <para>
    /// A second pass over the same resource rather than a second file, because the two are
    /// generated together and a Commander who never opens the Loadout tab pays for neither.
    /// </para>
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> LoadOffers()
    {
        using var stream = typeof(BlueprintCatalogue).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        using var reader = new StreamReader(stream);

        var built = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var inside = false;

        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith('['))
            {
                inside = line.StartsWith("[mtypes]", StringComparison.Ordinal);
                continue;
            }

            if (!inside || line.Length == 0 || line[0] == '#'
                || line.StartsWith("mtype	", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Split('	');

            // An empty second cell is the answer for a module type that takes no engineering, so
            // the row is kept rather than skipped. That distinction is the whole point of the
            // section: "nothing" and "I do not know" are different, and one of them used to be
            // rendered as all forty blueprints.
            built[cells[0]] = Split(cells, 1);
        }

        return built;
    }

    private static IReadOnlyList<Blueprint> Load()
    {
        using var stream = typeof(BlueprintCatalogue).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            // Every lookup then answers "I have no recipe for that", which is true. Answering
            // anyway is the failure the whole table exists to avoid.
            return [];
        }

        using var reader = new StreamReader(stream);

        var built = new List<Blueprint>();

        while (reader.ReadLine() is { } line)
        {
            // Sections after the recipes are other tables in the same resource, and their rows are
            // not blueprints. Stopping at the first one is what keeps `[groups]` from being read as
            // 86 nameless recipes — a header row is two cells wide and parses without complaint.
            if (line.Length > 0 && line[0] == '[')
            {
                break;
            }

            if (line.Length == 0 || line[0] == '#'
                || line.StartsWith("kind\t", StringComparison.Ordinal))
            {
                continue;
            }

            built.Add(Read(line.Split('\t')));
        }

        return built;
    }

    private static Blueprint Read(string[] cells) => new()
    {
        Kind = Kinds.GetValueOrDefault(Text(cells, 0) ?? string.Empty, BlueprintKind.Unknown),
        Module = Text(cells, 1) ?? "unknown",
        Name = Text(cells, 2) ?? "an unnamed blueprint",
        Grade = Integer(cells, 3),
        Engineers = Split(cells, 4),
        Ingredients = Ingredients(cells, 5),
        Effects = Effects(cells, 6),
        CoriolisGuid = Text(cells, 7),
        Symbols = Split(cells, 8),
        ModuleTypes = Split(cells, 9),
    };

    /// <summary>
    /// The kinds the generator writes. A closed set, so a word this build does not know answers
    /// <see cref="BlueprintKind.Unknown"/> — which nothing multiplies — rather than being read as
    /// a modification and quietly multiplied by a roll count.
    /// </summary>
    private static readonly Dictionary<string, BlueprintKind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["modification"] = BlueprintKind.Modification,
        ["experimental"] = BlueprintKind.Experimental,
        ["synthesis"] = BlueprintKind.Synthesis,
        ["tech-broker"] = BlueprintKind.TechBroker,
        ["unlock"] = BlueprintKind.Unlock,
        ["suit"] = BlueprintKind.Suit,
        ["weapon"] = BlueprintKind.Weapon,
        ["merchant"] = BlueprintKind.Vendor,
        ["bartender"] = BlueprintKind.Vendor,
    };

    private static string? Text(string[] cells, int index) =>
        index < cells.Length && cells[index].Length > 0 ? cells[index] : null;

    private static int? Integer(string[] cells, int index) =>
        Text(cells, index) is { } text && int.TryParse(text, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static IReadOnlyList<string> Split(string[] cells, int index) =>
        Text(cells, index) is not { } text
            ? []
            : [.. text.Split(',').Select(part => part.Trim()).Where(part => part.Length > 0)];

    private static IReadOnlyList<BlueprintIngredient> Ingredients(string[] cells, int index) =>
        Text(cells, index) is not { } text
            ? []
            : [.. text.Split(',')
                .Select(part => part.Split('*'))
                .Where(parts => parts.Length == 2
                                && int.TryParse(parts[1], CultureInfo.InvariantCulture, out _))
                .Select(parts => new BlueprintIngredient(
                    parts[0], int.Parse(parts[1], CultureInfo.InvariantCulture)))];

    private static IReadOnlyList<BlueprintEffect> Effects(string[] cells, int index) =>
        Text(cells, index) is not { } text
            ? []
            : [.. text.Split(';')
                .Select(part => part.Split('|'))
                .Where(parts => parts.Length == 3)
                .Select(parts => new BlueprintEffect(parts[0], parts[1], parts[2] == "good"))];
}
