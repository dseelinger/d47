using Avalonia.Controls;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The Asset Mgmt tab: Ships, Stored modules, Crew, Suits, Carrier, Construction, Materials and Engineers.</summary>
internal sealed class AssetsFurnishing(IPanelTabHost host, Func<string?, bool> planCarrierRoute)
{
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
    private (object? Materials, object? Suit, object? Colonisation) _planStock;
    private (object?, object?, object?, string?, bool) _inventorySeen;
    private Func<D47.Core.Journal.CommanderGameState?>? _loadoutState;
    private D47.Core.Journal.ShipLoadout? _loadoutSeen;
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
        CrewSeatsHost? crewSeats = null,
        D47.Core.Ships.BuildTalk? talk = null,
        Func<D47.Core.Journal.GameStatus>? status = null,
        ConstructionSurface? construction = null)
    {
        var shipsMode = new ShipsMode(ships, checklists, state, modulePower, hullArt, talk, status,
            D47.App.Input.DesktopClipboard.ReadTextAsync,
            D47.App.Input.DesktopClipboard.WriteTextAsync);

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

        // Per root, because this tab's roots are three different subjects.
        var roots = new List<NavCrumb>
        {
            new(LoadoutPages.FleetRoot, "Ships")
            {
                Help = D47.Core.Capabilities.Builtin.ShipsCapability.Id,
            },
            new(StoredModulesPage.RootKey, "Stored modules")
            {
                Help = D47.Core.Capabilities.Builtin.JournalCapability.Id,
            },
            new(CrewPage.RootKey, "Crew")
            {
                Help = D47.Core.Capabilities.Builtin.CrewCapability.Id,
            },
        };

        if (onFoot is not null)
        {
            roots.Add(new NavCrumb(OnFootMode.Root, "Suits")
            {
                Help = D47.Core.Capabilities.Builtin.OnFootCapability.Id,
            });
        }

        _carrier = new CarrierSource(
            () => state()?.Carrier ?? D47.Core.Journal.CarrierState.None,
            () => state()?.SquadronCarrier ?? D47.Core.Journal.CarrierState.NoSquadron,
            () => state()?.Hold is { IsShip: true } hold ? hold.Of("tritium") : 0,
            () => state()?.Location.StarSystem);

        // No capability page covers the carrier, so the root declares no help.
        roots.Add(new NavCrumb(LoadoutPages.CarrierRoot, "Carrier"));

        // _engineers is read when the first draw opens Materials, not here (#477); Copy and Portraits likewise.
        _loadoutBuild = crumb => LoadoutPages.Build(
            crumb, modes, gap, _carrier, host.Nav, host.Prompts, host.Copy, settingsStrip, carrierSettingsStrip, _engineers, _materialsClock,
            () => _comparePage = new ComparePage(state),
            () => _storedModulesPage = new StoredModulesPage(state),
            () => _crewPage = new CrewPage(state, crewSeats, host.Portraits?.Pictures),
            planCarrierRoute,
            host.Portraits?.Pictures is { } pictures && host.Model?.CarrierPictureSource is { } picture
                ? new CarrierPictures(pictures, picture, () => host.Model?.CommanderPictureSource?.Invoke() is not null)
                : null);

        host.Furnish(PanelTab.Assets, BuildAssets, [.. roots]);
        host.Nav.Declare(LoadoutPages.FleetRoot, ComparePage.Crumb);
        ShowCarrierRoot();

        if (construction is not null)
        {
            EnableConstruction(construction);
        }

        if (onFoot is not null)
        {
            host.Furnish(
                PanelTab.Assets,
                BuildAssets,
                new NavCrumb(LoadoutPages.GapRoot, "Materials")
                {
                    Help = D47.Core.Capabilities.Builtin.GapCapability.Id,
                });
        }
    }

    /// <summary>Adds Asset Mgmt › Construction; <see cref="EnableLoadout"/> calls it after Carrier.</summary>
    public void EnableConstruction(ConstructionSurface construction)
    {
        var headset = host.Headset;

        _constructionBuild = _ => _construction = new ConstructionPage(construction, host.Copy, headset, host.Browse);

        host.Furnish(
            PanelTab.Assets,
            BuildAssets,
            new NavCrumb(ConstructionPage.RootKey, "Construction")
            {
                Help = D47.Core.Capabilities.Builtin.ColonisationCapability.Id,
            });
    }

    /// <summary>Redraws the Construction page when the journal has moved the sites, the hold or the carrier.</summary>
    public bool TickConstruction() =>
        _construction is { } page
        && host.Tab == PanelTab.Assets
        && host.Nav.RootKeyOf(PanelTab.Assets) == ConstructionPage.RootKey
        && page.Tick();

    /// <summary>Adds the Engineers root; call after <see cref="EnableLoadout"/> so it lands last.</summary>
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

        _engineerBuild = crumb => EngineersPages.Build(crumb, source, host.Nav, memory, host.Copy);

        host.Furnish(
            PanelTab.Assets,
            BuildAssets,
            new NavCrumb(EngineersPages.DirectoryRoot, "Engineers")
            {
                Help = D47.Core.Capabilities.Builtin.EngineerCapability.Id,
            });
        host.Nav.Declare(EngineersPages.DirectoryRoot, EngineersPages.RouteCrumb);
    }

    /// <summary>Redraws the engineer pages when the Commander has moved, re-fitted or unlocked somebody.</summary>
    public bool TickEngineers()
    {
        if (_engineers is not { } source
            || host.Tab != PanelTab.Assets
            || host.Nav.RootKeyOf(PanelTab.Assets) != EngineersPages.DirectoryRoot)
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

    /// <summary>Redraws the Assets tab when the journal says the ship, carrier, stock or engineers moved.</summary>
    public bool TickLoadout()
    {
        var changed = TickConstruction();

        changed |= ShowCarrierRoot();

        if (_loadoutMode is not { } mode || host.Tab != PanelTab.Assets)
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

        if (_storedModulesPage is { } stored && nav.RootKeyOf(PanelTab.Assets) == StoredModulesPage.RootKey)
        {
            changed |= stored.Tick();
        }

        if (_crewPage is { } crewPage && nav.RootKeyOf(PanelTab.Assets) == CrewPage.RootKey)
        {
            changed |= crewPage.Tick();
        }

        // The Materials page measures plans against the inventory, which moves without either store saying so.
        if (_gap is { } gap && nav.RootKeyOf(PanelTab.Assets) == LoadoutPages.GapRoot)
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

    /// <summary>Shows the Carrier root only while the Commander owns a carrier.</summary>
    private bool ShowCarrierRoot() =>
        _carrier is { } carrier && host.Nav.SetVisible(PanelTab.Assets, LoadoutPages.CarrierRoot, carrier.Now.Owned);

    /// <summary>Redraws an open Ships page after the Hull pictures setting changes (#247).</summary>
    public void InvalidateLoadout() => _loadoutMode?.Invalidate();

    /// <summary>Draws an Asset Mgmt level: the Construction page, the engineer pages, or whatever the loadout roots draw.</summary>
    private Control BuildAssets(NavCrumb crumb) =>
        crumb.Key == ConstructionPage.RootKey && _constructionBuild is { } construction ? construction(crumb)
        : EngineersPages.Owns(crumb) && _engineerBuild is { } engineers ? engineers(crumb)
        : _loadoutBuild is { } loadout ? loadout(crumb)
        : new TextBlock { Text = "Nothing here." };
}
