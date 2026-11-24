using System.Globalization;

namespace D47.Core.Knowledge;

/// <summary>One group of materials a High Grade Emission can hold, and the condition that produces it.</summary>
/// <param name="Allegiances">
/// The allegiances that produce it, exactly as the journal spells them — <c>Federation</c>, <c>Empire</c>,
/// <c>Independent</c>, <c>Alliance</c>. With no states, the system's allegiance; with states, the allegiance of
/// the faction in that state.
/// </param>
/// <param name="States">
/// The states a faction present must be in — any one of them — in the journal's own spelling. Empty for a
/// group the system's allegiance alone produces.
/// </param>
/// <param name="Materials">The material symbols, as the journal writes them.</param>
public sealed record EmissionGroup(
    IReadOnlyList<string> Allegiances,
    IReadOnlyList<string> States,
    IReadOnlyList<string> Materials);

/// <summary>One faction present in a system: its own allegiance and every state it is in.</summary>
public sealed record EmissionFaction(string? Allegiance, IReadOnlyCollection<string> States);

/// <summary>
/// What a High Grade Emission holds, and where. Each emission belongs to one faction present in the system,
/// not only the controlling one, and holds what that faction's state produces.
/// </summary>
public static class EmissionRules
{
    /// <summary>The population a system needs before a High Grade Emission is worth mentioning at all.</summary>
    public const long MinimumPopulation = 1_000_000;

    /// <summary>The allegiances whose factions' states produce the state groups.</summary>
    private static readonly string[] StateAllegiances = ["Independent", "Alliance"];

    /// <summary>The six groups.</summary>
    public static readonly IReadOnlyList<EmissionGroup> Groups =
    [
        new(["Federation"], [], ["fedcorecomposites", "fedproprietarycomposites"]),
        new(["Empire"], [], ["imperialshielding"]),
        new(StateAllegiances, ["CivilUnrest"], ["improvisedcomponents"]),
        new(StateAllegiances, ["War", "CivilWar"], ["militarygradealloys", "militarysupercapacitors"]),

        // Expansion beside Boom.
        new(StateAllegiances, ["Boom", "Expansion"],
            ["protoheatradiators", "protolightalloys", "protoradiolicalloys"]),

        new(StateAllegiances, ["Outbreak"], ["pharmaceuticalisolators"]),
    ];

    /// <summary>
    /// Every group a system offers: the group of its own allegiance, and the group of each state any
    /// Independent or Alliance faction present is in. A minority faction's allegiance adds nothing.
    /// </summary>
    public static IReadOnlyList<EmissionGroup> For(string? systemAllegiance, IEnumerable<EmissionFaction>? factions)
    {
        var present = (factions ?? []).ToList();

        return
        [
            .. Groups.Where(group => group.States.Count == 0
                ? Includes(group.Allegiances, systemAllegiance)
                : present.Any(faction =>
                    Includes(group.Allegiances, faction.Allegiance)
                    && faction.States.Any(state => Includes(group.States, state)))),
        ];
    }

    private static bool Includes(IEnumerable<string> list, string? value) =>
        value is { Length: > 0 } && list.Contains(value, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The Systems search that finds where <paramref name="group"/> is produced: allegiance, the controlling
    /// faction's state, 500 light years, the population floor.
    /// </summary>
    public static IReadOnlyDictionary<string, string> SearchArguments(EmissionGroup group)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["distance"] = "500",
            ["allegiance"] = string.Join(",", group.Allegiances),
            ["population"] = MinimumPopulation.ToString(CultureInfo.InvariantCulture) + "-",
        };

        if (group.States.Count > 0)
        {
            arguments["state"] = string.Join(",", group.States.Select(FarmingAdvice.Spaced));
        }

        return arguments;
    }

    /// <summary>The search's conditions as a Commander reads them: "Independent or Alliance · Outbreak".</summary>
    public static IReadOnlyList<string> Conditions(EmissionGroup group)
    {
        var conditions = new List<string> { string.Join(" or ", group.Allegiances) };

        if (group.States.Count > 0)
        {
            conditions.Add(string.Join(" or ", group.States.Select(FarmingAdvice.Spaced)));
        }

        return conditions;
    }

    /// <summary>What one material needs, or null for one no emission carries.</summary>
    public static EmissionGroup? Holding(string? symbol) =>
        symbol is { Length: > 0 }
            ? Groups.FirstOrDefault(group =>
                group.Materials.Any(material =>
                    string.Equals(material, symbol, StringComparison.OrdinalIgnoreCase)))
            : null;
}
