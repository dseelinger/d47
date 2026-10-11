using System.Globalization;
using Avalonia.Controls;
using D47.Core.Capabilities.Builtin;
using D47.App.Controls;
using D47.Core.Interface;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The Search tab: the System page, then Systems, Stations and Bodies where the host gives it a way to search.</summary>
internal sealed class SearchFurnishing(IPanelTabHost host)
{
    /// <summary>Opens the System page on one system, by its id64, beneath Systems.</summary>
    public const string FoundKeyPrefix = "search.systems.found:";

    private StarSystemPage? _starSystem;
    private SystemSearchPage? _systems;
    private StationSearchPage? _stations;
    private BodySearchPage? _bodies;
    private StarSystemPage? _found;

    public void EnableStarSystem(StarSystemSurface surface)
    {
        var roots = new List<NavCrumb> { new(StarSystemPage.RootKey, "System") { Help = GalaxyCapability.Id } };

        if (surface.Searches is not null)
        {
            roots.Add(new NavCrumb(SystemSearchPage.RootKey, "Systems") { Help = GalaxyCapability.Id });
            roots.Add(new NavCrumb(StationSearchPage.RootKey, "Stations") { Help = GalaxyCapability.Id });
            roots.Add(new NavCrumb(BodySearchPage.RootKey, "Bodies") { Help = GalaxyCapability.Id });
        }

        host.Furnish(PanelTab.Search, crumb => Build(crumb, surface), [.. roots]);
    }

    /// <summary>Lets the Search pages follow the Commander and the setting while Search is the open tab; true when one redrew.</summary>
    public bool Tick()
    {
        if (host.Tab != PanelTab.Search)
        {
            return false;
        }

        var redrawn = _starSystem is { } page && page.Tick();
        redrawn |= _found is { } found
            && host.Nav.TrailOf(PanelTab.Search) is { Count: > 1 } trail
            && trail[^1].Key.StartsWith(FoundKeyPrefix, StringComparison.Ordinal)
            && found.Tick();
        redrawn |= _systems is { } systems && host.Nav.RootKeyOf(PanelTab.Search) == SystemSearchPage.RootKey && systems.Tick();
        redrawn |= _stations is { } stations && host.Nav.RootKeyOf(PanelTab.Search) == StationSearchPage.RootKey && stations.Tick();
        redrawn |= _bodies is { } bodies && host.Nav.RootKeyOf(PanelTab.Search) == BodySearchPage.RootKey && bodies.Tick();

        return redrawn;
    }

    public bool OpenSystem(long systemAddress) => _starSystem is { } page && page.OpenSystem(systemAddress);

    private Control Build(NavCrumb crumb, StarSystemSurface surface)
    {
        if (crumb.Key == SystemSearchPage.RootKey && surface.Searches is { } searches)
        {
            return _systems = new SystemSearchPage(
                searches,
                surface.LookupsEnabled,
                () => surface.Here()?.StarSystem,
                OpenFound,
                surface.OpenSettings,
                host.Headset);
        }

        if (crumb.Key == StationSearchPage.RootKey && surface.Searches is { } stationSearches)
        {
            return _stations = new StationSearchPage(
                stationSearches,
                surface.LookupsEnabled,
                () => surface.Here()?.StarSystem,
                ChooseServices,
                surface.OpenSettings,
                host.Headset);
        }

        if (crumb.Key == BodySearchPage.RootKey && surface.Searches is { } bodySearches)
        {
            return _bodies = new BodySearchPage(
                bodySearches,
                surface.LookupsEnabled,
                () => surface.Here()?.StarSystem,
                surface.OpenSettings,
                host.Headset);
        }

        if (crumb.Key.StartsWith(FoundKeyPrefix, StringComparison.Ordinal)
            && long.TryParse(crumb.Key[FoundKeyPrefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var address))
        {
            var found = _found = new StarSystemPage(surface);
            found.FiltersChanged += (_, _) => host.ShowSearch();
            found.OpenSystem(address);

            return found;
        }

        _starSystem = new StarSystemPage(surface);
        _starSystem.FiltersChanged += (_, _) => host.ShowSearch();

        return _starSystem;
    }

    private void ChooseServices(IReadOnlyList<string> current, Action<IReadOnlyList<string>> chosen) =>
        host.Prompts.Pick(
            StationSearchPage.ServicesKey,
            "Services",
            new PickerRequest
            {
                Prompt = "Services",
                Help = StationSearchPage.ServicesHelp,
                Choices = [.. StationQuery.ServiceNames.Select(service => service.Name)],
                Several = true,
                CurrentSeveral = current,
                DefaultDisplay = "ANY SERVICES",
            },
            result => chosen(result.Values ?? []));

    private void OpenFound(SystemSummary system)
    {
        if (system.SystemAddress is not { } address)
        {
            return;
        }

        var crumb = new NavCrumb(FoundKeyPrefix + address.ToString(CultureInfo.InvariantCulture), system.Name)
        {
            Help = GalaxyCapability.Id,
        };

        // A row pressed under one already open replaces it rather than stacking a second.
        while (host.Tab == PanelTab.Search && host.Nav.TrailOf(PanelTab.Search).Count > 1 && host.Nav.Back())
        {
        }

        host.Nav.Drill(PanelTab.Search, crumb);
    }
}
