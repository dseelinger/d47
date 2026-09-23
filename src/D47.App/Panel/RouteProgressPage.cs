using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>The route being flown, all of it (Phase 37, "Progress").</summary>
public sealed class RouteProgressPage : UserControl
{
    private readonly Func<NavRoute> _route;
    private readonly Func<string?> _here;
    private readonly Func<string, Task<bool>>? _copy;

    private readonly SelectableTextBlock _headline;

    private readonly TextBlock _totals = RoutingKit.Ink(string.Empty, TypeScale.Body, ThemeManager.AKey, wrap: true);

    private readonly TextBlock _aside = RoutingKit.Prose(string.Empty, TypeScale.Secondary);

    private readonly StackPanel _hops = new() { Spacing = 2 };

    public RouteProgressPage(
        Func<NavRoute> route,
        Func<string?> here,
        Func<string, Task<bool>>? copy = null)
    {
        _route = route;
        _here = here;
        _copy = copy;

        _totals.MaxWidth = double.PositiveInfinity;
        _aside.Margin = new Thickness(0, 6, 0, 0);
        _aside.IsVisible = false;

        (var title, _headline) = RoutingKit.Title(string.Empty);

        var header = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 12),
            Children = { title, _totals, _aside },
        };

        var root = new DockPanel { Margin = new Thickness(14) };

        DockPanel.SetDock(header, Dock.Top);

        root.Children.Add(header);
        root.Children.Add(new ScrollViewer
        {
            Content = _hops,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        });

        Content = root;

        Refresh();
    }

    /// <summary>Redraws from the current route and position.</summary>
    public void Refresh()
    {
        var route = _route();
        var here = _here();
        var progress = RouteProgress.For(route, here);

        _hops.Children.Clear();

        if (!route.IsPlotted)
        {
            TitleText.Show(_headline, "No route plotted.", sentence: true);
            _totals.Text = "Plot one in the galaxy map, or on the Plan page, and it appears here.";
            RoutingKit.Themed(_totals, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            _aside.IsVisible = false;
            return;
        }

        var first = route.Hops[0];
        var last = route.Hops[^1];

        TitleText.Show(_headline, $"{first.StarSystem} → {last.StarSystem}", sentence: true);

        RoutingKit.Themed(_totals, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var jumps = progress.JumpsRemaining == 1 ? "1 jump left" : $"{progress.JumpsRemaining} jumps left";
        var span = route.Hops.Count == 1 ? "1 system" : $"{route.Hops.Count} systems";

        _totals.Text = progress.DistanceRemaining is { } remaining
            ? $"{jumps} · {remaining:N0} ly to go · {span} on the route"
            // No total rather than a short one: a leg of unknown length would otherwise be silently omitted,
            // and the sum would read as a shorter trip.
            : $"{jumps} · {span} on the route";

        if (progress.OffRoute)
        {
            _aside.IsVisible = true;
            _aside.Text = here is { Length: > 0 }
                ? $"You are in {here}, which is not on this route — so all of it is still ahead."
                : "Nothing has said where you are yet, so all of this route is still ahead.";
        }
        else if (progress.JumpsRemaining == 0)
        {
            _aside.IsVisible = true;
            _aside.Text = "You are at the end of this route.";
        }
        else
        {
            _aside.IsVisible = false;
        }

        for (var index = 0; index < route.Hops.Count; index++)
        {
            _hops.Children.Add(Row(route.Hops[index], index, progress, here));
        }
    }

    /// <summary>One hop.</summary>
    private Control Row(RouteHop hop, int index, RouteProgress progress, string? here)
    {
        var behind = !progress.OffRoute && index < progress.Index;
        var tags = new List<Control>();

        if (hop.Hazardous)
        {
            tags.Add(RoutingKit.Tag(BadgeWord(hop), ThemeManager.RedKey));
        }

        // Scoopable is only worth saying where it changes a decision: a star that cannot refuel you, or one
        // d47 cannot vouch for either way.
        if (hop.Scoopable is false)
        {
            tags.Add(RoutingKit.Tag("no scoop", ThemeManager.GreyKey));
        }
        else if (hop.Scoopable is null)
        {
            // Never drawn as "no".
            tags.Add(RoutingKit.Tag("scoop unknown", ThemeManager.GreyKey));
        }

        var key = index == progress.Index || RoutingKit.IsHere(hop.StarSystem, here)
            ? ThemeManager.CyanKey
            : behind ? ThemeManager.GreyKey : ThemeManager.AKey;

        return RoutingKit.Row(
            [RoutingKit.Named(hop.StarSystem, key, [.. tags]), RoutingKit.Values(Detail(hop), behind)],
            hop.StarSystem,
            _copy);
    }

    private static string Detail(RouteHop hop) => StarClasses.Speak(hop.StarClass);

    /// <summary>What the hazard means for the Commander, rather than what it is.</summary>
    private static string BadgeWord(RouteHop hop) =>
        StarClasses.IsNeutron(hop.StarClass) ? "supercharge here" : "exclusion zone";
}
