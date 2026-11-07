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

    /// <summary>Ends a found-system key that opens the System page on its Stations section.</summary>
    public const string StationsSuffix = ":stations";

    /// <summary>Ends a found-system key that opens the System page on its Bodies section.</summary>
    public const string BodiesSuffix = ":bodies";

    private StarSystemPage? _starSystem;
    private SystemSearchPage? _systems;
    private StationSearchPage? _stations;
    private OutfittingSearchPage? _outfitting;
    private FactionPage? _faction;
    private BodySearchPage? _bodies;
    private SignalSearchPage? _signals;
    private RareGoodsPage? _rares;
    private StarSystemPage? _found;
    private SystemBodyPage? _body;
    private SystemStationPage? _station;
    private StarSystemProfile? _drawn;
    private StarSystemSurface? _surface;
    private (string Query, IReadOnlyList<PanelFindResult> Results, Action<PanelFindResult> Open)? _allResults;
    private string? _foundStation;
    private string? _foundSeek;
    private (long Address, string Id)? _pendingSystem;
    private string? _pendingName;
    private (long Address, string? Station)? _pendingAddress;
    private string? _pendingFaction;
    private string? _pendingFactionPage;
    private string? _stationSeek;
    private string? _systemEntry;
    private readonly Dictionary<GalaxySearchKind, SearchHistoryEntry> _pending = [];
    private IReadOnlyDictionary<string, string>? _pendingBodies;
    private (IReadOnlyDictionary<string, string> Arguments, string Rare, bool PowerOpen)? _pendingRareRun;

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

            if (surface.Searches.Faction is not null)
            {
                roots.Add(new NavCrumb(FactionPage.RootKey, "Faction") { Help = GalaxyCapability.Id });
            }

            roots.Add(new NavCrumb(StationSearchPage.RootKey, "Stations") { Help = GalaxyCapability.Id });
            roots.Add(new NavCrumb(OutfittingSearchPage.RootKey, "Outfitting") { Help = GalaxyCapability.Id });
            roots.Add(new NavCrumb(BodySearchPage.RootKey, "Bodies") { Help = GalaxyCapability.Id });
            roots.Add(new NavCrumb(SignalSearchPage.RootKey, "Signals") { Help = GalaxyCapability.Id });
            roots.Add(new NavCrumb(RareGoodsPage.RootKey, "Rare goods") { Help = GalaxyCapability.Id });
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
        redrawn |= _body is { } body
            && host.Nav.TrailOf(PanelTab.Search) is { Count: > 1 } bodyTrail
            && SystemBodyPage.IsKey(bodyTrail[^1].Key)
            && body.Tick();
        redrawn |= _station is { } station
            && host.Nav.TrailOf(PanelTab.Search) is { Count: > 1 } stationTrail
            && SystemStationPage.IsKey(stationTrail[^1].Key)
            && station.Tick();
        redrawn |= _systems is { } systems && host.Nav.RootKeyOf(PanelTab.Search) == SystemSearchPage.RootKey && systems.Tick();
        redrawn |= _faction is { } faction && host.Nav.RootKeyOf(PanelTab.Search) == FactionPage.RootKey && faction.Tick();
        redrawn |= _stations is { } stations && host.Nav.RootKeyOf(PanelTab.Search) == StationSearchPage.RootKey && stations.Tick();
        redrawn |= _outfitting is { } outfitting && host.Nav.RootKeyOf(PanelTab.Search) == OutfittingSearchPage.RootKey && outfitting.Tick();
        redrawn |= _bodies is { } bodies && host.Nav.RootKeyOf(PanelTab.Search) == BodySearchPage.RootKey && bodies.Tick();
        redrawn |= _signals is { } signals && host.Nav.RootKeyOf(PanelTab.Search) == SignalSearchPage.RootKey && signals.Tick();
        redrawn |= _rares is { } rares && host.Nav.RootKeyOf(PanelTab.Search) == RareGoodsPage.RootKey && rares.Tick();

        return redrawn;
    }

    /// <summary>The way to run a body search from another tab, or null where this surface has no search.</summary>
    public BodySearchLink? BodySearch =>
        _surface is { Searches: not null } surface ? new BodySearchLink(SearchBodies) : null;

    private void SearchBodies(IReadOnlyDictionary<string, string> arguments)
    {
        _pending.Clear();
        _pendingSystem = null;
        _pendingBodies = arguments;
        host.Nav.Show(BodySearchPage.RootKey);
        ApplyPending();
    }

    /// <summary>Opens Search › System on <paramref name="name"/>, as typing it there and pressing Enter does.</summary>
    public void OpenSystemNamed(string name)
    {
        _pending.Clear();
        _pendingSystem = null;
        _pendingName = name;
        host.Nav.Show(StarSystemPage.RootKey);
        ApplyPending();
    }

    /// <summary>Opens Search › System on one system, and on its Stations section narrowed to <paramref name="station"/> when one is named.</summary>
    public void OpenSystemAt(long address, string? station)
    {
        ClearPending();
        _pendingAddress = (address, station);
        host.Nav.Show(StarSystemPage.RootKey);
        ApplyPending();
    }

    /// <summary>Opens Search › Faction on <paramref name="faction"/>; a surface with no faction report runs the Systems search with it as a faction present.</summary>
    public void OpenFaction(string faction)
    {
        ClearPending();

        if (_surface?.Searches?.Faction is not null)
        {
            _pendingFactionPage = faction;
            host.Nav.Show(FactionPage.RootKey);
        }
        else
        {
            _pendingFaction = faction;
            host.Nav.Show(SystemSearchPage.RootKey);
        }

        ApplyPending();
    }

    private void ClearPending()
    {
        _pending.Clear();
        _pendingBodies = null;
        _pendingRareRun = null;
        _pendingSystem = null;
        _pendingName = null;
        _pendingAddress = null;
        _pendingFaction = null;
        _pendingFactionPage = null;
        _stationSeek = null;
    }

    /// <summary>Fills the panel's page-search box with the station once the System page lists its stations.</summary>
    private void SeekStation()
    {
        if (_stationSeek is { } station && _starSystem is { Filters: true })
        {
            _stationSeek = null;
            host.Seek(station);
        }
    }

    /// <summary>Opens Search › System › All results on the list Find held for <paramref name="query"/>.</summary>
    public void OpenAllResults(string query, IReadOnlyList<PanelFindResult> results, Action<PanelFindResult> open)
    {
        _allResults = (query, results, result => ChooseFromAllResults(result, open));
        host.Nav.Show(StarSystemPage.RootKey);
        host.Forget(PanelTab.Search, key => key == AllResultsPage.PageKey);
        host.Nav.Drill(PanelTab.Search, new NavCrumb(AllResultsPage.PageKey, "All results") { Whole = true, Starts = true, Local = true });
    }

    /// <summary>
    /// Drills a system, station or carrier with a known system under All results, a station or carrier on its
    /// Stations section narrowed to its name; any other result opens as it does from Find.
    /// </summary>
    private void ChooseFromAllResults(PanelFindResult result, Action<PanelFindResult> open)
    {
        if (result is not { SystemAddress: { } address, Kind: PanelFindKind.StarSystem or PanelFindKind.Station or PanelFindKind.Carrier })
        {
            open(result);
            return;
        }

        var station = result.Kind == PanelFindKind.StarSystem ? null : result.Target;

        while (host.Nav.TrailOf(PanelTab.Search) is { Count: > 1 } trail && trail[^1].Key != AllResultsPage.PageKey && host.Nav.Back())
        {
        }

        var key = FoundKeyPrefix + address.ToString(CultureInfo.InvariantCulture) + (station is null ? string.Empty : StationsSuffix);

        _foundStation = station;
        host.Forget(PanelTab.Search, built => built == key);
        host.Nav.Drill(PanelTab.Search, new NavCrumb(key, result.Title) { Help = GalaxyCapability.Id, Local = true });
    }

    /// <summary>Fills the page-search box with the station once the drilled system page lists its stations.</summary>
    private void SeekFoundStation()
    {
        if (_foundSeek is { } station && _found is { Filters: true })
        {
            _foundSeek = null;
            host.Seek(station);
        }
    }

    public bool OpenSystem(long systemAddress) => _starSystem is { } page && page.OpenSystem(systemAddress);

    private Control Build(NavCrumb crumb, StarSystemSurface surface)
    {
        if (crumb.Key == SystemSearchPage.RootKey && surface.Searches is { } searches)
        {
            _systems = new SystemSearchPage(
                searches,
                () => surface.Here()?.StarSystem,
                OpenFound,
                host.Copy,
                host.Headset)
            {
                PaneWidths = host.PaneWidths,
                ColumnWidths = host.ColumnWidths,
            };
            ApplyPending();

            return _systems;
        }

        if (crumb.Key == FactionPage.RootKey && surface.Searches is { Faction: not null } factionSearches)
        {
            var open = _pendingFactionPage;
            _pendingFactionPage = null;

            return _faction = new FactionPage(factionSearches, host.Headset, open) { ColumnWidths = host.ColumnWidths, Copy = host.Copy };
        }

        if (crumb.Key == StationSearchPage.RootKey && surface.Searches is { } stationSearches)
        {
            _stations = new StationSearchPage(
                stationSearches,
                () => surface.Here()?.StarSystem,
                ChooseServices,
                OpenResult,
                host.Copy,
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
                () => surface.Here()?.StarSystem,
                OpenFound,
                host.Copy,
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
                () => surface.Here()?.StarSystem,
                OpenResult,
                host.Copy,
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
                () => surface.Here()?.StarSystem,
                OpenFound,
                host.Copy,
                host.Headset)
            {
                PaneWidths = host.PaneWidths,
                ColumnWidths = host.ColumnWidths,
            };
            ApplyPending();

            return _signals;
        }

        if (crumb.Key == RareGoodsPage.RootKey && surface.Searches is { } rareSearches)
        {
            _rares = new RareGoodsPage(
                rareSearches,
                () => surface.Here()?.StarSystem,
                () => rareSearches.PledgedPower?.Invoke(),
                RunRare,
                host.Copy,
                host.Headset)
            {
                ColumnWidths = host.ColumnWidths,
            };

            return _rares;
        }

        if (crumb.Key == AllResultsPage.PageKey)
        {
            var (query, results, open) = _allResults ?? (string.Empty, [], _ => { });

            return new AllResultsPage(query, results, open);
        }

        if (SystemBodyPage.Parse(crumb.Key) is var (bodyAddress, bodyId))
        {
            var profile = _drawn?.SystemAddress == bodyAddress ? _drawn : null;

            return _body = new SystemBodyPage(surface, bodyAddress, bodyId, crumb.Word, profile, Codex, OpenBody, () => host.Nav.Back(), OpenStation);
        }

        if (SystemStationPage.Parse(crumb.Key) is var (stationAddress, marketId))
        {
            var profile = _drawn?.SystemAddress == stationAddress ? _drawn : null;

            return _station = new SystemStationPage(surface, stationAddress, marketId, crumb.Word, profile, () => host.Nav.Back());
        }

        if (crumb.Key.StartsWith(FoundKeyPrefix, StringComparison.Ordinal)
            && FoundTarget(crumb.Key) is var (address, suffix))
        {
            var found = _found = new StarSystemPage(surface, host.Copy) { OpenBody = OpenBody, OpenStation = OpenStation, Codex = Codex };
            found.FiltersChanged += (_, _) =>
            {
                host.ShowSearch();
                SeekFoundStation();
            };

            if (_foundStation is { } station && suffix == StationsSuffix)
            {
                _foundStation = null;
                _foundSeek = station;
                found.OpenSystemStation(address, station);

                return found;
            }

            _foundStation = null;

            _ = suffix switch
            {
                StationsSuffix => found.OpenSystemStations(address),
                BodiesSuffix => found.OpenSystemBodies(address),
                _ => found.OpenSystem(address),
            };

            return found;
        }

        _starSystem = new StarSystemPage(surface, host.Copy) { OpenBody = OpenBody, OpenStation = OpenStation, Codex = Codex };
        _starSystem.FiltersChanged += (_, _) =>
        {
            host.ShowSearch();
            SeekStation();
        };
        ApplyPending();

        return _starSystem;
    }

    /// <summary>Opens Search › Stations filled in for a rare run from <paramref name="row"/>'s rare, and runs it.</summary>
    private void RunRare(RareListingRow row)
    {
        var power = _surface?.Searches?.PledgedPower?.Invoke();

        ClearPending();
        _pendingRareRun = (RareRunSearch.StationArguments(row.Rare, power), row.Rare.Name, power is null);
        host.Nav.Show(StationSearchPage.RootKey);
        ApplyPending();
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
        if (_pendingName is { } name && _starSystem is { } named)
        {
            _pendingName = null;
            named.LookUp(name);
        }

        if (_pendingAddress is { } at && _starSystem is { } target)
        {
            _pendingAddress = null;
            _systemEntry = null;

            if (at.Station is { } station)
            {
                _stationSeek = station;
                target.OpenSystemStation(at.Address, station);
            }
            else
            {
                target.OpenSystem(at.Address);
            }
        }

        if (_pendingFactionPage is { } factionName && _faction is { } factionPage)
        {
            _pendingFactionPage = null;
            factionPage.Show(factionName);
        }

        if (_pendingFaction is { } faction && _systems is { } search)
        {
            _pendingFaction = null;
            search.Run(new Dictionary<string, string>(StringComparer.Ordinal) { ["faction"] = faction });
        }

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

        if (_pendingRareRun is { } rareRun && _stations is { } stationSearch)
        {
            _pendingRareRun = null;
            stationSearch.RunRareRun(rareRun.Arguments, rareRun.Rare, rareRun.PowerOpen);
        }

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

    /// <summary>The Codex pictures the body pages draw, or null where this surface has none.</summary>
    public CodexPictureSet? CodexPictures { get; set; }

    private BodyCodex Codex => new(
        CodexCatalogue.Table,
        CodexPictures,
        host.Nav.CanShow(CodexPage.PageKey) ? OpenCodexEntry : null,
        host.Nav.CanShow(CodexPage.PageKey) ? OpenGenus : null);

    private void OpenCodexEntry(CodexEntry entry) =>
        PageTrail.Open(host.Nav, LedgersPage.RootKey, [
            LedgersPage.Ledger(CodexPage.PageKey, "Codex"),
            CodexPage.RegionCrumb(null),
            CodexPage.EntryCrumb(null, entry.EntryId, entry.Name)]);

    /// <summary>Opens Commander › Codex with its search set to the genus.</summary>
    private void OpenGenus(string genus)
    {
        host.Nav.Show(CodexPage.PageKey);

        if (host.Nav.Trail is { Count: > 0 } trail && trail[^1].Key == CodexPage.PageKey)
        {
            host.Seek(genus);
        }
    }

    /// <summary>Drills a body's page under the page it was pressed on, keeping the record already read.</summary>
    private void OpenBody(StarSystemProfile profile, BodyProfile body)
    {
        _drawn = profile;
        host.Nav.Drill(PanelTab.Search, SystemBodyPage.Crumb(profile.SystemAddress, body));
    }

    /// <summary>Drills a station's page under the page it was pressed on, keeping the record already read.</summary>
    private void OpenStation(StarSystemProfile profile, StationProfile station)
    {
        if (SystemStationPage.Crumb(profile.SystemAddress, station) is { } crumb)
        {
            _drawn = profile;
            host.Nav.Drill(PanelTab.Search, crumb);
        }
    }

    /// <summary>Opens a Stations result's system on its Stations section, with the station's page drilled under it where the result names the station.</summary>
    private void OpenResult(StationSummary station)
    {
        OpenFound(station.SystemName, station.SystemAddress, StationsSuffix);

        if (station is { SystemAddress: { } address, MarketId: { } marketId }
            && host.Nav.TrailOf(PanelTab.Search) is { Count: > 1 } trail
            && trail[^1].Key == FoundKeyPrefix + address.ToString(CultureInfo.InvariantCulture) + StationsSuffix)
        {
            host.Nav.Drill(PanelTab.Search, SystemStationPage.Crumb(address, marketId, station.Name));
        }
    }

    /// <summary>Opens a Bodies result's system on its Bodies section, with the body's page drilled under it where the result names the body.</summary>
    private void OpenResult(BodySummary body)
    {
        OpenFound(body.SystemName, body.SystemAddress, BodiesSuffix);

        if (body is { SystemAddress: { } address, BodyId: { } id }
            && host.Nav.TrailOf(PanelTab.Search) is { Count: > 1 } trail
            && trail[^1].Key == FoundKeyPrefix + address.ToString(CultureInfo.InvariantCulture) + BodiesSuffix)
        {
            host.Nav.Drill(PanelTab.Search, SystemBodyPage.Crumb(address, id, body.Name));
        }
    }

    private static (long Address, string Suffix)? FoundTarget(string key)
    {
        var rest = key[FoundKeyPrefix.Length..];
        var suffix = string.Empty;

        foreach (var candidate in new[] { StationsSuffix, BodiesSuffix })
        {
            if (rest.EndsWith(candidate, StringComparison.Ordinal))
            {
                suffix = candidate;
                rest = rest[..^candidate.Length];
                break;
            }
        }

        return long.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out var address) ? (address, suffix) : null;
    }

    private void OpenFound(string name, long? systemAddress) => OpenFound(name, systemAddress, string.Empty);

    private void OpenFound(string name, long? systemAddress, string suffix)
    {
        if (systemAddress is not { } address)
        {
            return;
        }

        var crumb = new NavCrumb(FoundKeyPrefix + address.ToString(CultureInfo.InvariantCulture) + suffix, name)
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
