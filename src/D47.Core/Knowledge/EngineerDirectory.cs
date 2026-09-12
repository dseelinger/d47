using System.Globalization;
using System.Reflection;
using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>One thing an engineer works on, and how far they take it.</summary>
/// <param name="MaxGrade">
/// 1 to 5, or 0 where the source states no grade at all — which is every Odyssey suit and weapon
/// speciality, nine engineers' worth, because those blueprints are ungraded in the game rather than
/// missing a figure.
/// </param>
public sealed record Speciality(string Kind, int MaxGrade)
{
    public bool IsGraded => MaxGrade > 0;
}

/// <summary>
/// One engineer: who they are, where they work, what they grade, and what their invitation costs where
/// that is a delivery.
/// </summary>
public sealed record Engineer
{
    /// <summary>The id the journal's <c>EngineerProgress</c> writes.</summary>
    public required int Id { get; init; }

    public required string Name { get; init; }

    public string? System { get; init; }

    /// <summary>Their base.</summary>
    public string? Station { get; init; }

    /// <summary>The tribute their invitation task asks for, where it is a delivery.</summary>
    public string? UnlockCost { get; init; }

    /// <summary>What they grade, best grade first.</summary>
    public IReadOnlyList<Speciality> Specialities { get; init; } = [];

    /// <summary>Who recommends them.</summary>
    public IReadOnlyList<string> ReferredBy { get; init; } = [];

    /// <summary>The grade needed with the referrer, or null when no source states one.</summary>
    public int? ReferralGrade { get; init; }

    /// <summary>The body their base orbits, as the game labels it — "6 A".</summary>
    public string? Body { get; init; }

    /// <summary>
    /// Where their system is, in light years on Frontier's axes (Phase 28, "Where every engineer is").
    /// </summary>
    public StarPosition? Position { get; init; }

    /// <summary>Light years from a point to this engineer, or null when either end is unknown.</summary>
    public double? DistanceFrom(StarPosition? from) =>
        from is { } here && Position is { } there ? here.DistanceTo(there) : null;

    /// <summary>Whether they are out at Colonia rather than in the bubble (remediation.md 13, item 11).</summary>
    public bool? IsFarFromTheBubble =>
        Position is { } there ? there.DistanceTo(StarPosition.Origin) > 5000 : null;

    /// <summary>How the Commander learns they exist, in prose.</summary>
    public string? Discovery { get; init; }

    /// <summary>What earns the invitation — a rank, a distance, a reputation.</summary>
    public string? Meeting { get; init; }

    /// <summary>What the invitation itself asks for, in prose.</summary>
    public string? Unlock { get; init; }

    /// <summary>Whether anybody has to recommend them first.</summary>
    public bool NeedsReferral => ReferredBy.Count > 0;

    /// <summary>The best grade they reach on anything, or null when nothing is known of their work.</summary>
    public int? TopGrade => Specialities.Count == 0 ? null : Specialities.Max(speciality => speciality.MaxGrade);

    public string Where => (System, Station) switch
    {
        (null, _) => "somewhere I don't have on record",
        ({ } system, null) => system,
        ({ } system, { } station) => $"{station} in {system}",
    };
}

/// <summary>Every engineer, where they are, and what they grade (Phase 14, "Engineers").</summary>
public static class EngineerDirectory
{
    private const string ResourceName = "D47.Core.Engineers";

    private static readonly Lazy<IReadOnlyList<Engineer>> Loaded =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<Engineer> All => Loaded.Value;

    /// <summary>An engineer by the id the journal writes.</summary>
    public static Engineer? ById(int id) => All.FirstOrDefault(engineer => engineer.Id == id);

    /// <summary>
    /// An engineer by name, through the same matcher every other name in this namespace uses — so
    /// "Farseer" finds Felicity Farseer and "Tod McQuinn" finds the one with the nickname in the middle
    /// of it.
    /// </summary>
    public static Engineer? ByName(string? spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return null;
        }

        var names = All.Select(engineer => engineer.Name).ToArray();

