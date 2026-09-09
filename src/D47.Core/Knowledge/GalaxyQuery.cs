using System.Globalization;

namespace D47.Core.Knowledge;

/// <summary>How one filter is shaped on the wire, which is also what makes a value valid.</summary>
public enum GalaxyFilterKind
{
    /// <summary>A closed vocabulary.</summary>
    Choice,

    /// <summary>A numeric span.</summary>
    Range,
}

/// <summary>One filter the galaxy search understands, and the values it accepts.</summary>
public sealed record GalaxyFilter(string Name, GalaxyFilterKind Kind, IReadOnlyList<string> Choices)
{
    /// <summary>The key the service wants, where that is not the word d47 uses for it.</summary>
    public string Field { get; init; } = Name;

    // Deliberately not an overload taking the field name. `Choice(name, params string[])` would swallow the
    // first choice as the field on every existing call site, and the result is a filter sent under the key
    // "Alliance" — which the service ignores silently, the exact failure GalaxyFilters exists to prevent.
    public static GalaxyFilter Choice(string name, params string[] choices) =>
        new(name, GalaxyFilterKind.Choice, choices);

    /// <summary>A choice filter the service keys under a different name.</summary>
    public static GalaxyFilter ChoiceOf(string name, string field, IReadOnlyList<string> choices) =>
        new(name, GalaxyFilterKind.Choice, choices) { Field = field };

    public static GalaxyFilter Range(string name) => new(name, GalaxyFilterKind.Range, []);
}

/// <summary>The filter vocabulary, and the local validation that is the whole point of it.</summary>
public static class GalaxyFilters
{
    /// <summary>Every filter d47 offers.</summary>
    public static IReadOnlyList<GalaxyFilter> All { get; } =
    [
        GalaxyFilter.Range("distance"),

        // There is no "population" filter here, and there was one until 2026-08-16.
        GalaxyFilter.Choice(
            "allegiance",
            "Alliance", "Empire", "Federation", "Guardian", "Independent", "Pilots Federation", "Thargoid"),
        GalaxyFilter.Choice(
            "government",
            "Anarchy", "Communism", "Confederacy", "Cooperative", "Corporate", "Democracy", "Dictatorship",
            "Feudal", "None", "Patronage", "Prison", "Prison Colony", "Theocracy"),
        GalaxyFilter.Choice(
            "primary_economy",
            "Agriculture", "Colony", "Extraction", "High Tech", "Industrial", "Military", "None", "Refinery",
            "Service", "Terraforming", "Tourism"),
        GalaxyFilter.Choice("security", "Anarchy", "High", "Low", "Medium"),

        // What the controlling faction is going through, which is what a Commander means by "a system in
        // Boom" and what gates where several grade-5 materials can be found at all
        // (docs/spikes/engineering-data-sources.md §6).
        GalaxyFilter.ChoiceOf(
            "state",
            "controlling_minor_faction_state",
            [
                "Blight", "Boom", "Bust", "Civil Liberty", "Civil Unrest", "Civil War", "Drought", "Election",
                "Expansion", "Famine", "Infrastructure Failure", "Investment", "Lockdown", "Natural Disaster",
                "None", "Outbreak", "Pirate Attack", "Public Holiday", "Retreat", "Terrorist Attack", "War",
            ]),
    ];

