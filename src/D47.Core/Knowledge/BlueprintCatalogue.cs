using System.Globalization;
using System.Reflection;

namespace D47.Core.Knowledge;

/// <summary>Which of the six things EDEngineer keeps in one list a recipe actually is.</summary>
public enum BlueprintKind
{
    /// <summary>A module blueprint.</summary>
    Modification,

    /// <summary>An experimental effect.</summary>
    Experimental,

    /// <summary>Ammunition, refills, limpets, SRV repair.</summary>
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

    /// <summary>A kind the table does not recognise.</summary>
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

    /// <summary>Who offers it.</summary>
    public IReadOnlyList<string> Engineers { get; init; } = [];

    /// <summary>What it costs.</summary>
    public IReadOnlyList<BlueprintIngredient> Ingredients { get; init; } = [];

    public IReadOnlyList<BlueprintEffect> Effects { get; init; } = [];

    /// <summary>coriolis-data's id for the same recipe, where the two sources are joinable.</summary>
    public string? CoriolisGuid { get; init; }

    /// <summary>Frontier's own names for this blueprint — <c>Engine_Dirty</c>, <c>Weapon_LongRange</c>.</summary>
    public IReadOnlyList<string> Symbols { get; init; } = [];

    /// <summary>
    /// The module types this recipe belongs to, in the vocabulary <see
    /// cref="ModuleSpecification.Type"/> speaks.
    /// </summary>
    public IReadOnlyList<string> ModuleTypes { get; init; } = [];

    /// <summary>What this blueprint does, in a sentence, gains before costs.</summary>
    public string Describe()
    {
        var good = Effects.Where(effect => effect.IsGood).ToList();
        var bad = Effects.Where(effect => !effect.IsGood).ToList();

        return (Said(good), Said(bad)) switch
        {
            ("", "") => string.Empty,
            (var gains, "") => gains,
            ("", var costs) => $"at the cost of {costs}",
            var (gains, costs) => $"{gains}, at the cost of {costs}",
        };
    }

    /// <summary>
    /// One side of <see cref="Describe"/>: the attributes, each with its change where the source gives
    /// a number and bare where it gives a tick.
    /// </summary>
    private static string Said(IReadOnlyList<BlueprintEffect> effects) =>
        string.Join(
            ", ",
            effects
                .Select(effect => effect.Change is { Length: > 0 } change && change != "✓"
                    ? $"{effect.Property} {change}"
                    : effect.Property)
                .Distinct(StringComparer.Ordinal));

    /// <summary>
    /// What a full grade actually costs a Commander at a given rank, or null when the rank cannot reach
    /// the grade or the recipe is not per-application.
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
/// Every engineering recipe: what it does, what it costs per application, who offers it, and which
/// experimental effects a module can take (Phase 14, <c>#102</c>).
/// </summary>
public static class BlueprintCatalogue
{
    private const string ResourceName = "D47.Core.Blueprints";

