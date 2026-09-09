using Avalonia.Controls;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>What the Routing tab is made of, and what each of its pages needs (Phase 37).</summary>
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

    // The Community Goal page's own needs (#296): the saved search, the ledger, and who and when to ask them
    // about.
    CommunityGoalSurface? CommunityGoal = null);

/// <summary>What the Community Goal page reads and drives (#296).</summary>
/// <param name="Search">The saved query — its commodity is the one field the page edits.</param>
/// <param name="Ledger">What the commodity has made or lost, folded from the journals.</param>
/// <param name="Commander">
/// The Frontier id the ledger is asked about; null asks about the last one seen.
/// </param>
/// <param name="Now">The clock, injected like everywhere else the ledger is measured from.</param>
/// <param name="Week">
/// The Elite week a moment falls in (#332), reading the boundary setting — a function rather than the
/// page calling <see cref="CommodityLedger.Week"/> itself, so the page needs no settings dependency of
/// its own for two numbers it only ever passes straight through.
/// </param>
public sealed record CommunityGoalSurface(
    CommunityGoalSearch Search,
    CommodityLedger Ledger,
    Func<string?> Commander,
    Func<DateTimeOffset> Now,
    Func<DateTimeOffset, LedgerWindow> Week);

/// <summary>The Routing tab (Phase 37).</summary>
public static class RoutingPages
{
    /// <summary>Where a route comes from: the three planners.</summary>
    public const string PlanRoot = "routing.plan";

    /// <summary>The route being flown, read from the file Elite writes locally.</summary>
    public const string ProgressRoot = "routing.progress";

    /// <summary>A system name onto the clipboard, and into the galaxy map.</summary>
    public const string CourseRoot = "routing.course";

    /// <summary>Where to buy a commodity, or where to dump one (Phase 49).</summary>
    public const string MarketRoot = "routing.market";

    /// <summary>The Community Goal supply search and its ledger (#296).</summary>
    public const string CommunityGoalRoot = "routing.communityGoal";

    /// <summary>How a plan that was made is keyed when it is opened as a level.</summary>
    public const string ResultPrefix = "routing.result:";

    /// <summary>The crumb for a plan that was made.</summary>
    public static NavCrumb ResultCrumb(RoutePlanKind kind, string headline) =>
        new($"{ResultPrefix}{kind}", headline) { Level = "routing.result" };

    /// <summary>Draws whichever root or level a crumb names.</summary>
    public static Control Build(NavCrumb crumb, RoutingSurface surface, PanelNavigator nav)
    {
        if (crumb.Key.StartsWith(ResultPrefix, StringComparison.Ordinal))
        {
            return Result(crumb, surface);
        }

        return crumb.Key switch
        {
            PlanRoot => Plan(surface, nav),
            CourseRoot => Course(surface),
            MarketRoot => Market(surface),
            CommunityGoalRoot => CommunityGoal(surface),

            // Progress is the fallback rather than Plan, because it is the mode that works with nothing
            // switched on and nothing typed.
            _ => new RouteProgressPage(surface.Route, surface.Here, Copy(surface)),
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
                surface.JumpRange)
            : Missing("Plotting is not available on this surface.");

    private static Control Course(RoutingSurface surface) =>
        surface.Registry is { } registry
            ? new RouteCoursePage(
                registry,
                () => surface.Route() is { IsPlotted: true } route ? route.Hops[^1].StarSystem : null)
            : Missing("Setting a course is not available on this surface.");

    private static Control Market(RoutingSurface surface) =>
        surface is { Registry: { } registry, Commodities: { } board }
            ? new RouteMarketPage(
                registry,
                board,
                surface.LookupsEnabled ?? (() => false),
                surface.OpenSettings)
            : Missing("Market lookups are not available on this surface.");

    private static Control CommunityGoal(RoutingSurface surface) =>
        surface is { Registry: { } registry, Commodities: { } board, CommunityGoal: { } goal }
            ? new RouteCommunityGoalPage(
                registry,
                board,
                goal,
                surface.LookupsEnabled ?? (() => false),
                surface.OpenSettings,
                Copy(surface))
            : Missing("The Community Goal search is not available on this surface.");

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

        return new RoutePlanResultPage(plan, Copy(surface));
    }

    /// <summary>Copying a system name, wherever one is drawn on this tab.</summary>
    private static Action<string>? Copy(RoutingSurface surface) =>
        surface.Registry is { } registry
            ? system => _ = registry.InvokeAsync(
                "copy_to_clipboard",
                new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["text"] = system,
                }),
                CancellationToken.None)
            : null;

    private static Control Missing(string why) =>
        new TextBlock
        {
            Text = why,
            Margin = new Avalonia.Thickness(14),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        };
}
