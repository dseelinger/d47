using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// The Navigation tab's Galaxy map (#1406): where the Commander has been, where they are and the route Elite
/// plotted, over Elite's regions. Every control is a button or a checkbox, so each one is pressed by a tap on the big panel.
/// </summary>
public sealed class GalaxyMapPage : UserControl
{
    public const double WindowMapHeight = 560;
    public const double PanelMapHeight = 376;
    public const double SwitchRail = 152;
    public const double PadRail = 136;

    public const string Reading = "Reading your earlier journals. The jumps from this session are drawn now.";
    public const string NoTravels = "No travels to draw yet. Your jumps show here as you make them.";
    public const string StopsHidden = "Zoom to 6,400 ly to see the route's stops.";
    public const string NotVisited = "not visited";
    public const string YouAreHere = "You are here";
    public const string CentreHere = "Centre here";
    public const string Bookmark = "Bookmark";
    public const string ClearSelection = "Clear selection";

    private const int Galaxy = 0;
    private const int Here = 1;
    private const int RouteFraming = 2;

    private const int ThirtyDays = 0;
    private const int OneYear = 1;
    private const int All = 2;

    private static readonly double Closest = 100;
    private static readonly double Widest = 102_400;

    private static readonly RouteStar[] KeyOrder = [RouteStar.Neutron, RouteStar.WhiteDwarf, RouteStar.BlackHole, RouteStar.NoFuel];

    private readonly Func<TravelLog?> _travel;
    private readonly Func<CommanderGameState?> _commander;
    private readonly Func<NavRoute> _route;
    private readonly Func<string?> _here;
    private readonly Func<string, string?>? _offDuty;
    private readonly Func<DateTimeOffset> _now;
    private readonly bool _headset;
    private readonly BookmarkStore? _bookmarks;
    private readonly Func<string, Task<bool>>? _copy;

    private readonly GalaxyMapView _map = new();
    private readonly Segment _framing = new() { ItemsSource = ["Galaxy", "Here", "Route"] };
    private readonly Segment _range = new() { ItemsSource = ["30 days", "1 year", "All"], SelectedIndex = All };
    private readonly CheckBox _travels = Switch("Travels");
    private readonly CheckBox _routeShown = Switch("Route");
    private readonly CheckBox _regions = Switch("Regions");
    private readonly Button _zoomIn = Tile("+", "Zoom in", 20);
    private readonly Button _zoomOut = Tile("−", "Zoom out", 20);
    private readonly TextBlock _system = Chrome(TypeScale.ControlLarge, ThemeManager.CyanKey);
    private readonly TextBlock _dot = Chrome(TypeScale.ControlLarge, ThemeManager.Grey2Key);
    private readonly TextBlock _region = RoutingKit.Tag(string.Empty, ThemeManager.GreyKey);
    private readonly TextBlock _note = RoutingKit.Prose(string.Empty);
    private readonly TextBlock _counts = RoutingKit.Prose(string.Empty);
    private readonly TextBlock _stripName = Chrome(14, ThemeManager.AKey);
    private readonly TextBlock _stripFacts = RoutingKit.Ink(string.Empty, 13, ThemeManager.GreyKey);
    private readonly TextBlock _stripResult = RoutingKit.Ink(string.Empty, 13, ThemeManager.GreyKey);
    private readonly ContentControl _copyHolder = new();
    private readonly Button _centre = Press(CentreHere, 120);
    private readonly Button _bookmark = Press(Bookmark, 112);
    private readonly Button _clear = Press("✕", TypeScale.MinimumTarget);
    private readonly Border _strip = new() { IsVisible = false, Height = TypeScale.MinimumTarget };
    private Control[] _replaced = [];
    private Control? _mark;
    private bool _stripCurrent;
    private string? _current;
    private readonly WrapPanel _legend = new() { Orientation = Orientation.Horizontal, ItemSpacing = 14, LineSpacing = 4 };

    private IReadOnlyList<StarPosition> _routePoints = [];
    private StarPosition? _position;
    private object? _seen;

