using System.Globalization;

namespace D47.Core.Knowledge;

/// <summary>Which of the service's search indexes a filter is sent to.</summary>
public enum GalaxySearchKind
{
    Systems,
    Stations,
    Bodies,
}

/// <summary>How one filter is shaped on the wire, which is also what makes a value valid.</summary>
public enum GalaxyFilterKind
{
    /// <summary>A closed vocabulary.</summary>
    Choice,

    /// <summary>A numeric span sent as <c>{"min","max"}</c>, which the service honours for distance only.</summary>
    Range,

    /// <summary>A free name, sent as one value in the choice shape.</summary>
    Name,

    /// <summary>A numeric span sent as <c>{"value":[min,max],"comparison":"&lt;=&gt;"}</c>.</summary>
    Comparison,

    /// <summary>"Only these". The service cannot express false, so false sends nothing.</summary>
    Flag,
}

/// <summary>One filter the galaxy search understands, and the values it accepts.</summary>
public sealed record GalaxyFilter(string Name, GalaxyFilterKind Kind, IReadOnlyList<string> Choices)
{
    /// <summary>The service's key on each search kind that honours this filter.</summary>
    public required IReadOnlyDictionary<GalaxySearchKind, string> Fields { get; init; }

    /// <summary>The tool parameter's description.</summary>
    public string Description { get; init; } = string.Empty;

    public bool Honours(GalaxySearchKind kind) => Fields.ContainsKey(kind);

    /// <summary>The key on <paramref name="kind"/>; only valid where <see cref="Honours"/> holds.</summary>
    public string FieldOn(GalaxySearchKind kind) => Fields[kind];

    // Deliberately not an overload taking the field name. `Choice(name, description, params string[])` would
    // swallow the first choice as the field on every existing call site, and the result is a filter sent
    // under the key "Alliance" — which the service ignores silently, the exact failure GalaxyFilters exists
    // to prevent.
    public static GalaxyFilter Choice(string name, string description, params string[] choices) =>
        new(name, GalaxyFilterKind.Choice, choices) { Fields = Systems(name), Description = description };

    /// <summary>A choice filter the service keys under a different name.</summary>
    public static GalaxyFilter ChoiceOf(string name, string field, string description, IReadOnlyList<string> choices) =>
        new(name, GalaxyFilterKind.Choice, choices) { Fields = Systems(field), Description = description };

    public static GalaxyFilter Range(string name, string description) =>
        new(name, GalaxyFilterKind.Range, []) { Fields = Systems(name), Description = description };

    /// <summary>A name filter the service keys under a different name.</summary>
    public static GalaxyFilter NameOf(string name, string field, string description) =>
        new(name, GalaxyFilterKind.Name, []) { Fields = Systems(field), Description = description };

    public static GalaxyFilter ComparisonOf(string name, string field, string description) =>
        new(name, GalaxyFilterKind.Comparison, []) { Fields = Systems(field), Description = description };

    public static GalaxyFilter FlagOf(string name, string field, string description) =>
        new(name, GalaxyFilterKind.Flag, []) { Fields = Systems(field), Description = description };

    private static Dictionary<GalaxySearchKind, string> Systems(string field) =>
        new() { [GalaxySearchKind.Systems] = field };
}