    public static GalaxyFilter? Find(string name) =>
        All.FirstOrDefault(filter => string.Equals(filter.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The vocabulary with every value spelled out, for the sentence a rejected filter gets back and
    /// for spoken help.
    /// </summary>
    public static string Describe() =>
        string.Join(", ", All.Select(filter => filter.Kind == GalaxyFilterKind.Range
            ? $"{filter.Name} (a range)"
            : $"{filter.Name} ({string.Join("/", filter.Choices)})"));

    /// <summary>The filter names alone, for the tool description.</summary>
    public static string Names() => string.Join(", ", All.Select(filter => filter.Name));
}

/// <summary>One filter with the value asked for, already known to be valid.</summary>
public sealed record GalaxyCriterion
{
    public required GalaxyFilter Filter { get; init; }

    /// <summary>Set for <see cref="GalaxyFilterKind.Choice"/>.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>Set for <see cref="GalaxyFilterKind.Range"/>.</summary>
    public double? Min { get; init; }

    public double? Max { get; init; }
}

/// <summary>A validated galaxy search.</summary>
public sealed record GalaxyQuery
{
    private GalaxyQuery()
    {
    }

    /// <summary>Where distances are measured from.</summary>
    public string? ReferenceSystem { get; init; }

    public IReadOnlyList<GalaxyCriterion> Criteria { get; init; } = [];

    public int Size { get; init; } = 5;

    /// <summary>Builds a query, or explains what was wrong with it in words the model can act on.</summary>
    public static bool TryParse(
        string? referenceSystem,
        IReadOnlyDictionary<string, string> requested,
        int size,
        out GalaxyQuery query,
        out string failure)
    {
        query = new GalaxyQuery();
        failure = string.Empty;

        var criteria = new List<GalaxyCriterion>();

        foreach (var (name, value) in requested)
        {
            var filter = Find(name);

            if (filter is null)
            {
                failure =
                    $"There is no '{name}' filter. The ones I have are: {GalaxyFilters.Describe()}.";
                return false;
            }

            if (filter.Kind == GalaxyFilterKind.Choice)
            {
                var chosen = value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                var unknown = chosen.FirstOrDefault(one =>
                    !filter.Choices.Any(allowed => string.Equals(allowed, one, StringComparison.OrdinalIgnoreCase)));

                if (unknown is not null)
                {
                    failure =
                        $"'{unknown}' is not a {filter.Name} I know. It has to be one of: "
                        + $"{string.Join(", ", filter.Choices)}.";
                    return false;
                }

                // Canonicalised to the service's own casing: the comparison above is forgiving because the
                // model is speaking, and the request is exact because the service is not.
                criteria.Add(new GalaxyCriterion
                {
                    Filter = filter,
                    Choices = [.. chosen.Select(one => filter.Choices.First(allowed =>
                        string.Equals(allowed, one, StringComparison.OrdinalIgnoreCase)))],
                });

                continue;
            }

            if (!TryParseRange(value, out var min, out var max))
            {
                failure =
                    $"I couldn't read '{value}' as a range for {filter.Name}. "
                    + "Give it as a number, or as two numbers separated by a dash.";
                return false;
            }

            criteria.Add(new GalaxyCriterion { Filter = filter, Min = min, Max = max });
        }

        query = new GalaxyQuery
        {
            ReferenceSystem = string.IsNullOrWhiteSpace(referenceSystem) ? null : referenceSystem.Trim(),
            Criteria = criteria,

            // Clamped rather than rejected.
            Size = Math.Clamp(size <= 0 ? 5 : size, 1, 20),
        };

        return true;
    }

    private static GalaxyFilter? Find(string name) => GalaxyFilters.Find(name);

    /// <summary>Reads "20", "0-20", "-20" (up to) or "20-" (from).</summary>
    private static bool TryParseRange(string value, out double? min, out double? max)
    {
        min = null;
        max = null;

        var text = value.Trim();

        if (text.Length == 0)
        {
            return false;
        }

        var dash = text.IndexOf('-', 1);

        if (dash < 0)
        {
            if (text.StartsWith('-'))
            {
                return TryNumber(text[1..], out max);
            }

            // A bare number is an upper bound. "Systems within 20 light years" is the question being asked;
            // "systems exactly 20 light years away" is not a question anybody asks.
            return TryNumber(text, out max);
        }

        var left = text[..dash];
        var right = text[(dash + 1)..];

        if (right.Trim().Length == 0)
        {
            return TryNumber(left, out min);
        }

        return TryNumber(left, out min) && TryNumber(right, out max);
    }

    private static bool TryNumber(string text, out double? value)
    {
        value = null;

        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }
}