    public GalaxyMapPage(
        Func<TravelLog?> travel,
        Func<CommanderGameState?> commander,
        Func<NavRoute> route,
        Func<string?> here,
        Func<string, string?>? offDuty,
        Func<DateTimeOffset> now,
        bool headset,
        BookmarkStore? bookmarks = null,
        Func<string, Task<bool>>? copy = null)
    {
        _travel = travel;
        _commander = commander;
        _route = route;
        _here = here;
        _offDuty = offDuty;
        _now = now;
        _headset = headset;
        _bookmarks = bookmarks;
        _copy = copy;

        BuildStrip();

        _counts.MaxWidth = double.PositiveInfinity;
        _note.MaxWidth = double.PositiveInfinity;
        _note.TextWrapping = TextWrapping.NoWrap;
        _note.TextTrimming = TextTrimming.CharacterEllipsis;
        _dot.Text = "·";
        _region.FontWeight = FontWeight.Medium;

        Content = _headset ? PanelLayout() : WindowLayout();

        _framing.SelectionChanged += (_, _) => Frame(_framing.SelectedIndex);
        _range.SelectionChanged += (_, _) => Refresh();
        _travels.IsCheckedChanged += (_, _) => Refresh();
        _routeShown.IsCheckedChanged += (_, _) => Refresh();
        _regions.IsCheckedChanged += (_, _) => Refresh();
        _zoomIn.Click += (_, _) => Step(view => view.ZoomIn());
        _zoomOut.Click += (_, _) => Step(view => view.ZoomOut());
        _map.ViewportChanged += _ => Moved();
        _map.Picked += Strip;
        _clear.Click += (_, _) => _map.Clear();
        _centre.Click += (_, _) => CentreOnSelected();
        _bookmark.Click += (_, _) => BookmarkSelected();
        _map.Redrawn += Legend;

        Refresh();

        _framing.SelectedIndex = _position is null ? Galaxy : Here;
        Frame(_framing.SelectedIndex);
    }

    internal GalaxyMapView Map => _map;

    internal Segment Framing => _framing;

    internal Segment Range => _range;

    /// <summary>The words under the map where the counts go.</summary>
    internal string CountLine => _counts.Inlines is { Count: > 0 } inlines
        ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
        : _counts.Text ?? string.Empty;

    internal Control StripControl => _strip;

    internal bool StripShown => _strip.IsVisible;

    internal string StripName => _stripName.Text ?? string.Empty;

    internal string StripFacts => string.Concat((_stripFacts.Inlines ?? []).OfType<Run>().Select(run => run.Text));

    internal string StripReply => _stripResult.IsVisible ? _stripResult.Text ?? string.Empty : string.Empty;

    internal string NoteLine => _note.IsVisible ? _note.Text ?? string.Empty : string.Empty;

    /// <summary>Redraws when the Commander, their travels, their position or the route has moved; true when it did.</summary>
    public bool Tick()
    {
        if (Equals(Key(_travel(), _commander(), _offDuty?.Invoke(OffDutyNote.Position)), _seen))
        {
            return false;
        }

        Refresh();

        return true;
    }

    /// <summary>
    /// What a redraw depends on: the Commander, the route, where they are and the backfill; and their step count once
    /// the backfill has finished, so the files it folds do not each redraw the map.
    /// </summary>
    private object Key(TravelLog? log, CommanderGameState? commander, string? offDuty)
    {
        var fid = commander?.Identity.FrontierId ?? string.Empty;
        var folded = log?.HistoryFolded ?? true;

        return (fid, folded ? log?.Count(fid) ?? 0 : -1, folded, _route(), _here(), commander?.Location.StarPos, offDuty);
    }

