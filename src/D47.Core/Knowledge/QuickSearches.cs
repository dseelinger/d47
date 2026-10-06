using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;

namespace D47.Core.Knowledge;

/// <summary>A named galaxy search with its tool and arguments fixed.</summary>
public sealed record QuickSearch(string Name, string Tool, IReadOnlyDictionary<string, string> Arguments);

/// <summary>The fixed quick searches, run by phrase with no model involved.</summary>
public static class QuickSearches
{
    private const string Stations = "search_stations";

    private static QuickSearch Of(string name, string tool, params (string Key, string Value)[] arguments) =>
        new(name, tool, arguments.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

    public static IReadOnlyList<QuickSearch> All { get; } =
    [
        Of("raw material trader", Stations, ("material_trader", "Raw")),
        Of("manufactured material trader", Stations, ("material_trader", "Manufactured")),
        Of("encoded material trader", Stations, ("material_trader", "Encoded")),
        Of("guardian technology broker", Stations, ("technology_broker", "Guardian")),
        Of("human technology broker", Stations, ("technology_broker", "Human")),
        Of("interstellar factors", Stations, ("services", "Interstellar Factors")),
        Of("universal cartographics", Stations, ("services", "Universal Cartographics")),
        Of("vista genomics", Stations, ("services", "Vista Genomics")),
        Of("black market", Stations, ("services", "Black Market")),
        Of("pioneer supplies", Stations, ("services", "Pioneer Supplies")),
        Of("anarchy outbreak", "search_systems", ("government", "Anarchy"), ("state", "Outbreak")),
    ];

    /// <summary>The four phrases of each quick search.</summary>
    public static IEnumerable<string> PhrasesOf(QuickSearch search) =>
    [
        $"{search.Name} search",
        $"run the {search.Name} search",
        $"nearest {search.Name}",
        $"find the nearest {search.Name}",
    ];

    public static IEnumerable<DynamicCommand> Phrases() =>
        All.SelectMany(search => PhrasesOf(search).Select(phrase =>
            new DynamicCommand(phrase, GalaxyCapability.Id, search.Tool, search.Arguments)));
}
