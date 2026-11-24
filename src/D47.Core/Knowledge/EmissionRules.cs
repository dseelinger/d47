namespace D47.Core.Knowledge;

/// <summary>One group of materials a High Grade Emission can hold, and the condition that produces it.</summary>
/// <param name="Allegiance">
/// The allegiance the system answers to, exactly as the journal spells it — <c>Federation</c>,
/// <c>Empire</c>, <c>Independent</c>.
/// </param>
/// <param name="States">
/// The states the controlling faction must be in — any one of them — in the journal's own spelling.
/// </param>
/// <param name="Materials">The material symbols, as the journal writes them.</param>
public sealed record EmissionGroup(
    string Allegiance,
    IReadOnlyList<string> States,
    IReadOnlyList<string> Materials);

/// <summary>What a High Grade Emission holds, and where (Phase 40).</summary>
public static class EmissionRules
{
    /// <summary>The population a system needs before a High Grade Emission is worth mentioning at all.</summary>
    public const long MinimumPopulation = 1_000_000;

    /// <summary>The six groups, exactly as the Commander wrote them.</summary>
    public static readonly IReadOnlyList<EmissionGroup> Groups =
    [
        new("Federation", [], ["fedcorecomposites", "fedproprietarycomposites"]),
        new("Empire", [], ["imperialshielding"]),
        new("Independent", ["CivilUnrest"], ["improvisedcomponents"]),
        new("Independent", ["War", "CivilWar"], ["militarygradealloys", "militarysupercapacitors"]),

        // Expansion beside Boom.
        new("Independent", ["Boom", "Expansion"],
            ["protoheatradiators", "protolightalloys", "protoradiolicalloys"]),

        new("Independent", ["Outbreak"], ["pharmaceuticalisolators"]),
    ];

    /// <summary>
    /// Every group a system offers — none, one, or two where its controlling faction wears two states
    /// at once.
    /// </summary>
    public static IReadOnlyList<EmissionGroup> For(string? systemAllegiance, IEnumerable<string>? states)
    {
        if (systemAllegiance is not { Length: > 0 })
        {
            return [];
        }

        var held = (states ?? [])
            .Where(state => !string.IsNullOrWhiteSpace(state))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. Groups.Where(group =>
                string.Equals(group.Allegiance, systemAllegiance, StringComparison.OrdinalIgnoreCase)
                && (group.States.Count == 0 || group.States.Any(held.Contains))),
        ];
    }

    /// <summary>The Systems search that finds where <paramref name="group"/> is produced: allegiance, states, 500 light years, the population floor.</summary>
    public static IReadOnlyDictionary<string, string> SearchArguments(EmissionGroup group)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["distance"] = "500",
            ["allegiance"] = group.Allegiance,
            ["population"] = MinimumPopulation.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-",
        };

        if (group.States.Count > 0)
        {
            arguments["state"] = string.Join(",", group.States.Select(FarmingAdvice.Spaced));
        }

        return arguments;
    }

    /// <summary>What one material needs, or null for one no emission carries.</summary>
    public static EmissionGroup? Holding(string? symbol) =>
        symbol is { Length: > 0 }
            ? Groups.FirstOrDefault(group =>
                group.Materials.Any(material =>
                    string.Equals(material, symbol, StringComparison.OrdinalIgnoreCase)))
            : null;
}
