using Avalonia.Controls;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>What the Navigation tab is made of, and what each of its pages needs (Phase 37).</summary>
/// <param name="Route">The route Elite wrote, for Progress.</param>
/// <param name="Here">Where the Commander is, for Progress.</param>
/// <param name="Registry">How every button on this tab acts.</param>
/// <param name="Plans">The last plan of each kind, written by whoever plotted it.</param>
/// <param name="LookupsEnabled">
/// Whether the galaxy setting is on, for the Plan page's gate.
/// </param>
/// <param name="OpenSettings">
/// A way to the row that turns it on, where the surface has one.
/// </param>
/// <param name="Commodities">
/// The last commodity answer (Phase 49), written by whoever asked — by voice or from the Market page —
/// so both routes show one answer rather than two searches that could disagree.
/// </param>
/// <param name="Clipboard">Where a system name goes when a Commander copies one from this tab.</param>
public sealed record RoutingSurface(
    Func<NavRoute> Route,
    Func<string?> Here,
    CapabilityRegistry? Registry = null,
    RoutePlanBook? Plans = null,
    Func<bool>? LookupsEnabled = null,
    Action? OpenSettings = null,
    CommodityBoard? Commodities = null,

    // The jump range of the ship being flown, for the placeholder that says d47 will supply it (#253).
    Func<double?>? JumpRange = null,

    D47.Core.Capabilities.Builtin.IClipboard? Clipboard = null,

    // The Trade route page's own saved values, read and written through this (#311).
    SettingsService? Settings = null,

    // The Bookmarks page's own needs (#490): the store, the flying Commander, and the phrases a rename
    // may not take.
    BookmarkStore? Bookmarks = null,
    Func<CommanderGameState?>? Commander = null,
    Func<IReadOnlyCollection<string>>? BookmarkPhrasesTaken = null,

    // On this body (#555): the live position, and what an unmapped planet would pay if mapped.
    Func<GameStatus>? Status = null,
    Func<long, int, long?>? WorthIfMapped = null,

    // Unsold data (#556): the two ledgers, read once history has been set up.
    Func<ExobiologyLedger?>? Exobiology = null,
    Func<CartographyLedger?>? Cartography = null,

    // Fleet › Carrier's "Plan a carrier route", handed to the Carrier Route card (#637).
    CarrierRouteRequest? CarrierRoute = null,

    // Best cargo (#849): the last search, by voice or from the page.
    BestCargoBoard? Cargo = null);

/// <summary>A start system for the Carrier Route card, asked for from Fleet › Carrier and taken once.</summary>
public sealed class CarrierRouteRequest
{
    private readonly Lock _gate = new();
    private string? _from;
    private bool _pending;

    /// <summary>Raised when a start system is asked for.</summary>
    public event Action? Asked;

    public void Ask(string? from)
    {
        lock (_gate)
        {
            _from = from;
            _pending = true;
        }

        Asked?.Invoke();
    }

    /// <summary>The start system asked for, once; false when nothing is waiting.</summary>
    public bool TryTake(out string? from)
    {
        lock (_gate)
        {
            from = _from;

            if (!_pending)
            {
                return false;
            }

            _pending = false;
            _from = null;

            return true;
        }
    }
}

/// <summary>The Navigation tab (Phase 37).</summary>
public static class RoutingPages
{
    /// <summary>Where a route comes from: the three planners.</summary>
    public const string PlanRoot = "routing.plan";

    /// <summary>The route being flown, read from the file Elite writes locally: a level beside Plan.</summary>
    public const string ProgressKey = "routing.progress";

    /// <summary>Where to buy a commodity, or where to dump one (Phase 49).</summary>
    public const string MarketRoot = "routing.market";

    /// <summary>What to buy at the docked station to sell in another system (#849).</summary>
    public const string BestCargoRoot = "routing.bestCargo";

    /// <summary>The Trade route page: the plotter's saved hops, jumps and switches (#311).</summary>
    public const string TradeRoot = "routing.trade";

    /// <summary>The systems a Commander has named, so "set course for" the name returns to them (#490).</summary>
    public const string BookmarksRoot = "routing.bookmarks";

    /// <summary>How a plan that was made is keyed when it is opened as a level.</summary>
    public const string ResultPrefix = "routing.result:";

    /// <summary>The level right of a root, which Progress and a plan result take in turn.</summary>
    public const string BesideLevel = "routing.beside";

