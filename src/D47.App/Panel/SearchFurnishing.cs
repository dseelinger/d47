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
    private OutfittingSearchPage? _outfitting;
    private BodySearchPage? _bodies;
    private SignalSearchPage? _signals;
    private StarSystemPage? _found;
    private StarSystemSurface? _surface;
    private (long Address, string Id)? _pendingSystem;
    private string? _systemEntry;
    private readonly Dictionary<GalaxySearchKind, SearchHistoryEntry> _pending = [];
    private IReadOnlyDictionary<string, string>? _pendingBodies;

    public void EnableStarSystem(StarSystemSurface surface)
    {
        if (surface.Searches is { } searches)
        {
            surface = surface with { Searches = searches with { OpenHistory = OpenHistory } };
        }

        _surface = surface;

        var roots = new List<NavCrumb> { new(StarSystemPage.RootKey, "System") { Help = GalaxyCapability.Id } };

        if (surface.Searches is not null)
        {
            roots.Add(new NavCrumb(SystemSearchPage.RootKey, "Systems") { Help = GalaxyCapability.Id });
            roots.Add(new NavCrumb(StationSearchPage.RootKey, "Stations") { Help = GalaxyCapability.Id });
            roots.Add(new NavCrumb(OutfittingSearchPage.RootKey, "Outfitting") { Help = GalaxyCapability.Id });
            roots.Add(new NavCrumb(BodySearchPage.RootKey, "Bodies") { Help = GalaxyCapability.Id });
            roots.Add(new NavCrumb(SignalSearchPage.RootKey, "Signals") { Help = GalaxyCapability.Id });
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
        redrawn |= _outfitting is { } outfitting && host.Nav.RootKeyOf(PanelTab.Search) == OutfittingSearchPage.RootKey && outfitting.Tick();
        redrawn |= _bodies is { } bodies && host.Nav.RootKeyOf(PanelTab.Search) == BodySearchPage.RootKey && bodies.Tick();
        redrawn |= _signals is { } signals && host.Nav.RootKeyOf(PanelTab.Search) == SignalSearchPage.RootKey && signals.Tick();

        return redrawn;
    }

    /// <summary>The way to run a body search from another tab, or null where this surface has no search.</summary>
    public BodySearchLink? BodySearch =>
        _surface is { Searches: not null } surface ? new BodySearchLink(surface.LookupsEnabled, SearchBodies) : null;

    private void SearchBodies(IReadOnlyDictionary<string, string> arguments)
    {
        _pending.Clear();
        _pendingSystem = null;
        _pendingBodies = arguments;
        host.Nav.Show(BodySearchPage.RootKey);
        ApplyPending();
    }

    public bool OpenSystem(long systemAddress) => _starSystem is { } page && page.OpenSystem(systemAddress);

    private Control Build(NavCrumb crumb, StarSystemSurface surface)
    {
        if (crumb.Key == SystemSearchPage.RootKey && surface.Searches is { } searches)
        {
            _systems = new SystemSearchPage(
                searches,
                surface.LookupsEnabled,
                () => surface.Here()?.StarSystem,
                OpenFound,
                surface.OpenSettings,
                host.Headset)
            {
                PaneWidths = host.PaneWidths,
                ColumnWidths = host.ColumnWidths,
            };
            ApplyPending();

            return _systems;
        }

        if (crumb.Key == StationSearchPage.RootKey && surface.Searches is { } stationSearches)
        {
            _stations = new StationSearchPage(
                stationSearches,
                surface.LookupsEnabled,
                () => surface.Here()?.StarSystem,
                ChooseServices,
                surface.OpenSettings,
                host.Headset)
            {
                PaneWidths = host.PaneWidths,
                ColumnWidths = host.ColumnWidths,
            };
            ApplyPending();

            return _stations;
        }

        if (crumb.Key == OutfittingSearchPage.RootKey && surface.Searches is { } outfittingSearches)
        {
            _outfitting = new OutfittingSearchPage(
                outfittingSearches,
                surface.LookupsEnabled,
                () => surface.Here()?.StarSystem,
                OpenFound,
                host.Copy,
                surface.OpenSettings,
                host.Headset)
            {
                PaneWidths = host.PaneWidths,
                ColumnWidths = host.ColumnWidths,
            };
            ApplyPending();

            return _outfitting;
        }

        if (crumb.Key == BodySearchPage.RootKey && surface.Searches is { } bodySearches)
        {
            _bodies = new BodySearchPage(
                bodySearches,
                surface.LookupsEnabled,
                () => surface.Here()?.StarSystem,
                surface.OpenSettings,
                host.Headset)
            {
                PaneWidths = host.PaneWidths,
                ColumnWidths = host.ColumnWidths,
            };
            ApplyPending();

            return _bodies;
        }

        if (crumb.Key == SignalSearchPage.RootKey && surface.Searches is { } signalSearches)
        {
            _signals = new SignalSearchPage(
                signalSearches,
                surface.LookupsEnabled,
                () => surface.Here()?.StarSystem,
                OpenFound,
                host.Copy,
                surface.OpenSettings,
                host.Headset)
            {
                PaneWidths = host.PaneWidths,
                ColumnWidths = host.ColumnWidths,
            };
            ApplyPending();

            return _signals;
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
        ApplyPending();

        return _starSystem;
    }

    private void OpenHistory()
    {
        if (_surface?.Searches is not { History: { } history }
            || history.All() is not { Count: > 0 } entries)
        {
            return;
        }

        host.Prompts.Pick(
            SearchHistoryPicker.Key,
            SearchHistoryPicker.Word,
            SearchHistoryPicker.Request(entries, ShownEntryId(), DateTimeOffset.UtcNow),
            result =>
            {
                if (entries.FirstOrDefault(entry => entry.Id == result.Value) is { } chosen)
                {
                    OpenEntry(chosen);
                }
            });
    }

    /// <summary>The history entry the open root draws, or null when it draws anything else.</summary>
    private string? ShownEntryId() =>
        host.Nav.RootKeyOf(PanelTab.Search) switch
        {
            SystemSearchPage.RootKey => _systems?.HistoryId,
            StationSearchPage.RootKey => _stations?.HistoryId,
            OutfittingSearchPage.RootKey => _outfitting?.HistoryId,
            BodySearchPage.RootKey => _bodies?.HistoryId,
            SignalSearchPage.RootKey => _signals?.HistoryId,
            _ => _pendingSystem is null && _systemEntry is not null && _surface?.Searches is { History: { } history }
                && history.All().FirstOrDefault(entry => entry.Id == _systemEntry)?.SystemAddress == _starSystem?.ShownAddress
                    ? _systemEntry
                    : null,
        };

    /// <summary>Opens the root for <paramref name="entry"/>'s kind and has its page draw the entry.</summary>
    private void OpenEntry(SearchHistoryEntry entry)
    {
        _pending.Clear();
        _pendingBodies = null;
        _pendingSystem = null;

        if (entry.Tool == "describe_system" && entry.SystemAddress is { } address)
        {
            _pendingSystem = (address, entry.Id);
            host.Nav.Show(StarSystemPage.RootKey);
        }
        else if (SearchHistoryPicker.KindOf(entry) is { } kind)
        {
            _pending[kind] = entry;
            host.Nav.Show(kind switch
            {
                GalaxySearchKind.Systems => SystemSearchPage.RootKey,
                GalaxySearchKind.Stations => StationSearchPage.RootKey,
                GalaxySearchKind.Outfitting => OutfittingSearchPage.RootKey,
                GalaxySearchKind.Signals => SignalSearchPage.RootKey,
                _ => BodySearchPage.RootKey,
            });
        }

        ApplyPending();
    }

    /// <summary>Hands a waiting entry to its page, once that page exists.</summary>
    private void ApplyPending()
    {
        if (_pendingSystem is { } system && _starSystem is { } page)
        {
            _pendingSystem = null;
            _systemEntry = system.Id;
            page.OpenSystem(system.Address);
        }

        Deliver(GalaxySearchKind.Systems, _systems);
        Deliver(GalaxySearchKind.Stations, _stations);
        Deliver(GalaxySearchKind.Outfitting, _outfitting);
        Deliver(GalaxySearchKind.Bodies, _bodies);
        Deliver(GalaxySearchKind.Signals, _signals);

        if (_pendingBodies is { } bodies && _bodies is { } page2)
        {
            _pendingBodies = null;
            page2.Run(bodies);
        }
    }

    private void Deliver(GalaxySearchKind kind, GalaxySearchPage? page)
    {
        if (page is not null && _pending.Remove(kind, out var entry))
        {
            page.ShowEntry(entry);
        }
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

    private void OpenFound(SystemSummary system) => OpenFound(system.Name, system.SystemAddress);

    private void OpenFound(string name, long? systemAddress)
    {
        if (systemAddress is not { } address)
        {
            return;
        }

        var crumb = new NavCrumb(FoundKeyPrefix + address.ToString(CultureInfo.InvariantCulture), name)
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