    private static readonly Lazy<IReadOnlyList<Blueprint>> Loaded =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> Offers =
        new(LoadOffers, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Module type to the names of the engineering d47 withholds — see <see cref="DisputedFor"/>.
    /// </summary>
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> Disputed =
        new(LoadDisputed, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<Blueprint> All => Loaded.Value;

    /// <summary>What a module type can be engineered with, or null for a type the table does not know.</summary>
    public static IReadOnlyList<string>? OfferedTo(string? moduleType) =>
        moduleType is { Length: > 0 } type && Offers.Value.TryGetValue(type, out var offered)
            ? offered
            : null;

    /// <summary>
    /// Engineering this module type really has and d47 will not describe (#127), by the name its
    /// sources agree it goes by.
    /// </summary>
    public static IReadOnlyList<string> DisputedFor(string? moduleType) =>
        moduleType is { Length: > 0 } type && Disputed.Value.TryGetValue(type, out var withheld)
            ? withheld
            : [];

    /// <summary>Every recipe a module can take, by the module's own specification.</summary>
    public static IReadOnlyList<Blueprint>? For(ModuleSpecification? module)
    {
        if (module is null || OfferedTo(module.Type) is not { } offered)
        {
            return null;
        }

        var wanted = offered.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Both halves, and each catches what the other cannot.
        return
        [
            .. Loaded.Value
                .Where(recipe => recipe.ModuleTypes.Contains(module.Type, StringComparer.OrdinalIgnoreCase))
                .Where(recipe => recipe.Symbols.Any(wanted.Contains))
                .OrderBy(recipe => recipe.Name, StringComparer.Ordinal)
                .ThenBy(recipe => recipe.Grade),
        ];
    }

    /// <summary>What the Commander calls the blueprint the journal named, or null for one nothing knows.</summary>
    public static string? NameOf(string? symbol) =>
        symbol is not { Length: > 0 } wanted
            ? null
            : Loaded.Value
                .FirstOrDefault(recipe => recipe.Symbols.Contains(wanted, StringComparer.OrdinalIgnoreCase))
                ?.Name;

    /// <summary>Module kinds anything can be engineered on, as a Commander would say them.</summary>
    public static IReadOnlyList<string> Modules =>
        [.. Loaded.Value.Select(b => b.Module).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>Every grade of one named blueprint, lowest first.</summary>
    public static IReadOnlyList<Blueprint> Named(string? spoken, string? module = null)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return [];
        }

        var candidates = module is null
            ? Loaded.Value
            : [.. Loaded.Value.Where(b => Same(b.Module, module))];

        return Grades(spoken, candidates);
    }

    /// <summary>
    /// Every grade of one named blueprint, narrowed to a module by its type rather than by the name a
    /// Commander reads.
    /// </summary>
    public static IReadOnlyList<Blueprint> Named(string? spoken, ModuleSpecification? module) =>
        string.IsNullOrWhiteSpace(spoken) ? []
            : module is null ? Named(spoken)
            : Grades(spoken, For(module) ?? []);

    /// <summary>
    /// The one matching rule both overloads use: the spoken name against the candidates' names, then
    /// every grade of whichever it matched.
    /// </summary>
    private static IReadOnlyList<Blueprint> Grades(string spoken, IReadOnlyList<Blueprint> candidates)
    {
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

    /// <summary>The experimental effects a module kind can take.</summary>
    public static IReadOnlyList<Blueprint> ExperimentalsFor(string? spoken) =>
        [.. ForModule(spoken).Where(b => b.Kind == BlueprintKind.Experimental)];

    /// <summary>Whether a blueprint offers exactly one grade, in which case saying which is superfluous.</summary>
    public static bool HasOneGrade(string? blueprint, string? module)
    {
        var grades = Named(blueprint, module)
            .Where(recipe => recipe.Grade is not null)
            .Select(recipe => recipe.Grade)
            .Distinct()
            .ToList();

        return grades.Count == 1;
    }

    /// <summary>Every grade a blueprint offers, highest first — what a stepper clamps to.</summary>
    public static IReadOnlyList<int> GradesFor(string? blueprint, string? module) =>
    [
        .. Named(blueprint, module)
            .Where(recipe => recipe.Grade is not null)
            .Select(recipe => recipe.Grade!.Value)
            .Distinct()
            .OrderDescending(),
    ];

    /// <summary>Blueprint names close enough to offer back when nothing matched.</summary>
    public static IReadOnlyList<string> Near(string spoken) =>
        Catalogue.Near([.. Loaded.Value.Select(b => b.Name).Distinct(StringComparer.Ordinal)], spoken);

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>The <c>[mtypes]</c> section: what each module type can be engineered with.</summary>
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

            // An empty second cell is the answer for a module type that takes no engineering, so the row is
            // kept rather than skipped.
            built[cells[0]] = Split(cells, 1);
        }

        return built;
    }

    /// <summary>
    /// The <c>[disputed]</c> section, turned inside out: the table lists one row per withheld blueprint
    /// with the types it belongs to, and every caller asks by type.
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> LoadDisputed()
    {
        var built = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        using var stream = typeof(BlueprintCatalogue).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        using var reader = new StreamReader(stream);

        var inside = false;

        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith('['))
            {
                inside = line.StartsWith("[disputed]", StringComparison.Ordinal);
                continue;
            }

            if (!inside || line.Length == 0 || line[0] == '#'
                || line.StartsWith("symbol	", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Split('	');

            // The name, not the symbol: what this is for is saying the thing out loud, and
            // `GuardianModule_Sturdy` is not what a Commander sees on the engineer's screen.
            if (Text(cells, 1) is not { Length: > 0 } name)
            {
                continue;
            }

            foreach (var type in Split(cells, 2))
            {
                built.TryAdd(type, []);
                built[type].Add(name);
            }
        }

        return built.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<Blueprint> Load()
    {
        using var stream = typeof(BlueprintCatalogue).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            // Every lookup then answers "I have no recipe for that", which is true.
            return [];
        }

        using var reader = new StreamReader(stream);

        var built = new List<Blueprint>();

        while (reader.ReadLine() is { } line)
        {
            // Sections after the recipes are other tables in the same resource, and their rows are not
            // blueprints.
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

    /// <summary>The kinds the generator writes.</summary>
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
