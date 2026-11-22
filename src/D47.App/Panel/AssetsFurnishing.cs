using Avalonia.Controls;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// Commander › Assets: Ships, Stored modules, Crew, Characters, Suits, Carrier, Construction, Materials and Engineers,
/// the pages of its page sidebar.
/// </summary>
internal sealed class AssetsFurnishing(IPanelTabHost host, Func<string?, bool> planCarrierRoute, CommanderFurnishing commander)
{
    /// <summary>The pages in the page sidebar's order.</summary>
    public static IReadOnlyList<string> Order { get; } =
    [
        LoadoutPages.FleetRoot,
        StoredModulesPage.RootKey,
        CrewPage.RootKey,
        CharactersPage.RootKey,
        OnFootMode.Root,
        LoadoutPages.CarrierRoot,
        ConstructionPage.RootKey,
        LoadoutPages.GapRoot,
        EngineersPages.DirectoryRoot,
    ];

    /// <summary>The Assets sub-tab's crumb.</summary>
    public static NavCrumb Root { get; } = new(PanelTabNames.AssetsRoot, "Assets")
    {
        Spoken = ["asset management"],
    };

    /// <summary>A page of Assets: its own pane and the levels drilled from it, never the root beside them.</summary>
    private static NavCrumb Page(string key, string word, string help) =>
        new(key, word) { Help = help, Starts = true, Level = PanelTabNames.AssetsRoot };

    private bool _carrierOwned;

    private Func<NavCrumb, Control>? _loadoutBuild;
    private Func<NavCrumb, Control>? _engineerBuild;
    private Func<NavCrumb, Control>? _constructionBuild;
    private ConstructionPage? _construction;
    private EngineerSource? _engineers;
    private string _engineerStamp = string.Empty;
    private Func<D47.Core.Journal.CommanderGameState?>? _engineerState;
    private ShipsMode? _loadoutMode;
    private OnFootMode? _onFootMode;
    private GapSource? _gap;
    private CarrierSource? _carrier;
    private D47.App.Controls.JournalClock? _materialsClock;
    private ComparePage? _comparePage;
    private StoredModulesPage? _storedModulesPage;
    private CrewPage? _crewPage;
    private CharactersPage? _charactersPage;
    private Func<NavCrumb, Control>? _charactersBuild;
    private (object? Materials, object? Suit, object? Colonisation) _planStock;
    private (object?, object?, object?, string?, bool) _inventorySeen;
    private Func<D47.Core.Journal.CommanderGameState?>? _loadoutState;
    private D47.Core.Journal.ShipLoadout? _loadoutSeen;
    private D47.Core.Journal.ModuleStore? _modulesSeen;
    private D47.Core.Journal.FleetRegistry? _fleetSeen;
    private D47.Core.Journal.CarrierState? _carrierSeen;
    private D47.Core.Journal.CarrierState? _squadronSeen;
    private D47.Core.Journal.CargoHold? _holdSeen;
    private string _loadoutEngineerStamp = string.Empty;

