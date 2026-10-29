using D47.App.Panel;
using D47.Core.Capabilities.Builtin;

namespace D47.App.Coverage;

/// <summary>The panel page each tool's answer is shown on, keyed by tool name. A tool with no entry has no Go button.</summary>
internal static class ToolPlaces
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["describe_system"] = StarSystemPage.RootKey,
        ["search_systems"] = SystemSearchPage.RootKey,
        ["search_stations"] = StationSearchPage.RootKey,
        ["search_outfitting"] = OutfittingSearchPage.RootKey,
        ["find_body"] = BodySearchPage.RootKey,
        ["find_signal"] = SignalSearchPage.RootKey,
        ["plot_route"] = RoutingPages.PlanRoot,
        [BookmarksCapability.ListTool] = RoutingPages.BookmarksRoot,
        ["get_checklist"] = CommanderFurnishing.ChecklistRoot,
    };

    /// <summary>The destination key for a tool, or null when no page shows what it returns.</summary>
    public static string? For(string tool) => All.TryGetValue(tool, out var key) ? key : null;
}
