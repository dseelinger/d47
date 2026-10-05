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
            PageKind.Engineer when EngineerDirectory.ById(page.Id) is { } engineer
                => (EngineersPages.DirectoryRoot, EngineersPages.Crumb(engineer)),
            PageKind.Ship when fleet().FirstOrDefault(entry =>
                    entry.IsOwned && (entry.Stored?.ShipId == page.Id || entry.Build?.ShipId == page.Id)) is { } ship
                => (LoadoutPages.FleetRoot, LoadoutPages.Ship(ship)),
            _ => null,
        };

    /// <summary>Puts a surface on the root, then on the page. A surface holding a modal page stays where it is.</summary>
    public static void Open(PanelNavigator nav, string root, NavCrumb crumb)
    {
        nav.Show(root);

        if (nav.Root.Key == root)
        {
            nav.GoTo(crumb);
        }
    }

    /// <summary>Opens the page on every surface, each through its own thread.</summary>
    public static void OpenEverywhere(
        PageRef page,
        IEnumerable<(PanelNavigator Nav, Action<Action> Post)> surfaces,
        Func<IReadOnlyList<FleetEntry>> fleet)
    {
        if (For(page, fleet) is not { } target)
        {
            return;
        }

        foreach (var (nav, post) in surfaces)
        {
            post(() => Open(nav, target.Root, target.Crumb));
        }
    }
}
