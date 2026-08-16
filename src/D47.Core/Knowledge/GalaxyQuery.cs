using System.Globalization;

namespace D47.Core.Knowledge;

/// <summary>How one filter is shaped on the wire, which is also what makes a value valid.</summary>
public enum GalaxyFilterKind
{
    /// <summary>A closed vocabulary. Serialises as <c>{"value":["Federation"]}</c>.</summary>
    Choice,

    /// <summary>A numeric span. Serialises as <c>{"min":"0","max":"20"}</c>.</summary>
    Range,
}

/// <summary>
/// One filter the galaxy search understands, and the values it accepts.
/// </summary>
public sealed record GalaxyFilter(string Name, GalaxyFilterKind Kind, IReadOnlyList<string> Choices)
{
    /// <summary>
    /// The key the service wants, where that is not the word d47 uses for it.
    /// <para>
    /// Almost always the same string, and deliberately so — two names for one filter is a thing
    /// to keep in step. The exception is a service field whose real name is long enough to cost
    /// the model something to say: the tool description lists every filter, and tool definitions
    /// serialise first in the request, so a name is prompt in a way a value is not.
    /// </para>
    /// </summary>
    public string Field { get; init; } = Name;

    // Deliberately not an overload taking the field name. `Choice(name, params string[])` would
    // swallow the first choice as the field on every existing call site, and the result is a
    // filter sent under the key "Alliance" — which the service ignores silently, the exact
    // failure GalaxyFilters exists to prevent. A differently shaped call cannot be captured by
    // accident. (Caught by SpanshRequestTests before it left the working tree.)
    public static GalaxyFilter Choice(string name, params string[] choices) =>
        new(name, GalaxyFilterKind.Choice, choices);

    /// <summary>A choice filter the service keys under a different name. See <see cref="Field"/>.</summary>
    public static GalaxyFilter ChoiceOf(string name, string field, IReadOnlyList<string> choices) =>
        new(name, GalaxyFilterKind.Choice, choices) { Field = field };

    public static GalaxyFilter Range(string name) => new(name, GalaxyFilterKind.Range, []);
}

