using System.Globalization;
using System.Reflection;

namespace D47.Core.Knowledge;

/// <summary>What sort of thing an <see cref="OnFootEntry"/> is.</summary>
public enum OnFootKind
{
    /// <summary>A suit, at one grade.</summary>
    Suit,

    /// <summary>A hand weapon.</summary>
    Weapon,

    /// <summary>The Energylink, Profile Analyser, Genetic Sampler and Arc Cutter.</summary>
    Tool,

    /// <summary>An engineer modification for a suit.</summary>
    SuitModification,

    /// <summary>An engineer modification for a hand weapon.</summary>
    WeaponModification,

    /// <summary>A row this build does not recognise.</summary>
    Unknown,
}

/// <summary>One suit, weapon, tool or modification, keyed on the symbol the journal writes.</summary>
public sealed record OnFootEntry
{
    public required OnFootKind Kind { get; init; }

    /// <summary>Frontier's own spelling — <c>explorationsuit_class3</c>, <c>weapon_clipsize</c>.</summary>
    public required string Symbol { get; init; }

    /// <summary>What a Commander calls it.</summary>
    public required string Name { get; init; }

    /// <summary>1 to 5 for a suit.</summary>
    public int? Grade { get; init; }

    /// <summary>Weapon slots and the tool for a suit; damage type and shape for a weapon.</summary>
    public string? Detail { get; init; }

    /// <summary>The grade 1 price, where a journal was ever seen paying it.</summary>
    public long? Price { get; init; }

    /// <summary>
    /// Whether this symbol was read out of a real journal, or follows the naming convention and has
    /// never been seen.
    /// </summary>
    public bool SeenInJournal { get; init; }

    public bool IsModification =>
        Kind is OnFootKind.SuitModification or OnFootKind.WeaponModification;

    /// <summary>The suit family — <c>explorationsuit</c> for every Artemis.</summary>
    public string Family => Symbol.Split("_class")[0];

    /// <summary>How a Commander hears it: "Artemis Suit, grade 3".</summary>
    public string Speak() =>
        Grade is { } grade
            ? $"{Name}, grade {grade.ToString(CultureInfo.InvariantCulture)}"
            : Name;
}

/// <summary>
/// Every suit, hand weapon, hand tool and on-foot modification, keyed on what the journal writes (Phase
/// 20, "d47 knows what you are wearing").
/// </summary>
public static class OnFootCatalogue
{
    private const string ResourceName = "D47.Core.OnFoot";

    private static readonly Lazy<IReadOnlyList<OnFootEntry>> Loaded =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<OnFootEntry> All => Loaded.Value;

    /// <summary>The entry for a symbol Elite wrote, or null.</summary>
    public static OnFootEntry? Find(string? symbol) =>
        Journal.JournalJson.Symbol(symbol) is { } folded
            ? Loaded.Value.FirstOrDefault(entry => entry.Symbol == folded)
            : null;

    /// <summary>Every grade of one suit family, lowest first.</summary>
    public static IReadOnlyList<OnFootEntry> Grades(string family) =>
        [.. Loaded.Value
            .Where(entry => entry.Kind == OnFootKind.Suit
                            && string.Equals(entry.Family, family, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Grade)];

    /// <summary>
    /// What one grade of a suit or weapon costs to buy at grade 1, following a family where the row
    /// itself carries no price.
    /// </summary>
    public static long? BasePrice(OnFootEntry? entry) =>
        entry is null
            ? null
            : entry.Price ?? Loaded.Value
                .FirstOrDefault(other =>
                    other.Kind == entry.Kind
                    && string.Equals(other.Family, entry.Family, StringComparison.OrdinalIgnoreCase)
                    && other.Price is not null)?.Price;

    /// <summary>Suits and weapons a Commander could name, each family once.</summary>
    public static IReadOnlyList<string> Equipment =>
        [.. Loaded.Value
            .Where(entry => entry.Kind is OnFootKind.Suit or OnFootKind.Weapon)
            .Select(entry => entry.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// The suit or weapon a Commander named, through the same matcher every other name in this
    /// namespace goes through.
    /// </summary>
    public static OnFootEntry? Named(string? spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return null;
        }

        return Catalogue.Match(Equipment, spoken) is not { } name
            ? null
            : Loaded.Value
                .Where(entry => entry.Kind is OnFootKind.Suit or OnFootKind.Weapon && entry.Name == name)
                .OrderBy(entry => entry.Grade ?? 0)
                .FirstOrDefault();
    }

    /// <summary>Equipment names close enough to offer back when nothing matched.</summary>
    public static IReadOnlyList<string> Near(string spoken) => Catalogue.Near(Equipment, spoken);

    /// <summary>What a fitted modification is called, or null when nothing d47 ships joins the spelling.</summary>
    public static string? ModificationName(string? symbol) =>
        Find(symbol) is { IsModification: true } entry ? entry.Name : null;

    /// <summary>
    /// The recipes for a fitted modification: the family it names, and every per-manufacturer variant
    /// of it.
    /// </summary>
    public static IReadOnlyList<Blueprint> RecipesFor(string? symbol)
    {
        if (ModificationName(symbol) is not { } name)
        {
            return [];
        }

        var family = Checklists.ChecklistKeys.Compact(name);

        return [.. BlueprintCatalogue.All
            .Where(blueprint => blueprint.Kind is BlueprintKind.Suit or BlueprintKind.Weapon)
            .Where(blueprint => Checklists.ChecklistKeys.Compact(Bare(blueprint.Name)) == family)
            .OrderBy(blueprint => blueprint.Name, StringComparer.Ordinal)];
    }

    /// <summary>A recipe name with the manufacturer in brackets taken off.</summary>
    private static string Bare(string name)
    {
        var bracket = name.IndexOf('(', StringComparison.Ordinal);

        return bracket < 0 ? name : name[..bracket].TrimEnd();
    }

    private static IReadOnlyList<OnFootEntry> Load()
    {
        using var stream = typeof(OnFootCatalogue).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            // Every lookup then answers "I do not know what that is", which is true.
            return [];
        }

        using var reader = new StreamReader(stream);

        var built = new List<OnFootEntry>();

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#'
                || line.StartsWith("kind\t", StringComparison.Ordinal))
            {
                continue;
            }

            built.Add(Read(line.Split('\t')));
        }

        return built;
    }

    private static OnFootEntry Read(string[] cells) => new()
    {
        Kind = Kinds.GetValueOrDefault(Text(cells, 0) ?? string.Empty, OnFootKind.Unknown),
        Symbol = Text(cells, 1) ?? "unknown",
        Name = Text(cells, 2) ?? "an unnamed thing",
        Grade = Integer(cells, 3),
        Detail = Text(cells, 4),
        Price = Integer(cells, 5),
        SeenInJournal = Text(cells, 6) == "corpus",
    };

    /// <summary>The kinds the generator writes.</summary>
    private static readonly Dictionary<string, OnFootKind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["suit"] = OnFootKind.Suit,
        ["weapon"] = OnFootKind.Weapon,
        ["tool"] = OnFootKind.Tool,
        ["suitmod"] = OnFootKind.SuitModification,
        ["weaponmod"] = OnFootKind.WeaponModification,
    };

    private static string? Text(string[] cells, int index) =>
        index < cells.Length && cells[index].Length > 0 ? cells[index] : null;

    private static int? Integer(string[] cells, int index) =>
        Text(cells, index) is { } text && int.TryParse(text, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
