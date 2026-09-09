using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>The route being flown, all of it (Phase 37, "Progress").</summary>
public sealed class RouteProgressPage : UserControl
{
    private readonly Func<NavRoute> _route;
    private readonly Func<string?> _here;
    private readonly Action<string>? _copy;

    private readonly TextBlock _headline = new()
    {
        FontSize = TypeScale.Heading,
        FontWeight = FontWeight.SemiBold,
        TextWrapping = TextWrapping.Wrap,
    };

    private readonly TextBlock _totals = new()
    {
        FontSize = TypeScale.Body,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 2, 0, 0),
    };

    private readonly TextBlock _aside = new()
    {
        FontSize = TypeScale.Secondary,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 6, 0, 0),
        IsVisible = false,
    };

    private readonly StackPanel _hops = new() { Spacing = 2 };

    public RouteProgressPage(
        Func<NavRoute> route,
        Func<string?> here,
        Action<string>? copy = null)
    {
        _route = route;
        _here = here;
        _copy = copy;

        Themed(_headline, TextBlock.ForegroundProperty, ThemeManager.AccentKey);
        Themed(_totals, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        Themed(_aside, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var header = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 12),
            Children = { _headline, _totals, _aside },
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
            _headline.Text = "No route plotted.";
            _totals.Text = "Plot one in the galaxy map, or on the Plan page, and it appears here.";
            _aside.IsVisible = false;
            return;
        }

        var first = route.Hops[0];
        var last = route.Hops[^1];

        _headline.Text = $"{first.StarSystem} → {last.StarSystem}";

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
            _hops.Children.Add(Row(route.Hops[index], index, progress));
        }
    }

    /// <summary>One hop.</summary>
    private Control Row(RouteHop hop, int index, RouteProgress progress)
    {
        var behind = !progress.OffRoute && index < progress.Index;
        var current = index == progress.Index;

        var name = new TextBlock
        {
            Text = hop.StarSystem,
            FontSize = TypeScale.Body,
            FontWeight = current ? FontWeight.SemiBold : FontWeight.Normal,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Themed(
            name,
            TextBlock.ForegroundProperty,
            current ? ThemeManager.AccentKey : behind ? ThemeManager.TextMutedKey : ThemeManager.TextKey);

        var detail = new TextBlock
        {
            Text = Detail(hop),
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Themed(detail, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var line = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { name, detail },
        };

        if (hop.Hazardous)
        {
            line.Children.Add(Badge(BadgeWord(hop), ThemeManager.DangerKey));
        }

        // Scoopable is only worth saying where it changes a decision: a star that cannot refuel you, or one
        // d47 cannot vouch for either way. "Yes" on every K, G and B would be a badge on most of the route
        // saying nothing.
        if (hop.Scoopable is false)
        {
            line.Children.Add(Badge("no scoop", ThemeManager.TextMutedKey));
        }
        else if (hop.Scoopable is null)
        {
            // Never drawn as "no".
            line.Children.Add(Badge("scoop unknown", ThemeManager.TextMutedKey));
        }

        var row = new Border
        {
            Padding = new Thickness(8, 5),
            CornerRadius = new CornerRadius(3),
            Child = line,
        };

        if (current)
        {
            Themed(row, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);
        }

        if (_copy is { } copy)
        {
            // Every system name on this page is a copy target (Phase 37, "Course"): the clipboard is the part
            // of plotting that always works, whatever the map is doing.
            row.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            row.Tapped += (_, _) => copy(hop.StarSystem);
            ToolTip.SetTip(row, $"Copy {hop.StarSystem}");
        }

        return row;
    }

    private static string Detail(RouteHop hop) => StarClasses.Speak(hop.StarClass);

    /// <summary>What the hazard means for the Commander, rather than what it is.</summary>
    private static string BadgeWord(RouteHop hop) =>
        StarClasses.IsNeutron(hop.StarClass) ? "supercharge here" : "exclusion zone";

    private static Control Badge(string word, string colourKey)
    {
        var text = new TextBlock
        {
            Text = word,
            FontSize = TypeScale.Small,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Themed(text, TextBlock.ForegroundProperty, colourKey);

        var badge = new Border
        {
            Padding = new Thickness(6, 1),
            CornerRadius = new CornerRadius(2),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = text,
        };

        Themed(badge, Border.BorderBrushProperty, colourKey);

        return badge;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));
}
