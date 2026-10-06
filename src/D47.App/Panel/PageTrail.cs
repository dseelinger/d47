using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Knowledge;
using D47.Core.Ships;

namespace D47.App.Panel;

/// <summary>Where the panel draws a <see cref="PageRef"/>: the root it sits under and the crumb that opens it.</summary>
public static class PageTrail
{
    /// <summary>The root and crumb for a page, or null where nothing draws it.</summary>
    public static (string Root, NavCrumb Crumb)? For(PageRef page, Func<IReadOnlyList<FleetEntry>> fleet) =>
        page.Kind switch
        {
            PageKind.Engineer when EngineerDirectory.ById((int)page.Id) is { } engineer
                => (EngineersPages.DirectoryRoot, EngineersPages.Crumb(engineer)),
            PageKind.Ship when fleet().FirstOrDefault(entry =>
                    entry.IsOwned && (entry.Stored?.ShipId == page.Id || entry.Build?.ShipId == page.Id)) is { } ship
                => (LoadoutPages.FleetRoot, LoadoutPages.Ship(ship)),
            PageKind.System => (StarSystemPage.RootKey, new NavCrumb(StarSystemPage.RootKey, "System")),
            _ => null,
        };

    /// <summary>
    /// Puts a surface on the root, then on the page; true when it arrived. A surface holding a modal page stays
    /// where it is.
    /// </summary>
    public static bool Open(PanelNavigator nav, string root, NavCrumb crumb)
    {
        nav.Show(root);

        return nav.Root.Key == root && nav.GoTo(crumb);
    }

    /// <summary>
    /// Opens the page on every surface, each through its own thread. A system page is opened on the system
    /// through the surface's <c>OpenSystem</c>, which a surface with no System page leaves null.
    /// </summary>
    public static void OpenEverywhere(
        PageRef page,
        IEnumerable<(PanelNavigator Nav, Action<Action> Post, Action<long>? OpenSystem)> surfaces,
        Func<IReadOnlyList<FleetEntry>> fleet)
    {
        if (For(page, fleet) is not { } target)
        {
            return;
        }

        foreach (var (nav, post, openSystem) in surfaces)
        {
            post(() =>
            {
                if (Open(nav, target.Root, target.Crumb) && page.Kind == PageKind.System)
                {
                    openSystem?.Invoke(page.Id);
                }
            });
        }
    }
}
