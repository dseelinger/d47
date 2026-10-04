using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
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
    private readonly Func<string, Task<bool>>? _copy;

    private readonly SelectableTextBlock _headline;

    private readonly TextBlock _toGo = Figure();

    private readonly Control _toGoBlock;

    private readonly TextBlock _totals = RoutingKit.Ink(string.Empty, TypeScale.Body, ThemeManager.GreyKey, wrap: true);

    private readonly TextBlock _aside = RoutingKit.Prose(string.Empty, TypeScale.Secondary);

    private readonly StackPanel _hops = new() { Spacing = 2 };

    private readonly TextBlock _kept = RoutingKit.Tag(string.Empty, ThemeManager.GreyKey);

    public RouteProgressPage(
        Func<NavRoute> route,
        Func<string?> here,
        Func<string, Task<bool>>? copy = null)
    {
        _route = route;
        _here = here;
        _copy = copy;

        _totals.MaxWidth = double.PositiveInfinity;
        _totals.Margin = new Thickness(0, 12, 0, 0);
        _aside.Margin = new Thickness(0, 6, 0, 0);
        _aside.IsVisible = false;

        _headline = TitleText.Style(
            new SelectableTextBlock { TextWrapping = TextWrapping.Wrap },
            TypeScale.Heading,
            TitleRank.Screen);

        _toGoBlock = new StackPanel
        {
            Spacing = 2,
            Children = { FigureLabel("To go"), _toGo },
        };

        var header = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 12),
            Children =
            {
                TitleText.Block(_headline, TitleText.Context("Progress"), _toGoBlock),
                _totals,
                _aside,
            },
        };

        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var say = LoadoutPages.SayLine("how far to go");
        say.Margin = default;

        _kept.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(_kept, Dock.Right);
        footer.Children.Add(_kept);
        footer.Children.Add(say);

        var root = new DockPanel { Margin = new Thickness(14) };

        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);

        root.Children.Add(header);
        root.Children.Add(footer);
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
        ShowKept();

        if (!route.IsPlotted)
        {
            _headline.Inlines = null;
            TitleText.Show(_headline, "No route plotted.", sentence: true);
            _toGoBlock.IsVisible = false;
            _totals.Inlines = null;
            _totals.Text = "Plot one in the galaxy map, or on the Plan page, and it appears here.";
            _aside.IsVisible = false;
            return;
        }

        // From where the Commander is, while they are on the route; from its start, while they are not.
        var from = progress.OffRoute ? route.Hops[0] : route.Hops[Math.Min(progress.Index, route.Hops.Count - 1)];
        var last = route.Hops[^1];

        _headline.Inlines =
        [
            Ink(from.StarSystem.ToUpperInvariant(), progress.OffRoute ? ThemeManager.AKey : ThemeManager.CyanKey),
            Ink(" → ", ThemeManager.GreyKey),
            Ink(last.StarSystem.ToUpperInvariant(), ThemeManager.AKey),
        ];

        _toGoBlock.IsVisible = progress.DistanceRemaining is not null;
        _toGo.Text = progress.DistanceRemaining is { } ahead ? $"{ahead:N0} LY" : string.Empty;

        var totals = new InlineCollection
        {
            Number(progress.JumpsRemaining.ToString("N0")),
            Ink(progress.JumpsRemaining == 1 ? " jump left" : " jumps left", ThemeManager.GreyKey),
        };

        // No total rather than a short one: a leg of unknown length would otherwise be silently omitted,
        // and the sum would read as a shorter trip.
        if (progress.DistanceRemaining is { } remaining)
        {
            totals.Add(Ink(" · ", ThemeManager.GreyKey));
            totals.Add(Number($"{remaining:N0}"));
            totals.Add(Ink(" ly to go", ThemeManager.GreyKey));
        }

        totals.Add(Ink(" · ", ThemeManager.GreyKey));
        totals.Add(Number(route.Hops.Count.ToString("N0")));
        totals.Add(Ink(route.Hops.Count == 1 ? " system on the route" : " systems on the route", ThemeManager.GreyKey));

        _totals.Inlines = totals;

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

    /// <summary>One hop: its number, system, star class and the one tag that changes a decision there.</summary>
    private Control Row(RouteHop hop, int index, RouteProgress progress, string? here)
    {
        var behind = !progress.OffRoute && index < progress.Index;
        var current = (!progress.OffRoute && index == progress.Index) || RoutingKit.IsHere(hop.StarSystem, here);

        var number = RoutingKit.Ink((index + 1).ToString("N0"), TypeScale.Meta, ThemeManager.GreyKey);
        number.FontFamily = new FontFamily(Fonts.MonoFamily);
        number.VerticalAlignment = VerticalAlignment.Center;

        var system = RoutingKit.Ink(
            hop.StarSystem,
            TypeScale.ControlLarge,
            current ? ThemeManager.CyanKey : behind ? ThemeManager.GreyKey : ThemeManager.AKey);
        system.FontFamily = new FontFamily(Fonts.ChromeFamily);
        system.FontWeight = FontWeight.Medium;
        system.TextTrimming = TextTrimming.CharacterEllipsis;
        system.VerticalAlignment = VerticalAlignment.Center;

        var star = RoutingKit.Tag(ClassWord(hop.StarClass), ThemeManager.GreyKey);
        star.FontWeight = FontWeight.Medium;

        var grid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(28, GridUnitType.Pixel),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(110, GridUnitType.Pixel),
                new ColumnDefinition(170, GridUnitType.Pixel),
            ],
            MinHeight = 44,
        };

        Place(grid, number, 0);
        Place(grid, system, 1);
        Place(grid, star, 2);

        if (Decision(hop, current) is { } tag)
        {
            tag.HorizontalAlignment = HorizontalAlignment.Right;
            Place(grid, tag, 3);
        }

        var layout = new DockPanel();

        if (_copy is not null)
        {
            var glyph = D47.App.Controls.CopyGlyph.For(hop.StarSystem, _copy);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            glyph.Margin = new Thickness(10, 0, 0, 0);

            DockPanel.SetDock(glyph, Dock.Right);
            layout.Children.Add(glyph);
        }

        layout.Children.Add(grid);

        var row = ListRow.Dress(new Border { Child = layout });
        row.Padding = new Thickness(14, 0);

        if (_copy is not null)
        {
            row.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            row.Tapped += (_, _) => _ = _copy(hop.StarSystem);
        }

        return row;
    }

    private static void Place(Grid grid, Control control, int column)
    {
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    /// <summary>
    /// Where the Commander is, then a hazard, then a star that cannot refuel them or that d47 cannot vouch
    /// for — never drawn as "no".
    /// </summary>
    private static TextBlock? Decision(RouteHop hop, bool current) =>
        current ? RoutingKit.Tag("here", ThemeManager.CyanKey)
        : hop.Hazardous ? RoutingKit.Tag(
            StarClasses.IsNeutron(hop.StarClass) ? "supercharge here" : "exclusion zone",
            StarClasses.IsNeutron(hop.StarClass) ? ThemeManager.AKey : ThemeManager.RedKey)
        : hop.Scoopable is false ? RoutingKit.Tag("no scoop", ThemeManager.WarnKey)
        : hop.Scoopable is null ? RoutingKit.Tag("scoop unknown", ThemeManager.GreyKey)
        : null;

    /// <summary>The star class as the column shows it: the letter, or what the star is where it has a name.</summary>
    private static string ClassWord(string? starClass) => starClass?.Trim() switch
    {
        null or "" => string.Empty,
        "N" => "neutron",
        "H" or "SupermassiveBlackHole" => "black hole",
        { } dwarf when StarClasses.IsWhiteDwarf(dwarf) => "white dwarf",
        { } other => other,
    };

    private void ShowKept()
    {
        _kept.Inlines =
        [
            Ink("KEPT CURRENT BY THE JOURNAL · ", ThemeManager.GreyKey),
            Number(DateTime.Now.ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture), ThemeManager.CyanKey),
        ];
    }

    private static Run Ink(string text, string key)
    {
        var run = new Run(text);
        RoutingKit.Themed(run, TextElement.ForegroundProperty, key);

        return run;
    }

    private static Run Number(string text, string key = ThemeManager.AKey)
    {
        var run = Ink(text, key);
        run.FontFamily = new FontFamily(Fonts.MonoFamily);

        return run;
    }

    private static TextBlock FigureLabel(string text)
    {
        var label = RoutingKit.Tag(text, ThemeManager.GreyKey);
        label.FontWeight = FontWeight.Medium;
        label.HorizontalAlignment = HorizontalAlignment.Right;

        return label;
    }

    private static TextBlock Figure()
    {
        var figure = RoutingKit.Ink(string.Empty, TypeScale.Figure, ThemeManager.AKey);
        figure.FontFamily = new FontFamily(Fonts.MonoFamily);
        figure.HorizontalAlignment = HorizontalAlignment.Right;

        return figure;
    }
}
