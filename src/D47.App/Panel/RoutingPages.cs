using Avalonia.Controls;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// What the Routing tab is made of, and what each of its pages needs (Phase 37).
/// <para>
/// Handed to <see cref="PanelView.Furnish"/> as one builder, the way the Loadout and Engineers
/// tabs are.
/// </para>
/// </summary>
/// <param name="Route">The route Elite wrote, for Progress.</param>
/// <param name="Here">Where the Commander is, for Progress.</param>
/// <param name="Registry">
/// How every button on this tab acts. Nothing here plots, copies or drives the map itself: it
/// invokes the same tools the model calls, through the same registry the keyword router uses.
/// </param>
/// <param name="Plans">The last plan of each kind, written by whoever plotted it.</param>
/// <param name="LookupsEnabled">Whether the galaxy setting is on, for the Plan page's gate.</param>
/// <param name="OpenSettings">A way to the row that turns it on, where the surface has one.</param>
/// <param name="Commodities">
/// The last commodity answer (Phase 49), written by whoever asked — by voice or from the
/// Market page — so both routes show one answer rather than two searches that could disagree.
/// </param>
public sealed record RoutingSurface(
    Func<NavRoute> Route,
    Func<string?> Here,
    CapabilityRegistry? Registry = null,
    RoutePlanBook? Plans = null,
    Func<bool>? LookupsEnabled = null,
    Action? OpenSettings = null,
    CommodityBoard? Commodities = null,

    // The jump range of the ship being flown, for the placeholder that says d47 will supply it
    // (#253). Read fresh, because it changes on a swap and on a refit — and it is the same value
    // RouteCapability falls back to, so the figure drawn is the figure sent.
    Func<double?>? JumpRange = null);

/// <summary>
/// The Routing tab (Phase 37).
/// <para>
/// <b>Three roots, one journey.</b> <em>Plan</em> is where a route comes from, <em>Progress</em>
/// is the one being flown, and <em>Course</em> is getting a system name into the game. They are
/// three readings of the same subject rather than three destinations, which is why they cost one
/// tab and a mode control rather than three tabs — the same collapse Transcript already makes for
/// In Ship, Log File and Journal File.
/// </para>
/// <para>
/// The tab adds no tool, no service and no network call of its own: everything here is a surface
/// for capabilities d47 already had and could previously only speak.
/// </para>
/// </summary>
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

    /// <summary>How a plan that was made is keyed when it is opened as a level.</summary>
    public const string ResultPrefix = "routing.result:";

    /// <summary>
    /// The crumb for a plan that was made.
    /// <para>
    /// <b>Levelled</b>, so opening a second result replaces the first rather than nesting under
    /// it — the same rule the ship and engineer crumbs learned, and for the same reason: a trail
    /// reading <c>Plan › Sol to Colonia › 5 stops from Abraham Lincoln</c> is a route through two
    /// plans at once, which is not a thing that can be true.
    /// </para>
    /// </summary>
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

            // Progress is the fallback rather than Plan, because it is the mode that works with
            // nothing switched on and nothing typed.
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

    private static Control Result(NavCrumb crumb, RoutingSurface surface)
    {
        if (surface.Plans is not { } plans
            || !Enum.TryParse<RoutePlanKind>(crumb.Key[ResultPrefix.Length..], out var kind)
            || plans.Last(kind) is not { } plan)
        {
            // A plan can go away between the button being drawn and being pressed — the file is
            // hand-editable and another plot replaces what was there.
            return Missing("That plan is no longer here. Plot it again.");
        }

        return new RoutePlanResultPage(plan, Copy(surface));
    }

    /// <summary>
    /// Copying a system name, wherever one is drawn on this tab. Through the tool rather than
    /// straight to the clipboard, so the one place that knows how to put a name somewhere useful
    /// stays the one place (Phase 37, "Course").
    /// </summary>
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