/// <summary>The filter vocabulary, and the local validation that is the reason for it.</summary>
public static class GalaxyFilters
{
    /// <summary>Every filter d47 offers, on any search kind.</summary>
    public static IReadOnlyList<GalaxyFilter> All { get; } =
    [
        GalaxyFilter.Range("distance", "How far to look, in light years."),
        GalaxyFilter.Choice(
            "allegiance",
            "Superpower allegiance.",
            "Alliance", "Empire", "Federation", "Guardian", "Independent", "Pilots Federation", "Thargoid"),
        GalaxyFilter.Choice(
            "government",
            "Form of government.",
            "Anarchy", "Communism", "Confederacy", "Cooperative", "Corporate", "Democracy", "Dictatorship",
            "Feudal", "None", "Patronage", "Prison", "Prison Colony", "Theocracy"),
        GalaxyFilter.Choice(
            "primary_economy",
            "The system's main economy.",
            "Agriculture", "Colony", "Extraction", "High Tech", "Industrial", "Military", "None", "Refinery",
            "Service", "Terraforming", "Tourism"),
        GalaxyFilter.Choice("security", "Security level.", "Anarchy", "High", "Low", "Medium"),

        // What the controlling faction is going through, which is what a Commander means by "a system in
        // Boom" and what gates where several grade-5 materials can be found at all
        // (docs/spikes/engineering-data-sources.md §6).
        GalaxyFilter.ChoiceOf(
            "state",
            "controlling_minor_faction_state",
            "What the controlling faction is going through. Crowd-reported, so this finds systems reported in "
            + "that state.",
            [
                "Blight", "Boom", "Bust", "Civil Liberty", "Civil Unrest", "Civil War", "Drought", "Election",
                "Expansion", "Famine", "Infrastructure Failure", "Investment", "Lockdown", "Natural Disaster",
                "None", "Outbreak", "Pirate Attack", "Public Holiday", "Retreat", "Terrorist Attack", "War",
            ]),

        // Measured as silently ignored: minor_faction_presences: {"name":{"value":[…]}}, and a top-level minor_faction.
        GalaxyFilter.NameOf(
            "faction", "minor_faction_presences", "A minor faction present in the system, by its exact name."),
        GalaxyFilter.NameOf(
            "controlling_faction",
            "controlling_minor_faction",
            "The minor faction controlling the system, by its exact name."),

        // The twelve names /api/systems/field_values/power returns.
        GalaxyFilter.ChoiceOf(
            "power",
            "controlling_power",
            "The Powerplay power controlling the system.",
            [
                "A. Lavigny-Duval", "Aisling Duval", "Archon Delaine", "Denton Patreus", "Edmund Mahon",
                "Felicia Winters", "Jerome Archer", "Li Yong-Rui", "Nakato Kaine", "Pranav Antal", "Yuri Grom",
                "Zemina Torval",
            ]) with
        {
            Fields = new Dictionary<GalaxySearchKind, string>
            {
                [GalaxySearchKind.Systems] = "controlling_power",
                [GalaxySearchKind.Bodies] = "system_controlling_power",
            },
        },
        GalaxyFilter.ChoiceOf(
            "power_state",
            "power_state",
            "The system's Powerplay state.",
            ["Exploited", "Fortified", "Stronghold", "Unoccupied"]) with
        {
            Fields = new Dictionary<GalaxySearchKind, string>
            {
                [GalaxySearchKind.Systems] = "power_state",
                [GalaxySearchKind.Bodies] = "system_power_state",
            },
        },

        // A comparison, not a range: the service drops the min/max shape for population.
        GalaxyFilter.ComparisonOf(
            "population", "population", "How many people live there. \"0\" means unpopulated."),
        GalaxyFilter.FlagOf("colonised", "is_colonised", "Only colonised systems."),
    ];

    /// <summary>The filters <paramref name="kind"/> honours.</summary>
    public static IReadOnlyList<GalaxyFilter> For(GalaxySearchKind kind) =>
        [.. All.Where(filter => filter.Honours(kind))];

