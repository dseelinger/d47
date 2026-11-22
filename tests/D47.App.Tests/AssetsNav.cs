using D47.App.Panel;
using D47.Core.Interface;

namespace D47.App.Tests;

/// <summary>Furnishes Commander › Assets on a bare navigator, as <see cref="AssetsFurnishing"/> does on a panel.</summary>
internal static class AssetsNav
{
    /// <summary>Registers the Assets root on Commander and declares each page under it, in the page sidebar's order.</summary>
    public static PanelNavigator Pages(PanelNavigator nav, params (string Key, string Word)[] pages)
    {
        nav.Register(PanelTab.Commander, AssetsFurnishing.Root);

        foreach (var (key, word) in pages)
        {
            nav.Declare(PanelTabNames.AssetsRoot, new NavCrumb(key, word) { Starts = true, Level = PanelTabNames.AssetsRoot });
        }

        nav.HoldPages(PanelTabNames.AssetsRoot, AssetsFurnishing.Order);
        return nav;
    }
}