        return Catalogue.Match(names, spoken) is { } name
            ? All.First(engineer => engineer.Name == name)
            : null;
    }

    /// <summary>
    /// Whether a recipe's engineer list names this engineer, through the same matcher <see
    /// cref="ByName"/> uses rather than by comparing the two strings.
    /// </summary>
    public static bool IsNamedIn(IEnumerable<string>? recipeEngineers, Engineer? engineer)
    {
        if (recipeEngineers is null || engineer is null)
        {
            return false;
        }

        foreach (var named in recipeEngineers)
        {
            if (Canonical(named) is { } resolved && resolved.Id == engineer.Id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The directory row a recipe table's spelling means, remembered.</summary>
    private static Engineer? Canonical(string named) =>
        Resolved.GetOrAdd(named, name => ByName(name));

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Engineer?> Resolved =
        new(StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyList<string> Near(string spoken) =>
        Catalogue.Near([.. All.Select(engineer => engineer.Name)], spoken);

    /// <summary>Every engineer based in a named system, case-insensitive.</summary>
    public static IReadOnlyList<Engineer> InSystem(string? system) =>
        string.IsNullOrWhiteSpace(system)
            ? []
            : [.. All.Where(engineer => string.Equals(engineer.System, system, StringComparison.OrdinalIgnoreCase))];

    /// <summary>Who grades a named thing, best grade first.</summary>
    public static IReadOnlyList<(Engineer Engineer, Speciality Speciality)> Grading(string? spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return [];
        }

        var kind = Catalogue.Match(Kinds, spoken);

        if (kind is null)
        {
            return [];
        }

        return
        [
            .. All
                .SelectMany(engineer => engineer.Specialities
                    .Where(speciality => speciality.Kind == kind)
                    .Select(speciality => (Engineer: engineer, Speciality: speciality)))
                .OrderByDescending(match => match.Speciality.MaxGrade)
                .ThenBy(match => match.Engineer.Name, StringComparer.Ordinal),
        ];
    }

    /// <summary>Everything any engineer works on, each named once.</summary>
    public static IReadOnlyList<string> Kinds { get; } =
        [.. Loaded.Value
            .SelectMany(engineer => engineer.Specialities.Select(speciality => speciality.Kind))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    public static IReadOnlyList<string> NearKinds(string spoken) => Catalogue.Near(Kinds, spoken);

    private static IReadOnlyList<Engineer> Load()
    {
        using var stream = typeof(EngineerDirectory).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            return [];
        }

        using var reader = new StreamReader(stream);

        var engineers = new List<Engineer>();

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#' || line.StartsWith("id\t", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Split('\t');

            if (cells.Length < 2 || !int.TryParse(cells[0], CultureInfo.InvariantCulture, out var id))
            {
                continue;
            }

            engineers.Add(new Engineer
            {
                Id = id,
                Name = cells[1],
                System = Text(cells, 2),
                Station = Text(cells, 3),
                UnlockCost = Text(cells, 4),
                Specialities = ReadSpecialities(Text(cells, 5)),
                ReferredBy = ReadNames(Text(cells, 6)),
                ReferralGrade = ReadGrade(Text(cells, 7)),
                Body = Text(cells, 8),
                Discovery = Text(cells, 9),
                Meeting = Text(cells, 10),
                Unlock = Text(cells, 11),
                Position = ReadPosition(cells),
            });
        }

        return engineers;
    }

    /// <summary>The three coordinate columns, or null.</summary>
    private static StarPosition? ReadPosition(string[] cells)
    {
        var axes = new double[3];

        for (var index = 0; index < 3; index++)
        {
            if (Text(cells, 12 + index) is not { } cell ||
                !double.TryParse(cell, CultureInfo.InvariantCulture, out axes[index]))
            {
                return null;
            }
        }

        return new StarPosition(axes[0], axes[1], axes[2]);
    }

    private static IReadOnlyList<string> ReadNames(string? cell) =>
        cell is null
            ? []
            : [.. cell.Split(',').Select(name => name.Trim()).Where(name => name.Length > 0)];

    private static int? ReadGrade(string? cell) =>
        cell is not null && int.TryParse(cell, CultureInfo.InvariantCulture, out var grade)
            ? grade
            : null;

    private static IReadOnlyList<Speciality> ReadSpecialities(string? cell)
    {
        if (cell is null)
        {
            return [];
        }

        var read = new List<Speciality>();

        foreach (var entry in cell.Split(','))
        {
            var split = entry.LastIndexOf(':');

            // A malformed entry is dropped rather than read as grade 0. "Farseer grades frame shift drives to
            // grade 0" is a worse answer than not mentioning it.
            if (split > 0
                && int.TryParse(entry[(split + 1)..], CultureInfo.InvariantCulture, out var grade))
            {
                read.Add(new Speciality(entry[..split], grade));
            }
        }

        return read;
    }

    private static string? Text(string[] cells, int index) =>
        index < cells.Length && cells[index].Length > 0 ? cells[index] : null;
}
