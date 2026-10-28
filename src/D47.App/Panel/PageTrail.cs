using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Knowledge;
using D47.Core.Ships;

namespace D47.App.Panel;

/// <summary>Where the panel draws a <see cref="PageRef"/>: the root it sits under and the crumb that opens it.</summary>
public static class PageTrail
{
    /// <summary>The root and the crumbs below it for a page, or null where nothing draws it.</summary>
    public static (string Root, IReadOnlyList<NavCrumb> Crumbs)? For(PageRef page, Func<IReadOnlyList<FleetEntry>> fleet) =>
        Single(page, fleet) is { } single
            ? (single.Root, [single.Crumb])
            : page.Kind == PageKind.CodexEntry
                ? (LedgersPage.RootKey, [
                    new NavCrumb(CodexPage.PageKey, "Codex"),
                    CodexPage.RegionCrumb(page.Region),
                    CodexPage.EntryCrumb(page.Region, page.Id, page.Name ?? "Entry")])
                : null;

    private static (string Root, NavCrumb Crumb)? Single(PageRef page, Func<IReadOnlyList<FleetEntry>> fleet) =>
        page.Kind switch
        {
            PageKind.Engineer when EngineerDirectory.ById((int)page.Id) is { } engineer
                => (EngineersPages.DirectoryRoot, EngineersPages.Crumb(engineer)),
            PageKind.Ship when fleet().FirstOrDefault(entry =>
                    entry.IsOwned && (entry.Stored?.ShipId == page.Id || entry.Build?.ShipId == page.Id)) is { } ship
                => (LoadoutPages.FleetRoot, LoadoutPages.Ship(ship)),
            PageKind.System => (StarSystemPage.RootKey, new NavCrumb(StarSystemPage.RootKey, "System")),
            PageKind.Plan => (RoutingPages.PlanRoot, RoutingPages.ResultCrumb((RoutePlanKind)page.Id, page.Name ?? "Plan")),
            PageKind.Search => SearchRoot((GalaxySearchKind)page.Id) is { } root ? (root, new NavCrumb(root, "Search")) : null,
            _ => null,
        };

    private static string? SearchRoot(GalaxySearchKind kind) =>
        kind switch
        {
            GalaxySearchKind.Systems => SystemSearchPage.RootKey,
            GalaxySearchKind.Stations => StationSearchPage.RootKey,
            GalaxySearchKind.Outfitting => OutfittingSearchPage.RootKey,
            GalaxySearchKind.Bodies => BodySearchPage.RootKey,
            GalaxySearchKind.Signals => SignalSearchPage.RootKey,
            _ => null,
        };

    /// <summary>
    /// Puts a surface on the root, then on the page; true when it arrived. A surface holding a modal page stays
    /// where it is.
    /// </summary>
    public static bool Open(PanelNavigator nav, string root, NavCrumb crumb) => Open(nav, root, [crumb]);

    /// <summary>Puts a surface on the root, then at the end of the crumbs; true when it arrived.</summary>
    public static bool Open(PanelNavigator nav, string root, IReadOnlyList<NavCrumb> crumbs)
    {
        nav.Show(root);

        return nav.Root.Key == root && nav.GoTo(crumbs);
    }

    /// <summary>
    /// Opens the page on every surface, each through its own thread. A system page is opened on the system
    /// through the surface's <c>OpenSystem</c>, which a surface with no System page leaves null. A surface
    /// <paramref name="held"/> by a prompt stays where it is.
    /// </summary>
    public static void OpenEverywhere(
        PageRef page,
        IEnumerable<(PanelNavigator Nav, Action<Action> Post, Action<long>? OpenSystem)> surfaces,
        Func<IReadOnlyList<FleetEntry>> fleet,
        Func<PanelNavigator, bool>? held = null)
    {
        if (For(page, fleet) is not { } target)
        {
            return;
        }

        foreach (var (nav, post, openSystem) in surfaces)
        {
            post(() =>
            {
                if (held?.Invoke(nav) != true && Open(nav, target.Root, target.Crumbs) && page.Kind == PageKind.System)
                {
                    openSystem?.Invoke(page.Id);
                }
            });
        }
    }
}