    /// <summary>Rebuilds what the map draws and the lines under it, keeping the viewport.</summary>
    public void Refresh()
    {
        var log = _travel();
        var commander = _commander();
        var fid = commander?.Identity.FrontierId ?? string.Empty;
        var offDuty = _offDuty?.Invoke(OffDutyNote.Position);

        _seen = Key(log, commander, offDuty);

        var steps = log is null || fid.Length == 0 ? [] : log.Steps(fid);
        var travels = TravelPicture.Of(steps, Since());
        var here = offDuty is null ? _here() : null;
        var route = offDuty is null ? _route() : NavRoute.None;
        var stops = route.IsPlotted ? RouteMapStop.From(route) : [];

        _current = here;
        _position = offDuty is null ? commander?.Location.StarPos : null;
        _routePoints = [.. stops.Where(stop => stop.Position is not null).Select(stop => stop.Position!.Value)];

        var hasRoute = _routePoints.Count >= 2;

        _framing.SetOptionEnabled(Here, _position is not null);
        _framing.SetOptionEnabled(RouteFraming, hasRoute);

        if ((_framing.SelectedIndex == Here && _position is null) || (_framing.SelectedIndex == RouteFraming && !hasRoute))
        {
            _framing.SelectedIndex = -1;
        }

        _map.Show(new GalaxyMapScene(
            _regions.IsChecked == true,
            _travels.IsChecked == true ? travels : null,
            _routeShown.IsChecked == true && hasRoute ? stops : null,
            RouteProgress.For(route, here).Index,
            _position,
            here,
            Named(steps, Since())));

        Strip(_map.Selected);

        Where(offDuty, here);
        Counts(log, steps.Count, travels);
        Zoom();
    }

    private static IReadOnlyList<GalaxyMapStar> Named(IReadOnlyList<TravelStep> steps, DateTimeOffset? since)
    {
        var systems = new Dictionary<long, GalaxyMapStar>();

        foreach (var step in steps.Where(step => since is not { } from || step.At >= from))
        {
            systems[step.SystemAddress] = new GalaxyMapStar(step.System, step.Position);
        }

        return [.. systems.Values];
    }

    private DateTimeOffset? Since() => _range.SelectedIndex switch
    {
        ThirtyDays => _now().AddDays(-30),
        OneYear => _now().AddYears(-1),
        _ => null,
    };

    private void Frame(int framing)
    {
        GalaxyViewport? to = framing switch
        {
            Galaxy => GalaxyViewport.WholeGalaxy,
            Here when _position is { } at => GalaxyViewport.Around(at),
            RouteFraming when _routePoints.Count >= 2 => GalaxyViewport.Fitting(_routePoints),
            _ => null,
        };

        if (to is null)
        {
            return;
        }

        _map.View(to);
        Zoom();
    }

    /// <summary>A zoom or pan press; one that would not move the view does nothing.</summary>
    private void Step(Func<GalaxyViewport, GalaxyViewport> move)
    {
        var to = move(_map.Viewport);

        if (to == _map.Viewport)
        {
            return;
        }

        _map.View(to);
        Moved();
    }

    private void BuildStrip()
    {
        AutomationProperties.SetName(_clear, ClearSelection);
        _clear.FontSize = TypeScale.ControlLarge;
        _stripFacts.TextTrimming = _stripResult.TextTrimming = TextTrimming.CharacterEllipsis;
        _stripResult.IsVisible = false;
        _bookmark.IsVisible = _bookmarks is not null;

        var mark = _mark = new Glyph((context, ink) =>
        {
            var pen = new Pen(ink(ThemeManager.WhiteKey), 1.5, lineCap: PenLineCap.Square);

            foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                context.DrawLine(pen, new Point(11 + (sx * 10), 11 + (sy * 5)), new Point(11 + (sx * 10), 11 + (sy * 10)));
                context.DrawLine(pen, new Point(11 + (sx * 10), 11 + (sy * 10)), new Point(11 + (sx * 5), 11 + (sy * 10)));
            }

            var side = _stripCurrent ? 6 : 3;
            context.DrawRectangle(ink(_stripCurrent ? ThemeManager.CyanKey : ThemeManager.AKey), null, new Rect(11 - (side / 2.0), 11 - (side / 2.0), side, side));
        })
        { Width = 22, Height = 22, VerticalAlignment = VerticalAlignment.Center };

        _stripName.VerticalAlignment = VerticalAlignment.Center;
        _stripFacts.VerticalAlignment = VerticalAlignment.Center;