    public void EnableLoadout(
        D47.Core.Ships.ShipPlanService ships,
        D47.Core.Checklists.ChecklistService checklists,
        Func<D47.Core.Journal.CommanderGameState?> state,
        D47.Core.Loadout.OnFootPlanService? onFoot = null,
        Func<D47.Core.Journal.ModulePower>? modulePower = null,
        Func<bool>? hullArt = null,
        Func<Control?>? settingsStrip = null,
        Func<Control?>? carrierSettingsStrip = null,
        Func<D47.Core.Knowledge.IGalaxyService?>? galaxy = null,
        D47.Core.Ships.BuildTalk? talk = null,
        Func<D47.Core.Journal.GameStatus>? status = null,
        ConstructionSurface? construction = null,
        CharactersHost? characters = null,
        OutfittingPlotter? outfitting = null)
    {
        var shipsMode = new ShipsMode(ships, checklists, state, modulePower, hullArt, talk, status,
            D47.App.Input.DesktopClipboard.ReadTextAsync,
            D47.App.Input.DesktopClipboard.WriteTextAsync,
            outfitting);

        _loadoutMode = shipsMode;
        _loadoutState = state;
        _loadoutEngineerStamp = D47.Core.Engineers.UnlockPlanner.Stamp(state());

        var modes = new List<ILoadoutMode> { shipsMode };

        if (onFoot is not null)
        {
            var onFootMode = new OnFootMode(onFoot, checklists, state);

            _onFootMode = onFootMode;
            modes.Add(onFootMode);
        }

        // Recomputed on every draw: it is a subtraction over two stores and the live inventory, all three of
        // which move under the page.
        GapSource? gap = null;

        if (onFoot is not null)
        {
            gap = new GapSource(
                () => D47.Core.Loadout.PlanGap.Of(
                    ships.Mine,
                    onFoot.Mine,
                    state(),
                    includeIntended: true,
                    checklists.SlotFor),
                state,
                galaxy);

            // Either store moving changes the subtraction, and neither knows about the other.
            ships.Store.Changed += gap.Invalidate;
            onFoot.Store.Changed += gap.Invalidate;

            _gap = gap;
            _materialsClock = new D47.App.Controls.JournalClock(() => state()?.Session.LastEventAt);
        }

        var pages = new List<NavCrumb>
        {
            Page(LoadoutPages.FleetRoot, "Ships", D47.Core.Capabilities.Builtin.ShipsCapability.Id),
            Page(StoredModulesPage.RootKey, "Stored modules", D47.Core.Capabilities.Builtin.JournalCapability.Id),
            Page(CrewPage.RootKey, "Crew", D47.Core.Capabilities.Builtin.CrewCapability.Id),
        };

        if (characters is not null)
        {
            _charactersBuild = _ => _charactersPage = new CharactersPage(
                characters,
                state,
                host.Prompts,
                host.Portraits,
                host.Model?.CarrierPictureSource,
                host.Headset,
                host.Copy);

            pages.Add(Page(CharactersPage.RootKey, "Characters", D47.Core.Capabilities.Builtin.CharactersCapability.Id));
        }

        if (onFoot is not null)
        {
            pages.Add(Page(OnFootMode.Root, "Suits", D47.Core.Capabilities.Builtin.OnFootCapability.Id));
        }

        var carrier = _carrier = new CarrierSource(
            () => state()?.Carrier ?? D47.Core.Journal.CarrierState.None,
            () => state()?.SquadronCarrier ?? D47.Core.Journal.CarrierState.NoSquadron,
            () => state()?.Hold is { IsShip: true } hold ? hold.Of("tritium") : 0,
            () => state()?.Location.StarSystem);

        // _engineers is read when the first draw opens Materials, not here (#477); Copy and Portraits likewise.
        _loadoutBuild = crumb => LoadoutPages.Build(
            crumb, modes, gap, carrier, host.Nav, host.Prompts, host.Copy, settingsStrip, carrierSettingsStrip, _engineers, _materialsClock,
            () => _comparePage = new ComparePage(state, host.ColumnWidths, host.Copy, host.ColumnArrangements),
            () => _storedModulesPage = new StoredModulesPage(state, host.ColumnWidths, host.Copy, host.ColumnArrangements),
            () => _crewPage = new CrewPage(state, host.Portraits?.Pictures, host.ColumnWidths, host.ColumnArrangements),
            planCarrierRoute,
            host.Portraits?.Pictures is { } pictures && host.Model?.CarrierPictureSource is { } picture
                ? new CarrierPictures(pictures, picture, () => host.Model?.CommanderPictureSource?.Invoke() is not null)
                : null,
            host.CarrierHulls,
            host.ColumnWidths,
            host.Model?.WaitSound,
            host.ColumnArrangements);

        foreach (var page in pages)
        {
            DeclarePage(page);
        }

        _carrierOwned = carrier.Now.Owned;
        DeclarePage(Page(LoadoutPages.CarrierRoot, "Carrier", D47.Core.Capabilities.Builtin.CarrierCapability.Id), () => carrier.Now.Owned);
        host.Nav.Declare(LoadoutPages.FleetRoot, ComparePage.Crumb);

        if (construction is not null)
        {
            EnableConstruction(construction);
        }

        if (onFoot is not null)
        {
            DeclarePage(Page(LoadoutPages.GapRoot, "Materials", D47.Core.Capabilities.Builtin.GapCapability.Id));
        }
    }

    /// <summary>Furnishes the Assets root, once, and declares a page under it, which the page sidebar lists.</summary>
    private void DeclarePage(NavCrumb page, Func<bool>? canOpen = null)
    {
        commander.FurnishAssets(Root, BuildAssets);
        host.Nav.Declare(PanelTabNames.AssetsRoot, page, canOpen);
        host.Nav.HoldPages(PanelTabNames.AssetsRoot, Order);
    }

