using Avalonia.Controls;
using D47.Core.Interface;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>
/// The Routing tab (list.md Phase 37).
/// <para>
/// <b>Three roots, one journey.</b> <em>Plan</em> is where a route comes from, <em>Progress</em>
/// is the one being flown, and <em>Course</em> is getting a system name into the game. They are
/// three readings of the same subject rather than three destinations, which is why they cost one
/// tab and a mode control rather than three tabs — the same collapse Transcript already makes for
/// Conversation, Technical and the log file.
/// </para>
/// <para>
/// The tab adds no tool, no service and no network call: everything here is a surface for
/// capabilities d47 already has and could previously only speak.
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

    /// <summary>
    /// Draws whichever root a crumb names. Handed to <see cref="PanelView.Furnish"/>.
    /// <para>
    /// Progress is the fallback rather than Plan, because it is the mode that works with nothing
    /// switched on and nothing typed.
    /// </para>
    /// </summary>
    public static Control Build(
        NavCrumb crumb,
        Func<NavRoute> route,
        Func<string?> here,
        Action<RouteProgressPage> keep)
    {
        _ = crumb;

        var page = new RouteProgressPage(route, here);

        keep(page);

        return page;
    }
}