    /// <summary>The crumb for a plan that was made.</summary>
    public static NavCrumb ResultCrumb(RoutePlanKind kind, string headline) =>
        new($"{ResultPrefix}{kind}", headline) { Level = BesideLevel };

    /// <summary>The crumb for the route being flown.</summary>
    public static NavCrumb ProgressCrumb { get; } = new(ProgressKey, "Progress")
    {
        Level = BesideLevel,
        Help = D47.Core.Capabilities.Builtin.RouteCapability.Id,
    };

    /// <summary>Draws whichever root or level a crumb names.</summary>
    public static Control Build(
        NavCrumb crumb,
        RoutingSurface surface,
        PanelNavigator nav,
        PanelPrompts prompts)
    {
        if (crumb.Key.StartsWith(ResultPrefix, StringComparison.Ordinal))
        {
            return Result(crumb, surface);
        }

        return crumb.Key switch
        {
            PlanRoot => Plan(surface, nav),
            ProgressKey => new RouteProgressPage(surface.Route, surface.Here, Copy(surface)),
            MarketRoot => Market(surface),
            BestCargoRoot => BestCargo(surface, nav),
            TradeRoot => Trade(surface, nav),
            BookmarksRoot => Bookmarks(surface, prompts),
            _ => Missing("There is no such page on this tab."),
        };
    }

    private static Control Plan(RoutingSurface surface, PanelNavigator nav) =>
        surface is { Registry: { } registry, Plans: { } plans }
            ? new RoutePlanPage(
                registry,
                plans,
                nav,
                surface.LookupsEnabled ?? (() => false),
                surface.OpenSettings,
                surface.Here,
                surface.JumpRange,
                () => surface.Commander?.Invoke()?.Carrier.StarSystem,
                surface.CarrierRoute)
            : Missing("Plotting is not available on this surface.");

    private static Control Market(RoutingSurface surface) =>
        surface is { Registry: { } registry, Commodities: { } board }
            ? new RouteMarketPage(
                registry,
                board,
                surface.LookupsEnabled ?? (() => false),
                surface.OpenSettings,
                Copy(surface))
            : Missing("Market lookups are not available on this surface.");

    private static Control BestCargo(RoutingSurface surface, PanelNavigator nav) =>
        surface is { Registry: { } registry, Cargo: { } board }
            ? new RouteBestCargoPage(
                registry,
                board,
                surface.Commander ?? (() => null),
                surface.Route,
                surface.LookupsEnabled ?? (() => false),
                surface.Settings,
                surface.OpenSettings,
                nav.Roots(PanelTab.Navigation).Any(root => root.Key == TradeRoot)
                    ? () => nav.SelectRoot(PanelTab.Navigation, TradeRoot)
                    : null)
            : Missing("Best cargo is not available on this surface.");

    private static Control Trade(RoutingSurface surface, PanelNavigator nav) =>
        surface is { Registry: { } registry, Plans: { } plans, Settings: { } settings }
            ? new RouteTradePage(
                registry,
                plans,
                nav,
                surface.LookupsEnabled ?? (() => false),
                settings,
                surface.OpenSettings)
            : Missing("The Trade route page is not available on this surface.");

    private static Control Bookmarks(RoutingSurface surface, PanelPrompts prompts) =>
        surface is { Bookmarks: { } store, Commander: { } commander }
            ? new BookmarksPage(store, commander, surface.BookmarkPhrasesTaken ?? (() => []), prompts, Copy(surface), surface.Registry)
            : Missing("Bookmarks are not available on this surface.");

    private static Control Result(NavCrumb crumb, RoutingSurface surface)
    {
        if (surface.Plans is not { } plans
            || !Enum.TryParse<RoutePlanKind>(crumb.Key[ResultPrefix.Length..], out var kind)
            || plans.Last(kind) is not { } plan)
        {
            // A plan can go away between the button being drawn and being pressed — the file is hand-editable
            // and another plot replaces what was there.
            return Missing("That plan is no longer here. Plot it again.");
        }

        return new RoutePlanResultPage(plan, Copy(surface), plans, surface.Here);
    }

    /// <summary>Copying a system name, wherever one is drawn on this tab.</summary>
    private static Func<string, Task<bool>>? Copy(RoutingSurface surface) =>
        surface.Clipboard is { } clipboard ? text => clipboard.SetTextAsync(text) : null;

    private static Control Missing(string why)
    {
        var text = RoutingKit.Prose(why, D47.App.Theming.TypeScale.Body);
        text.Margin = new Avalonia.Thickness(14);

        return text;
    }
}