        var one = _headset
            ? new StackPanel { Children = { _stripName, _stripFacts } }
            : new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Height = 20, Children = { _stripName, _stripFacts } };
        var words = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Children = { one, _stripResult } };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children = { _copyHolder, _centre, _bookmark, _clear },
        };

        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(actions, Dock.Right);
        DockPanel.SetDock(mark, Dock.Left);
        row.Children.Add(actions);
        row.Children.Add(mark);
        row.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 0), Children = { words } });

        _strip.Padding = new Thickness(12, 0, 0, 0);
        _strip.ClipToBounds = true;
        _strip.Child = row;
        RoutingKit.Themed(_strip, Border.BackgroundProperty, ThemeManager.SlabKey);
    }

    /// <summary>Shows what the selected system is and what can be done with it, and hides what the strip replaces.</summary>
    private void Strip(GalaxyMapStar? star)
    {
        _stripResult.IsVisible = false;
        _stripFacts.IsVisible = true;

        foreach (var covered in _replaced)
        {
            covered.Opacity = star is null ? 1 : 0;
        }

        if (star is null)
        {
            _strip.IsVisible = false;
            return;
        }

        _stripCurrent = _current is not null && string.Equals(star.Name, _current, StringComparison.OrdinalIgnoreCase);
        _strip.IsVisible = true;
        _stripName.Text = star.Name.ToUpperInvariant();
        RoutingKit.Themed(_stripName, TextBlock.ForegroundProperty, _stripCurrent ? ThemeManager.CyanKey : ThemeManager.AKey);
        _copyHolder.Content = _copy is { } copy ? CopyGlyph.For(star.Name, copy) : null;

        _stripFacts.Inlines = [.. Facts(star)];
        _mark?.InvalidateVisual();
    }

    private IEnumerable<Run> Facts(GalaxyMapStar star)
    {
        if (_stripCurrent)
        {
            yield return Ink(YouAreHere);
        }
        else if (_position is { } here)
        {
            var away = Math.Sqrt(Math.Pow(star.At.X - here.X, 2) + Math.Pow(star.At.Y - here.Y, 2) + Math.Pow(star.At.Z - here.Z, 2));

            yield return Mono(away.ToString("N1", CultureInfo.CurrentCulture) + " LY");
            yield return Ink(" away");
        }

        var fid = _commander()?.Identity.FrontierId ?? string.Empty;
        var arrivals = (_travel() is { } log && fid.Length > 0 ? log.Steps(fid) : [])
            .Where(step => string.Equals(step.System, star.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        yield return Ink(_stripCurrent || _position is not null ? " · " : string.Empty);

        if (arrivals.Count == 0)
        {
            yield return Ink(NotVisited);
            yield break;
        }

        yield return Ink("arrived ");

        if (arrivals.Count == 1)
        {
            yield return Ink("once");
        }
        else
        {
            yield return Mono(arrivals.Count.ToString("N0", CultureInfo.CurrentCulture));
            yield return Ink(" times");
        }

        yield return Ink(", last ");
        yield return Mono(arrivals.Max(step => step.At).UtcDateTime.ToString("d MMM yyyy", CultureInfo.InvariantCulture));
    }

    private void CentreOnSelected()
    {
        if (_map.Selected is { } star)
        {
            Step(view => view with { CentreX = star.At.X, CentreZ = star.At.Z });
        }
    }

    private void BookmarkSelected()
    {
        var fid = _commander()?.Identity.FrontierId ?? string.Empty;

        if (_map.Selected is not { } star || _bookmarks is null || fid.Length == 0)
        {
            return;
        }

        var added = _bookmarks.Add(fid, star.Name, star.Name, _now());

        _stripResult.Text = added
            ? $"Bookmarked {star.Name}."
            : $"{star.Name} is already bookmarked under that name. The Bookmarks page renames it.";
        RoutingKit.Themed(_stripResult, TextBlock.ForegroundProperty, added ? ThemeManager.BlueKey : ThemeManager.WhiteKey);
        _stripResult.IsVisible = true;
        _stripFacts.IsVisible = !_headset;
    }

    private void Moved()
    {
        _framing.SelectedIndex = -1;
        Zoom();
    }

    private void Zoom()
    {
        _zoomIn.IsEnabled = _map.Viewport.LightYearsAcross > Closest;
        _zoomOut.IsEnabled = _map.Viewport.LightYearsAcross < Widest;
    }

    private void Where(string? offDuty, string? here)
    {
        if (offDuty is not null)
        {
            _note.Text = offDuty;
            _note.IsVisible = true;
            _system.IsVisible = _dot.IsVisible = _region.IsVisible = false;
            return;
        }

        _note.IsVisible = false;
        _system.Text = here?.ToUpperInvariant() ?? string.Empty;
        _system.IsVisible = !string.IsNullOrEmpty(here);

        var region = _position is { } at ? GalacticRegions.Find(at) : null;
        _region.Text = region?.ToUpperInvariant() ?? string.Empty;
        _region.IsVisible = region is not null;
        _dot.IsVisible = _system.IsVisible && _region.IsVisible;
    }

    private void Counts(TravelLog? log, int stepCount, TravelPicture travels)
    {
        _counts.Inlines = null;

        if (log is { HistoryFolded: false })
        {
            _counts.Text = Reading;
            return;
        }

        if (stepCount == 0)
        {
            _counts.Text = NoTravels;
            return;
        }

        if (travels.Systems.Count == 0)
        {
            _counts.Text = _range.SelectedIndex == ThirtyDays ? "No jumps in the last 30 days." : "No jumps in the last year.";
            return;
        }

        _counts.Text = null;
        _counts.Inlines =
        [
            Number(travels.Jumps),
            Ink(travels.Jumps == 1 ? " jump to " : " jumps to "),
            Number(travels.Systems.Count),
            Ink(travels.Systems.Count == 1 ? " system" : " systems"),
        ];
    }

    private void Legend(GalaxyMapDrawn drawn)
    {
        _legend.Children.Clear();

        if (drawn.Travels)
        {
            Entry("Travels", (context, ink) =>
            {
                context.DrawLine(new Pen(ink(ThemeManager.Grey2Key), 1), new Point(1, 8), new Point(21, 2));
                context.DrawRectangle(ink(ThemeManager.AKey), null, new Rect(-0.5, 6.5, 3, 3));
                context.DrawRectangle(ink(ThemeManager.AKey), null, new Rect(19.5, 0.5, 3, 3));
            });
        }

        if (drawn.Route)
        {
            Entry(_headset ? "Behind" : "Route behind", (context, ink) =>
                context.DrawLine(new Pen(ink(ThemeManager.GreyKey), 2), new Point(0, 5), new Point(22, 5)));
            Entry(_headset ? "Ahead" : "Route ahead", (context, ink) =>
                context.DrawLine(new Pen(ink(ThemeManager.AKey), 2), new Point(0, 5), new Point(22, 5)));
        }

        if (drawn.You)
        {
            Entry("You", (context, ink) => context.DrawRectangle(ink(ThemeManager.CyanKey), null, new Rect(7, 1, 8, 8)));
        }

        if (drawn.Start)
        {
            Entry("Start", (context, ink) => RouteMarks.Start(context, ink, new Point(11, 5)));
        }

        if (drawn.Destination)
        {
            Entry("Destination", (context, ink) => RouteMarks.Destination(context, ink, new Point(11, 5)));
        }

        foreach (var kind in KeyOrder.Where(drawn.Stars.Contains))
        {
            Entry(Word(kind), (context, ink) => RouteMarks.Star(context, ink, kind, new Point(11, 5), 0.7));
        }

        if (drawn.StopsHidden)
        {
            var hint = RoutingKit.Ink(StopsHidden, TypeScale.Meta, ThemeManager.Grey2Key, wrap: true);
            hint.MaxWidth = _legend.MaxWidth;
            hint.VerticalAlignment = VerticalAlignment.Center;
            _legend.Children.Add(hint);
        }
    }

    private void Entry(string word, Action<DrawingContext, Func<string, IBrush>> draw)
    {
        var label = RoutingKit.Tag(word, ThemeManager.GreyKey);
        label.FontSize = TypeScale.MetaSmall;
        label.FontWeight = FontWeight.Medium;
        label.LetterSpacing = TypeScale.MetaSmall * Fonts.ChromeTracking;

        _legend.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Children = { new Glyph(draw) { Width = 22, Height = 10, VerticalAlignment = VerticalAlignment.Center }, label },
        });
    }

    private static string Word(RouteStar kind) => kind switch
    {
        RouteStar.Neutron => "Neutron",
        RouteStar.WhiteDwarf => "White dwarf",
        RouteStar.BlackHole => "Black hole",
        _ => "No fuel",
    };

    private Control Toolbar()
    {
        var width = _headset ? 360 : 330;
        _framing.Width = width;
        _range.Width = width;

        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { Label("View"), _framing } };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { Label("Travels"), _range } };
        right.HorizontalAlignment = HorizontalAlignment.Right;

        var bar = new DockPanel();
        DockPanel.SetDock(right, Dock.Right);
        bar.Children.Add(right);
        bar.Children.Add(left);

        return bar;
    }

    private Control Framed()
    {
        var frame = new Border { IsHitTestVisible = false, BorderThickness = new Thickness(1) };
        RoutingKit.Themed(frame, Border.BorderBrushProperty, ThemeManager.LineKey);

        var grid = new Grid { Children = { _map, frame } };

        if (_headset)
        {
            _strip.VerticalAlignment = VerticalAlignment.Bottom;
            grid.Children.Insert(1, _strip);
        }

        return grid;
    }

    private Control ZoomPair()
    {
        var grid = Cells(2, 1);
        Place(grid, _zoomIn, 0, 0);
        Place(grid, _zoomOut, 1, 0);

        return grid;
    }

    private Control PanPad()
    {
        var grid = Cells(3, 3);

        Place(grid, PanTile("▲", "Pan up", 0, 1), 1, 0);
        Place(grid, PanTile("◄", "Pan left", -1, 0), 0, 1);
        Place(grid, PanTile("►", "Pan right", 1, 0), 2, 1);
        Place(grid, PanTile("▼", "Pan down", 0, -1), 1, 2);

        return grid;
    }

    private Button PanTile(string glyph, string says, int across, int up)
    {
        var tile = Tile(glyph, says, TypeScale.Body);
        tile.Click += (_, _) => Step(view => view.Pan(across, up));

        return tile;
    }

    private StackPanel Switches() => new() { Spacing = 2, Children = { _travels, _routeShown, _regions } };

    private Control WhereLine()
    {
        var where = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _system, _dot, _region },
        };

        var line = new DockPanel { Height = 22 };

        if (!_headset)
        {
            _counts.VerticalAlignment = VerticalAlignment.Center;
            _counts.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(_counts, Dock.Right);
            line.Children.Add(_counts);
        }

        _note.VerticalAlignment = VerticalAlignment.Center;
        line.Children.Add(new Avalonia.Controls.Panel { Children = { where, _note } });

        return line;
    }

    private Control WindowLayout()
    {
        var rail = new StackPanel { Spacing = 12, Children = { ZoomPair(), PanPad(), Switches() } };
        var body = new Grid
        {
            MinHeight = WindowMapHeight,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(2, GridUnitType.Pixel), new ColumnDefinition(SwitchRail, GridUnitType.Pixel)],
        };

        Place(body, Framed(), 0, 0);
        Place(body, rail, 2, 0);

        // The map takes the height the window leaves after the toolbar and the lines under it. The strip is drawn
        // in the same cell as those lines, which stay in the layout while it shows, so selecting does not move the map.
        var page = new Grid
        {
            RowSpacing = 10,
            Margin = new Thickness(0, 4, 0, 0),
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)],
        };

        var under = new StackPanel { Spacing = 10, Children = { WhereLine(), _legend } };
        _replaced = [under];
        _strip.VerticalAlignment = VerticalAlignment.Top;

        Place(page, Toolbar(), 0, 0);
        Place(page, body, 0, 1);
        Place(page, new Grid { Children = { under, _strip } }, 0, 2);

        return page;
    }

    /// <summary>
    /// On the big panel the page has 435 px under the header: the line under the map goes in the pad rail and
    /// the legend under the switches, so the map keeps its height. There is no room for the strip's own row, so
    /// it covers the bottom of the map.
    /// </summary>
    private Control PanelLayout()
    {
        foreach (var text in new[] { _counts, _system, _note })
        {
            text.TextWrapping = TextWrapping.Wrap;
            text.TextTrimming = TextTrimming.None;
            text.MaxWidth = PadRail;
        }

        _legend.MaxWidth = SwitchRail;

        var where = new StackPanel { Spacing = 2, Children = { _system, _region, _note } };
        var pad = new StackPanel { Spacing = 6, Children = { ZoomPair(), PanPad(), _counts, where } };
        var side = new StackPanel { Spacing = 10, Children = { Switches(), _legend } };
        var body = new Grid
        {
            Height = PanelMapHeight,
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(2, GridUnitType.Pixel),
                new ColumnDefinition(PadRail, GridUnitType.Pixel),
                new ColumnDefinition(2, GridUnitType.Pixel),
                new ColumnDefinition(SwitchRail, GridUnitType.Pixel),
            ],
        };

        Place(body, Framed(), 0, 0);
        Place(body, pad, 2, 0);
        Place(body, side, 4, 0);

        _replaced = [_counts, where, _legend];

        return new StackPanel
        {
            Spacing = 4,
            Children = { Toolbar(), body },
        };
    }

    private static Grid Cells(int columns, int rows)
    {
        var grid = new Grid { ColumnSpacing = 2, RowSpacing = 2 };

        for (var column = 0; column < columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (var row = 0; row < rows; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(TypeScale.MinimumTarget, GridUnitType.Pixel));
        }

        return grid;
    }

    private static void Place(Grid grid, Control control, int column, int row)
    {
        Grid.SetColumn(control, column);
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }

    private static Button Tile(string glyph, string says, double size)
    {
        var button = new Button
        {
            Content = glyph,
            FontSize = size,
            Padding = default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        ToolTip.SetTip(button, says);
        AutomationProperties.SetName(button, says);

        return button;
    }

    private static Button Press(string label, double width)
    {
        var button = new Button
        {
            Content = label,
            Width = width,
            Height = TypeScale.MinimumTarget,
            Padding = default,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        AutomationProperties.SetName(button, label);

        return button;
    }

    private static CheckBox Switch(string label)
    {
        var box = LabeledCheckBox.Caps(label);
        box.IsChecked = true;
        box.HorizontalAlignment = HorizontalAlignment.Stretch;

        return box;
    }

    private static TextBlock Label(string text)
    {
        var label = RoutingKit.Tag(text, ThemeManager.GreyKey);
        label.FontWeight = FontWeight.Medium;

        return label;
    }

    private static TextBlock Chrome(double size, string key)
    {
        var text = RoutingKit.Ink(string.Empty, size, key);
        text.FontFamily = new FontFamily(Fonts.ChromeFamily);
        text.FontWeight = FontWeight.Medium;
        text.VerticalAlignment = VerticalAlignment.Center;

        return text;
    }

    private static Run Ink(string text)
    {
        var run = new Run(text);
        RoutingKit.Themed(run, TextElement.ForegroundProperty, ThemeManager.GreyKey);

        return run;
    }

    private static Run Mono(string text)
    {
        var run = Ink(text);
        run.FontFamily = new FontFamily(Fonts.MonoFamily);

        return run;
    }

    private static Run Number(int value)
    {
        var run = Ink(value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture));
        run.FontFamily = new FontFamily(Fonts.MonoFamily);

        return run;
    }

    /// <summary>A legend mark drawn in theme inks.</summary>
    private sealed class Glyph : Control
    {
        private readonly Action<DrawingContext, Func<string, IBrush>> _draw;

        public Glyph(Action<DrawingContext, Func<string, IBrush>> draw)
        {
            _draw = draw;

            ResourcesChanged += (_, _) => InvalidateVisual();
            ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        }

        public override void Render(DrawingContext context) =>
            _draw(context, key => this.TryFindResource(key, ActualThemeVariant, out var found) && found is IBrush brush ? brush : Brushes.Transparent);
    }
}
