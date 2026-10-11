using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The Search tab, with the System page as its root.</summary>
internal sealed class SearchFurnishing(IPanelTabHost host)
{
    private StarSystemPage? _starSystem;

    public void EnableStarSystem(StarSystemSurface surface) =>
        host.Furnish(
            PanelTab.Search,
            _ =>
            {
                _starSystem = new StarSystemPage(surface);
                _starSystem.FiltersChanged += (_, _) => host.ShowSearch();

                return _starSystem;
            },
            new NavCrumb(StarSystemPage.RootKey, "System") { Help = D47.Core.Capabilities.Builtin.GalaxyCapability.Id });

    /// <summary>Lets the System page follow the Commander while Search is the open tab; true when it redrew.</summary>
    public bool Tick() => host.Tab == PanelTab.Search && _starSystem is { } page && page.Tick();

    public bool OpenSystem(long systemAddress) => _starSystem is { } page && page.OpenSystem(systemAddress);
}