    /// <summary>Whether Commander is showing this page of Assets, at any depth below it.</summary>
    private bool Showing(string page) =>
        host.Tab == PanelTab.Commander && host.Nav.PageKeyOf(PanelTab.Commander) == page;

    /// <summary>Adds Commander › Assets › Construction; <see cref="EnableLoadout"/> calls it after Carrier.</summary>
    public void EnableConstruction(ConstructionSurface construction)
    {
        var headset = host.Headset;

        _constructionBuild = _ => _construction = new ConstructionPage(construction, host.Copy, headset, host.Browse, host.ColumnWidths, host.Model?.WaitSound, host.ColumnArrangements);

        DeclarePage(Page(ConstructionPage.RootKey, "Construction", D47.Core.Capabilities.Builtin.ColonisationCapability.Id));
    }

    /// <summary>Redraws the Construction page when the journal has moved the sites, the hold or the carrier.</summary>
    public bool TickConstruction() =>
        _construction is { } page
        && Showing(ConstructionPage.RootKey)
        && page.Tick();

    /// <summary>Adds the Engineers page; call after <see cref="EnableLoadout"/> so it lands last.</summary>
    public void EnableEngineers(
        D47.Core.Engineers.EngineerPlanService unlocks,
        D47.Core.Ships.ShipPlanService ships,
        Func<D47.Core.Journal.CommanderGameState?> state,
        D47.Core.Loadout.OnFootPlanService? onFoot = null,
        EngineerDirectoryMemory? memory = null,
        D47.Core.Checklists.ChecklistService? checklists = null)
    {
        var source = new EngineerSource(
            unlocks.Report,
            checklists is null ? null : checklists.IsPinned,
            checklists is null ? null : checklists.Pin,
            unlocks.AddPrerequisites);

        // A plan moving changes who is worth flying to, and neither store knows about this page.
        ships.Store.Changed += source.Invalidate;

        if (onFoot is not null)
        {
            onFoot.Store.Changed += source.Invalidate;
        }

        // A pin set or cleared here is exactly what this page exists to show (#113).
        if (checklists is not null)
        {
            checklists.PinnedChanged += source.Invalidate;
        }

        _engineers = source;
        _engineerStamp = D47.Core.Engineers.UnlockPlanner.Stamp(state());
        _engineerState = state;

        _engineerBuild = crumb => EngineersPages.Build(crumb, source, host.Nav, memory, host.Copy, host.Headset, host.ColumnWidths, host.ColumnArrangements);

        DeclarePage(Page(EngineersPages.DirectoryRoot, "Engineers", D47.Core.Capabilities.Builtin.EngineerCapability.Id));
        host.Nav.Declare(EngineersPages.DirectoryRoot, EngineersPages.RouteCrumb);
    }

    /// <summary>Redraws the engineer pages when the Commander has moved, re-fitted or unlocked somebody.</summary>
    public bool TickEngineers()
    {
        if (_engineers is not { } source || !Showing(EngineersPages.DirectoryRoot))
        {
            return false;
        }

        var stamp = D47.Core.Engineers.UnlockPlanner.Stamp(_engineerState?.Invoke());

        if (string.Equals(stamp, _engineerStamp, StringComparison.Ordinal))
        {
            return false;
        }

        _engineerStamp = stamp;
        source.Invalidate();

        return true;
    }

