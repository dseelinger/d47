using Avalonia.Controls;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The Navigation tab: Plan, Bookmarks, Market, Best cargo, Trade route, On this body and Unsold data.</summary>
internal sealed class NavigationFurnishing(IPanelTabHost host, CarrierRouteRequest carrierRoute)
{
    private RouteProgressPage? _routeProgress;
    private BodyPage? _bodyPage;
    private UnsoldPage? _unsoldPage;
    private RoutePlanPage? _routePlan;
    private RouteTradePage? _routeTrade;
    private RoutePlanResultPage? _routeResult;
    private RouteBestCargoPage? _bestCargo;
    
    private Func<D47.Core.Journal.NavRoute>? _routeState;
    private Func<string?>? _routeHere;
    private Func<string, string?>? _routeOffDuty;
    private Func<double?>? _routeRange;
    private D47.Core.Journal.NavRoute? _routeSeen;
    private string? _routeWhere;
    private string? _routeOffDutySeen;
    private double? _routeRangeSeen;

    /// <summary>Mini's reading of the Plan root; null until a host furnishes a plan book.</summary>
    public RouteMini? Mini { get; private set; }

    /// <summary>
    /// Gives this surface the Navigation tab (Phase 37): where the Commander is going. Progress is a level
    /// beside Plan rather than a root, and needs <paramref name="plan"/>.
    /// </summary>
    public void EnableRouting(
        RoutingSurface surface,
        bool plan = true,
        bool progress = true,
        bool market = true,
        bool bestCargo = true,
        bool trade = true,
        bool bookmarks = true,
        bool body = true,
        bool unsold = true)
    {
        var roots = new List<NavCrumb>();

        surface = surface with { CarrierRoute = carrierRoute };

        if (plan)
        {
            roots.Add(new NavCrumb(RoutingPages.PlanRoot, "Plan")
            {
                Help = D47.Core.Capabilities.Builtin.RouteCapability.Id,
            });
        }

        // After Plan, because a bookmark is a place to plot to (#490).
        if (bookmarks && surface.Bookmarks is not null)
        {
            roots.Add(new NavCrumb(RoutingPages.BookmarksRoot, "Bookmarks")
            {
                Help = D47.Core.Capabilities.Builtin.BookmarksCapability.Id,
            });
        }

        // Last of the four, because it is the newest and because the three before it are the journey and this
        // is the errand (Phase 49).
        if (market && surface.Commodities is not null)
        {
            roots.Add(new NavCrumb(RoutingPages.MarketRoot, "Market")
            {
                Help = D47.Core.Capabilities.Builtin.GalaxyCapability.Id,
            });
        }

        // After Market, because it reads the market the Commander is docked at (#849).
        if (bestCargo && surface is { Cargo: not null, Registry: not null })
        {
            roots.Add(new NavCrumb(RoutingPages.BestCargoRoot, "Best cargo")
            {
                Help = D47.Core.Capabilities.Builtin.RouteCapability.Id,
            });
        }

        // After Market, because it is the newest (#311): its own saved hops, jumps and
        // switches, split out of the Plan page's Trade run card.
        if (trade && surface.Plans is not null && surface.Settings is not null)
        {
            roots.Add(new NavCrumb(RoutingPages.TradeRoot, "Trade route")
            {
                Help = D47.Core.Capabilities.Builtin.RouteCapability.Id,
            });
        }

        // After the existing roots (#555).
        if (body && surface.Commander is not null)
        {
            roots.Add(new NavCrumb(BodyPage.RootKey, "On this body")
            {
                Help = D47.Core.Capabilities.Builtin.ExobiologyCapability.Id,
            });
        }

        // After On this body (#556).
        if (unsold && surface.Commander is not null && (surface.Exobiology is not null || surface.Cartography is not null))
        {
            roots.Add(new NavCrumb(UnsoldPage.RootKey, "Unsold data")
            {
                Help = D47.Core.Capabilities.Builtin.ExobiologyCapability.Id,
            });
        }

        if (roots.Count == 0)
        {
            return;
        }

        _routeState = surface.Route;
        _routeHere = surface.Here;
        _routeOffDuty = surface.OffDuty;
        _routeRange = surface.JumpRange;

        // Mini's own reading of the Plan root, wherever there is a book to read it from (#197).
        if (surface.Plans is { } miniPlans)
        {
            Mini = new RouteMini(miniPlans, surface.Route, surface.Here, surface.OffDuty);
        }

        host.Furnish(
            PanelTab.Navigation,
            crumb =>
            {
                if (crumb.Key == BodyPage.RootKey && surface.Commander is { } commander)
                {
                    return _bodyPage = new BodyPage(commander, surface.Status, surface.WorthIfMapped, surface.OffDuty);
                }

                if (crumb.Key == UnsoldPage.RootKey && surface.Commander is { } carrying)
                {
                    return _unsoldPage = new UnsoldPage(
                        carrying,
                        surface.Exobiology ?? (() => null),
                        surface.Cartography ?? (() => null),
                        surface.Registry);
                }

                var page = RoutingPages.Build(crumb, surface, host.Nav, host.Prompts);

                // Held onto so the tick can redraw Progress and a plot made elsewhere can redraw Plan.
                _routeProgress = page as RouteProgressPage ?? _routeProgress;
                _routePlan = page as RoutePlanPage ?? _routePlan;
                _routeTrade = page as RouteTradePage ?? _routeTrade;
                _routeResult = page as RoutePlanResultPage ?? _routeResult;
                _bestCargo = page as RouteBestCargoPage ?? _bestCargo;

                return page;
            },
            [.. roots]);

        // Each launch opens Plan with Progress beside it; once the Commander leaves it, Plan stays alone.
        if (plan
            && progress
            && host.Nav.RootKeyOf(PanelTab.Navigation) == RoutingPages.PlanRoot
            && host.Nav.TrailOf(PanelTab.Navigation).Count == 1)
        {
            host.Nav.Drill(PanelTab.Navigation, RoutingPages.ProgressCrumb);
        }

        // A plot made anywhere - this tab's own button, or a spoken tool call - leaves the Plan page one
        // redraw out of date, because "Show most recent" is drawn from the book. Mini reads the same
        // book, so it moves with the same event.
        if (surface.Plans is { } plans)
        {
            plans.Changed += () =>
            {
                _routePlan?.Refresh();
                _routeTrade?.Refresh();
                Mini?.Refresh();
                _routeResult?.Refresh();
            };
        }
    }