    public static GalaxyFilter? Find(string name) =>
        All.FirstOrDefault(filter => string.Equals(filter.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// <paramref name="kind"/>'s vocabulary with every value spelled out, for the sentence a rejected filter
    /// gets back and for spoken help.
    /// </summary>
    public static string Describe(GalaxySearchKind kind = GalaxySearchKind.Systems) =>
        string.Join(", ", For(kind).Select(filter => filter.Kind switch
        {
            GalaxyFilterKind.Range or GalaxyFilterKind.Comparison => $"{filter.Name} (a range)",
            GalaxyFilterKind.Name => $"{filter.Name} (a minor faction's exact name)",
            GalaxyFilterKind.Flag => $"{filter.Name} (true, or left out)",
            _ => $"{filter.Name} ({string.Join("/", filter.Choices)})",
        }));

    /// <summary>The filter names alone, for the tool description.</summary>
    public static string Names(GalaxySearchKind kind = GalaxySearchKind.Systems) =>
        string.Join(", ", For(kind).Select(filter => filter.Name));
}

/// <summary>One filter with the value asked for, already known to be valid.</summary>
public sealed record GalaxyCriterion
{
    public required GalaxyFilter Filter { get; init; }

    /// <summary>Set for <see cref="GalaxyFilterKind.Choice"/> and <see cref="GalaxyFilterKind.Name"/>.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>Set for <see cref="GalaxyFilterKind.Range"/> and <see cref="GalaxyFilterKind.Comparison"/>.</summary>
    public double? Min { get; init; }

    public double? Max { get; init; }
}

/// <summary>Turns requested filter values into criteria one search kind honours.</summary>
public static class GalaxyCriteria
{
    /// <summary>Validates <paramref name="requested"/>, or explains what was wrong in words the model can act on.</summary>
    public static bool TryParse(
        GalaxySearchKind kind,
        IReadOnlyDictionary<string, string> requested,
        out IReadOnlyList<GalaxyCriterion> criteria,
        out string failure)
    {
        criteria = [];
        failure = string.Empty;

        var parsed = new List<GalaxyCriterion>();

        foreach (var (name, value) in requested)
        {
            var filter = GalaxyFilters.Find(name);

            if (filter is null)
            {
                failure =
                    $"There is no '{name}' filter. The ones I have are: {GalaxyFilters.Describe(kind)}.";
                return false;
            }

            if (!filter.Honours(kind))
            {
                var (plural, singular) = Nouns(kind);
                failure =
                    $"{plural} can't be filtered by {filter.Name}: Spansh's {singular} index doesn't carry it.";
                return false;
            }

            if (!TryParseOne(filter, value, parsed, out failure))
            {
                return false;
            }
        }

        criteria = parsed;
        return true;
    }

    private static bool TryParseOne(GalaxyFilter filter, string value, List<GalaxyCriterion> parsed, out string failure)
    {
        failure = string.Empty;

        switch (filter.Kind)
        {
            case GalaxyFilterKind.Name:
                parsed.Add(new GalaxyCriterion { Filter = filter, Choices = [value.Trim()] });
                return true;

            case GalaxyFilterKind.Flag:
                if (!bool.TryParse(value.Trim(), out var only))
                {
                    failure = $"{filter.Name} is true or left out, and '{value}' is neither.";
                    return false;
                }

                if (only)
                {
                    parsed.Add(new GalaxyCriterion { Filter = filter });
                }

                return true;

            case GalaxyFilterKind.Choice:
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
                parsed.Add(new GalaxyCriterion
                {
                    Filter = filter,
                    Choices = [.. chosen.Select(one => filter.Choices.First(allowed =>
                        string.Equals(allowed, one, StringComparison.OrdinalIgnoreCase)))],
                });

                return true;

            default:
                if (!TryParseRange(value, out var min, out var max))
                {
                    failure =
                        $"I couldn't read '{value}' as a range for {filter.Name}. "
                        + "Give it as a number, or as two numbers separated by a dash.";
                    return false;
                }

                parsed.Add(new GalaxyCriterion { Filter = filter, Min = min, Max = max });
                return true;
        }
    }

    private static (string Plural, string Singular) Nouns(GalaxySearchKind kind) => kind switch
    {
        GalaxySearchKind.Stations => ("Stations", "station"),
        GalaxySearchKind.Bodies => ("Bodies", "body"),
        _ => ("Systems", "system"),
    };

    /// <summary>Reads "20", "0-20", "-20" (up to) or "20-" (from).</summary>
    public static bool TryParseRange(string value, out double? min, out double? max)
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

        if (!GalaxyCriteria.TryParse(GalaxySearchKind.Systems, requested, out var criteria, out failure))
        {
            return false;
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
}