    /// <summary>Redraws Commander › Assets when the journal says the ship, carrier, stock or engineers moved.</summary>
    public bool TickLoadout()
    {
        var changed = TickConstruction();

        changed |= ShowCarrierPage();

        if (_loadoutMode is not { } mode
            || host.Tab != PanelTab.Commander
            || host.Nav.RootKeyOf(PanelTab.Commander) != PanelTabNames.AssetsRoot)
        {
            return changed;
        }

        // The carrier moves on its own events rather than with the ship, so it is compared separately (#230).
        var carrier = _loadoutState?.Invoke()?.Carrier;
        var squadron = _loadoutState?.Invoke()?.SquadronCarrier;
        var hold = _loadoutState?.Invoke()?.Hold;

        if (!ReferenceEquals(carrier, _carrierSeen)
            || !ReferenceEquals(squadron, _squadronSeen)
            || !ReferenceEquals(hold, _holdSeen))
        {
            _carrierSeen = carrier;
            _squadronSeen = squadron;
            _holdSeen = hold;
            _carrier?.Invalidate();
            changed = true;
        }

        var held = _loadoutState?.Invoke();
        var stock = (held?.Materials, held?.Suit, held?.Colonisation);

        if (!ReferenceEquals(stock.Materials, _planStock.Materials)
            || !ReferenceEquals(stock.Suit, _planStock.Suit)
            || !ReferenceEquals(stock.Colonisation, _planStock.Colonisation))
        {
            _planStock = stock;
            mode.Invalidate();
            changed = true;
        }

        var fleet = held?.Fleet;

        if (!ReferenceEquals(fleet, _fleetSeen))
        {
            _fleetSeen = fleet;
            mode.Invalidate();
            changed = true;
        }

        // Module storage decides what a ship's plan still needs bought.
        var modules = _loadoutState?.Invoke()?.Modules;

        if (!ReferenceEquals(modules, _modulesSeen))
        {
            _modulesSeen = modules;
            mode.Invalidate();
            changed = true;
        }

        var current = _loadoutState?.Invoke()?.Ship;

        if (!ReferenceEquals(current, _loadoutSeen))
        {
            _loadoutSeen = current;
            mode.Invalidate();
            changed = true;
        }

        // Neither store knows about a jump or a rank-up, and the engineers block on a slot page reads both;
        // the Engineers tab compares the same stamp (#195).
        var engineerStamp = D47.Core.Engineers.UnlockPlanner.Stamp(_loadoutState?.Invoke());

        if (!string.Equals(engineerStamp, _loadoutEngineerStamp, StringComparison.Ordinal))
        {
            _loadoutEngineerStamp = engineerStamp;
            mode.Invalidate();
            _onFootMode?.Invalidate();
            _carrier?.Invalidate();
            _gap?.Invalidate();
            changed = true;
        }

        var nav = host.Nav;

        if (_comparePage is { } compare && nav.Trail.Count > 0 && nav.Trail[^1].Key == ComparePage.Key)
        {
            changed |= compare.Tick();
        }

        if (_storedModulesPage is { } stored && Showing(StoredModulesPage.RootKey))
        {
            changed |= stored.Tick();
        }

        if (_crewPage is { } crewPage && Showing(CrewPage.RootKey))
        {
            changed |= crewPage.Tick();
        }

        if (_charactersPage is { } charactersPage && Showing(CharactersPage.RootKey))
        {
            changed |= charactersPage.Tick();
        }

        // The Materials page measures plans against the inventory, which moves without either store saying so.
        if (_gap is { } gap && Showing(LoadoutPages.GapRoot))
        {
            changed |= _materialsClock?.Tick() ?? false;

            // The system and the galaxy setting decide what material detail searches.
            var live = _loadoutState?.Invoke();
            (object?, object?, object?, string?, bool) inventory =
                (live?.Materials, live?.Suit, hold, live?.Location.StarSystem, gap.Galaxy() is not null);

            if (!Equals(inventory, _inventorySeen))
            {
                _inventorySeen = inventory;
                gap.Invalidate();
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Lists the Carrier page only while the Commander owns a carrier, and moves a surface off it when the carrier
    /// goes; true when that changed.
    /// </summary>
    private bool ShowCarrierPage()
    {
        if (_carrier is not { } carrier || carrier.Now.Owned == _carrierOwned)
        {
            return false;
        }

        _carrierOwned = carrier.Now.Owned;
        host.Nav.Refresh();
        return true;
    }

    /// <summary>Redraws an open Ships page after the Hull pictures setting changes (#247).</summary>
    public void InvalidateLoadout() => _loadoutMode?.Invalidate();

    /// <summary>Draws a level of Commander › Assets: the Construction page, the engineer pages, or whatever the loadout pages draw.</summary>

    private Control BuildAssets(NavCrumb crumb) =>
        crumb.Key == ConstructionPage.RootKey && _constructionBuild is { } construction ? construction(crumb)
        : crumb.Key == CharactersPage.RootKey && _charactersBuild is { } characters ? characters(crumb)
        : EngineersPages.Owns(crumb) && _engineerBuild is { } engineers ? engineers(crumb)
        : _loadoutBuild is { } loadout ? loadout(crumb)
        : new TextBlock { Text = "Nothing here." };
}