    /// <summary>
    /// Redraws the route being flown, from the host's tick, and says whether anything on it moved — the
    /// headset serves a frame for a true and holds the last one for a false (#52).
    /// </summary>
    public bool Tick()
    {
        if (host.Tab != PanelTab.Navigation)
        {
            return false;
        }

        // On this body follows the game's position, so it ticks ahead of the route's guard.
        var body = _bodyPage is { } bodyPage && host.Nav.RootKeyOf(PanelTab.Navigation) == BodyPage.RootKey && bodyPage.Tick();
        body |= _unsoldPage is { } unsoldPage && host.Nav.RootKeyOf(PanelTab.Navigation) == UnsoldPage.RootKey && unsoldPage.Tick();
        body |= _bestCargo is { } cargo && host.Nav.RootKeyOf(PanelTab.Navigation) == RoutingPages.BestCargoRoot && cargo.Tick();

        var route = _routeState?.Invoke();
        var here = _routeHere?.Invoke();
        var offDuty = _routeOffDuty?.Invoke(OffDutyNote.Route);

        // And the ship's range, which the plan forms quote (#253).
        var range = _routeRange?.Invoke();

        if (ReferenceEquals(route, _routeSeen)
            && string.Equals(here, _routeWhere, StringComparison.Ordinal)
            && string.Equals(offDuty, _routeOffDutySeen, StringComparison.Ordinal)
            && Nullable.Equals(range, _routeRangeSeen))
        {
            return body;
        }

        _routeSeen = route;
        _routeWhere = here;
        _routeOffDutySeen = offDuty;
        _routeRangeSeen = range;

        // The progress page only where a host furnished one — the plan forms are furnished separately, and a
        // surface with one and not the other used to return before either.
        _routeProgress?.Refresh();

        // RefreshSupplied rather than Refresh: this fires on every jump, and rebuilding the page then would
        // throw away a half-typed destination.
        _routePlan?.RefreshSupplied();

        // The mark on mini's waypoint list is read from the same route and position (#197).
        Mini?.Refresh();

        // The plan's reached stop moves on the same arrival that moves the route and position, so it rides
        // this same guard (#200).
        _routeResult?.Refresh();

        return true;
    }

}