/// <summary>
/// The filter vocabulary, and the local validation that is the whole point of it.
/// <para>
/// <b>Why this exists.</b> The search service ignores filter keys it does not recognise. It does
/// not reject them, warn about them, or mention them in the response — a request carrying
/// <c>allegience</c> instead of <c>allegiance</c> returns 200 with the same result count as no
/// filter at all. Measured against the live service on 2026-08-14: a bogus key and a misspelled
/// real key both returned 108 systems within 20 light years of Sol, identical to the unfiltered
/// search, while the correctly spelled filter returned 60.
/// </para>
/// <para>
/// That failure mode is the dangerous one. A wrong <em>value</em> shape is a 400 and everybody
/// finds out; a wrong <em>key</em> is a confident answer to a question nobody asked, and it
/// reaches the Commander as "the nearest Federation system" when the search was never told to
/// care about allegiance. d47's first guardrail is never to invent game data, and silently
/// dropping the filter that made the question a question is exactly that. So the vocabulary is
/// closed here, checked here, and a filter this file does not know is refused before a request
/// is built.
/// </para>
/// </summary>
public static class GalaxyFilters
{
    /// <summary>
    /// Every filter d47 offers. Deliberately a fraction of what the service supports: this is the
    /// set that answers a question a Commander asks out loud, and each one added is a word the
    /// model has to be taught and a row of documentation to keep true.
    /// <para>
    /// The choice vocabularies are the service's own, read from its <c>field_values</c> endpoints
    /// on 2026-08-14 rather than recalled. They are pinned rather than fetched because a
    /// vocabulary that changes underneath a running turn is a tool schema that changes underneath
    /// a cached prefix (architecture.md §6).
    /// </para>
    /// </summary>
    public static IReadOnlyList<GalaxyFilter> All { get; } =
    [
        GalaxyFilter.Range("distance"),

        // There is no "population" filter here, and there was one until 2026-08-16. It did
        // nothing. Measured within 15 light years of Sol, where 48 of the 51 systems are
        // populated: {"min":"1","max":"1000000000000"} returned 51, {"min":"0","max":"0"} returned
        // 51, numeric bounds rather than string ones returned 51, and a key the service has never
        // heard of returned 51. The field is real — its own field_values endpoint reports a
        // minimum and a maximum for it — and only the range shape is dropped; written as a choice
        // it is honoured and matches nothing, 0 results for a population a system in range
        // actually has. So every "find me a system with a million people" this ever answered was
        // an answer to a question nobody asked, which is the precise failure this class exists to
        // prevent, shipped inside it since Phase 14. A filter the service ignores must not be
        // offered, and offering it is worse than not having it — the number is on every result,
        // so a caller who needs the distinction reads it there (ColonisationScan does).
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

        // What the controlling faction is going through, which is what a Commander means by "a
        // system in Boom" and what gates where several grade-5 materials can be found at all
        // (docs/spikes/engineering-data-sources.md §6).
        //
        // The field is NOT called "state", and that trap is worse than the silent-ignore one this
        // class was built for. Measured on 2026-08-15: "state" is a real key — it has its own
        // field_values list carrying exactly these 21 words, and it is honoured rather than
        // dropped — but it matches nothing. Zero systems for every value including "None", where
        // a bogus key returns the unfiltered count; no result row carries a "state" field at all.
        // So it fails as an empty answer rather than as a wrong one, and an empty answer reads as
        // "there are none near you", which is a wrong answer that looks like a right one.
        // controlling_minor_faction_state returns 1,286 systems in Boom within 200 ly of Sol.
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
    /// The vocabulary with every value spelled out, for the sentence a <em>rejected</em> filter
    /// gets back and for spoken help.
    /// <para>
    /// <b>Not for the tool description.</b> Every value here is already in that tool's schema as
    /// an <c>enum</c> on the parameter it belongs to, so putting it in the description as well
    /// pays for the whole vocabulary twice in prompt position 1 (architecture.md §6). A tool
    /// result is a different matter — it is not cached, and a model that has just guessed a
    /// filter value needs to be shown the real ones rather than told it was wrong.
    /// </para>
    /// </summary>
    public static string Describe() =>
        string.Join(", ", All.Select(filter => filter.Kind == GalaxyFilterKind.Range
            ? $"{filter.Name} (a range)"
            : $"{filter.Name} ({string.Join("/", filter.Choices)})"));

    /// <summary>The filter names alone, for the tool description. See <see cref="Describe"/>.</summary>
    public static string Names() => string.Join(", ", All.Select(filter => filter.Name));
}

/// <summary>One filter with the value asked for, already known to be valid.</summary>
public sealed record GalaxyCriterion
{
    public required GalaxyFilter Filter { get; init; }

    /// <summary>Set for <see cref="GalaxyFilterKind.Choice"/>.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>Set for <see cref="GalaxyFilterKind.Range"/>. Either end may be absent.</summary>
    public double? Min { get; init; }

    public double? Max { get; init; }
}

/// <summary>
/// A validated galaxy search. Constructed only through <see cref="Parse"/>, so a query object
/// existing is itself the evidence that every filter in it is one the service will honour.
/// </summary>
public sealed record GalaxyQuery
{
    private GalaxyQuery()
    {
    }

    /// <summary>Where distances are measured from. Null asks the service for its own default.</summary>
    public string? ReferenceSystem { get; init; }

    public IReadOnlyList<GalaxyCriterion> Criteria { get; init; } = [];

    public int Size { get; init; } = 5;

    /// <summary>
    /// Builds a query, or explains what was wrong with it in words the model can act on.
    /// <para>
    /// The failure text names the filter that was not understood and lists what is available,
    /// because a tool error that says only "invalid filter" invites the model to guess again with
    /// the same vocabulary it just invented.
    /// </para>
    /// </summary>
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

                // Canonicalised to the service's own casing: the comparison above is forgiving
                // because the model is speaking, and the request is exact because the service is
                // not.
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

            // Clamped rather than rejected. A model asking for fifty results is not making an
            // error worth an error message — it is asking for more than can be read aloud in a
            // cockpit, and the answer to that is fewer results, not a refusal.
            Size = Math.Clamp(size <= 0 ? 5 : size, 1, 20),
        };

        return true;
    }

    private static GalaxyFilter? Find(string name) => GalaxyFilters.Find(name);

    /// <summary>
    /// Reads "20", "0-20", "-20" (up to) or "20-" (from). Written by hand rather than with a
    /// regex because the shapes are four and each means something different.
    /// </summary>
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

            // A bare number is an upper bound. "Systems within 20 light years" is the question
            // being asked; "systems exactly 20 light years away" is not a question anybody asks.
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
