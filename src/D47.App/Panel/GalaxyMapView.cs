using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>A system the map can select.</summary>
public sealed record GalaxyMapStar(string Name, StarPosition At);

/// <summary>The kinds of marker the map draws beside the travels, in the order a row of them runs.</summary>
public enum GalaxyMapMarkKind
{
    Engineer,
    Bookmark,
    Carrier,
    Ships,
    GuardianBeacon,
    FarmingSite,
    Mission,
    SystemResult,
    StationResult,
    BodyResult,
}

/// <summary>One kind of marker in one system, with the names the strip lists; one mark is drawn however many names it holds.</summary>
public sealed record GalaxyMapMark(GalaxyMapMarkKind Kind, string System, StarPosition At, IReadOnlyList<string> Names);

/// <summary>What the galaxy map draws; a null part is not drawn.</summary>
/// <param name="Reached">The index in <paramref name="Route"/> of the stop the Commander is on, or -1.</param>
/// <param name="Visited">The systems drawn as visited, by name; selectable while <paramref name="Travels"/> is drawn.</param>
/// <param name="Marks">The markers to draw; engineers only while the view is <see cref="GalaxyMapView.DetailAcross"/> across or less.</param>
/// <param name="Unvisited">Systems the Commander has not visited, drawn and selectable while the view is <see cref="GalaxyMapView.DetailAcross"/> across or less.</param>
public sealed record GalaxyMapScene(
    bool Regions,
    TravelPicture? Travels,
    IReadOnlyList<RouteMapStop>? Route,
    int Reached,
    StarPosition? Commander,
    string? Here,
    IReadOnlyList<GalaxyMapStar>? Visited = null,
    IReadOnlyList<GalaxyMapMark>? Marks = null,
    IReadOnlyList<GalaxyMapStar>? Unvisited = null)
{
    public static GalaxyMapScene Empty { get; } = new(true, null, null, -1, null, null);
}

/// <summary>What the last rebuild put in view, for the legend under the map.</summary>
public sealed record GalaxyMapDrawn(
    bool Travels,
    bool Route,
    bool You,
    bool Start,
    bool Destination,
    bool StopsHidden,
    bool RegionNames,
    IReadOnlyList<RouteStar> Stars,
    bool EngineersHidden = false,
    bool Unvisited = false)
{
    /// <summary>The mark kinds with a mark drawn inside the view, in <see cref="GalaxyMapMarkKind"/> order.</summary>
    public IReadOnlyList<GalaxyMapMarkKind> MarkKinds { get; init; } = [];

    public static GalaxyMapDrawn Nothing { get; } = new(false, false, false, false, false, false, false, []);
}

/// <summary>
/// The galaxy through a <see cref="GalaxyViewport"/> at its heading and tilt (#1406): Elite's regions on the plane, the
/// Commander's travels, the plotted route and the Commander. Geometry is rebuilt when the scene, the viewport or the size changes.
/// </summary>
/// <remarks>
/// On the desktop the wheel zooms one step, a left drag turns and tilts the map as the hull viewer's does, and a right
/// or middle drag pans. A left press and release at one point, on the desktop or the
/// big panel, selects the nearest system drawn whose centre is inside a <see cref="TapSide"/> px square
/// on the tap, or clears the selection. The selection stays while its system is not drawn.
/// </remarks>
public sealed class GalaxyMapView : Control
{
    /// <summary>The widest view that draws route stops and system names, and the narrowest that draws region names.</summary>
    public const double DetailAcross = 6_400;

    public const double TapSide = 44;

    private const double NameOffset = 16;
    private const double EdgeMargin = 4;
    private const double Visited = 3;
    private const double ScaleInset = 12;
    private const double DragStart = 4;
    private const double AngleBaseline = 20;
    private const double PlaneInset = 10;
    private const double PlaneGap = 6;
    private const double BracketHalf = 12;
    private const double BracketLeg = 6;
    private const double BracketGap = 8;
    private const int RegionNameWrap = 14;
    private const double NameGap = 2;
    private const double BracketClear = 5;
    private const double CountGap = 9;

    private static readonly (double X, double Y)[] CrossHalo = [(-3, -7), (3, -7), (3, -3), (7, -3), (7, 3), (3, 3), (3, 7), (-3, 7), (-3, 3), (-7, 3), (-7, -3), (-3, -3)];
    private static readonly (double X, double Y)[] Cross = [(-1.5, -5.5), (1.5, -5.5), (1.5, -1.5), (5.5, -1.5), (5.5, 1.5), (1.5, 1.5), (1.5, 5.5), (-1.5, 5.5), (-1.5, 1.5), (-5.5, 1.5), (-5.5, -1.5), (-1.5, -1.5)];
    private static readonly (double X, double Y)[] RibbonHalo = [(-5.5, -7), (5.5, -7), (5.5, 8.5), (0, 4.5), (-5.5, 8.5)];
    private static readonly (double X, double Y)[] Ribbon = [(-4, -5.5), (4, -5.5), (4, 5.5), (0, 2), (-4, 5.5)];
    private static readonly (double X, double Y)[] HullHalo = [(-8.5, -1.5), (-3.5, -1.5), (-3.5, -5.5), (3.5, -5.5), (3.5, -1.5), (8.5, -1.5), (8.5, 5.5), (-8.5, 5.5)];
    private static readonly (double X, double Y)[] Hull = [(-7, 0), (-2, 0), (-2, -4), (2, -4), (2, 0), (7, 0), (7, 4), (-7, 4)];
    private static readonly (double X, double Y)[] ChevronHalo = [(0, -8.5), (7.5, 6.5), (0, 3.8), (-7.5, 6.5)];
    private static readonly (double X, double Y)[] Chevron = [(0, -6), (5.5, 5), (0, 2), (-5.5, 5)];
    private static readonly (double X, double Y)[] PylonHalo = [(0, -9.6), (4.6, -5), (4.6, 7.6), (-4.6, 7.6), (-4.6, -5)];
    private static readonly (double X, double Y)[] Pylon = [(0, -7), (3, -4), (3, 6), (-3, 6), (-3, -4)];
    private static readonly (double X, double Y)[] HexHalo = [(0, -7.2), (6.3, -3.6), (6.3, 3.6), (0, 7.2), (-6.3, 3.6), (-6.3, -3.6)];
    private static readonly (double X, double Y)[] FlagHalo = [(-6, -7.5), (-2, -7.5), (8.5, -2.5), (-1, 2.6), (-1, 7.5), (-6, 7.5)];
    private static readonly (double X, double Y)[] Flag = [(-4.5, -6), (-2.5, -6), (5.5, -2.5), (-2.5, 1), (-2.5, 6), (-4.5, 6)];
    private static readonly (double X, double Y)[] Hex = [(0, -5.5), (4.8, -2.75), (4.8, 2.75), (0, 5.5), (-4.8, 2.75), (-4.8, -2.75)];
    private static readonly (double X, double Y)[] Triangle = [(0, -5), (5.5, 4.5), (-5.5, 4.5)];
    private static readonly (double X, double Y)[] Box = [(-6, -4), (6, -4), (6, 4), (-6, 4)];
    private static readonly (double X, double Y)[] Slot = [(-3, -0.75), (3, -0.75), (3, 0.75), (-3, 0.75)];
    private static readonly (double X, double Y)[] Octagon = [(-2.2, -5.25), (2.2, -5.25), (5.25, -2.2), (5.25, 2.2), (2.2, 5.25), (-2.2, 5.25), (-5.25, 2.2), (-5.25, -2.2)];

    private const double HollowPen = 1.5;
    private const double HollowHalo = 4.5;

    private static Color? _groundColour;
    private static WriteableBitmap? _ground;
    private static StreamGeometry? _borders;
    private static StreamGeometry? _angledBorders;
    private static (double Heading, double Tilt) _angledFor;
    private static IReadOnlyList<(string Name, StarPosition At)>? _regionNames;

    private GalaxyMapScene _scene = GalaxyMapScene.Empty;
    private GalaxyViewport _viewport = GalaxyViewport.WholeGalaxy;

    private StreamGeometry? _legs;
    private (double Heading, double Tilt) _legsFor;
    private StreamGeometry? _visited;
    private StreamGeometry? _unvisited;
    private GalaxyMapLayout _layout = GalaxyMapLayout.Empty;
    private Size _builtFor;
    private bool _stale = true;
    private GalaxyMapStar? _selected;
    private Point? _selectedAt;
    private double _bottomInset;
    private double? _fitInset;
    private bool _fitEachAxis;

    private Point? _pressedAt;
    private MouseButton _pressedButton;
    private GalaxyViewport? _pressedView;
    private bool _dragging;

    public GalaxyMapView()
    {
        ClipToBounds = true;
        Focusable = false;

        ResourcesChanged += (_, _) => InvalidateVisual();
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>Raised when the wheel or a drag moves the view.</summary>
    public event Action<GalaxyViewport>? ViewportChanged;

    /// <summary>Raised when a tap or <see cref="Clear"/> changes the selection.</summary>
    public event Action<GalaxyMapStar?>? Picked;

    /// <summary>Raised after each rebuild, with what is in view.</summary>
    public event Action<GalaxyMapDrawn>? Redrawn;

    /// <summary>Pixels at the bottom of the map that something else covers; the scale bar sits above them.</summary>
    public double BottomInset
    {
        get => _bottomInset;
        set
        {
            if (_bottomInset == value)
            {
                return;
            }

            _bottomInset = value;
            InvalidateVisual();
        }
    }

    public GalaxyViewport Viewport => _viewport;

    public GalaxyMapDrawn Drawn { get; private set; } = GalaxyMapDrawn.Nothing;

    public GalaxyMapStar? Selected => _selected;

    public void Clear()
    {
        if (_selected is null)
        {
            return;
        }

        _selected = null;
        _selectedAt = null;
        InvalidateVisual();
        Picked?.Invoke(null);
    }

    public void Show(GalaxyMapScene scene)
    {
        _scene = scene;
        _legs = null;
        Invalidate();
    }

    /// <summary>Frames the scene's route with <paramref name="inset"/> px clear each side at any width, refitted on resize; <paramref name="eachAxis"/> fits width and height separately and a single stop.</summary>
    public void Fit(double inset, bool eachAxis = false)
    {
        _fitInset = inset;
        _fitEachAxis = eachAxis;
        Invalidate();
    }

    public void View(GalaxyViewport viewport)
    {
        _viewport = viewport;
        Invalidate();
    }

    private void Invalidate()
    {
        _stale = true;
        Rebuild();
        InvalidateVisual();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Invalidate();
    }

    private double Scale => Math.Min(Bounds.Width, Bounds.Height) / _viewport.LightYearsAcross;

    private bool Wide => _viewport.LightYearsAcross >= DetailAcross;

    private (double Heading, double Tilt) Angles => (_viewport.Heading, _viewport.Tilt);

    /// <summary>True at tilt 90, where the plane has no height on screen.</summary>
    private bool EdgeOn => Math.Cos(_viewport.Tilt * Math.PI / 180) < 1e-6;

    private Point Screen(StarPosition at)
    {
        var (x, y) = _viewport.ToScreen(at, Bounds.Width, Bounds.Height);

        return new Point(x, y);
    }

    private bool InView(Point at, double slack) =>
        at.X >= -slack && at.Y >= -slack && at.X <= Bounds.Width + slack && at.Y <= Bounds.Height + slack;

    private void Rebuild()
    {
        if (!_stale || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        _stale = false;
        _builtFor = Bounds.Size;

        _layout = GalaxyMapLayout.Of(_scene, _viewport, Bounds.Size, _fitInset, _fitEachAxis);
        _viewport = _layout.Viewport;

        if (_scene.Travels is { } travels && (_legs is null || _legsFor != Angles))
        {
            _legs = Legs(travels, _viewport);
            _legsFor = Angles;
        }

        _visited = _scene.Travels is { } picture ? Squares(picture.Systems) : null;
        _unvisited = _layout.Unvisited.Count > 0 ? Outlines(_layout.Unvisited.Select(star => star.At)) : null;

        Drawn = _layout.Drawn;

        Reselect();

        Redrawn?.Invoke(Drawn);
    }

    /// <summary>Follows the selected system to where it is drawn now; it has no mark while it is not drawn.</summary>
    private void Reselect()
    {
        if (_selected is not { } was)
        {
            return;
        }

        var found = _layout.Targets.FirstOrDefault(target => string.Equals(target.Star.Name, was.Name, StringComparison.OrdinalIgnoreCase));

        _selected = found.Star ?? was;
        _selectedAt = found.Star is null ? null : Screen(found.Star.At);
    }

    private void Pick(Point at)
    {
        var near = _layout.Targets.Where(target => Math.Abs(target.At.X - at.X) <= TapSide / 2 && Math.Abs(target.At.Y - at.Y) <= TapSide / 2).ToList();
        var index = GalaxyMapHit.Nearest([.. near.Select(target => (target.At.X, target.At.Y))], (at.X, at.Y), double.MaxValue);

        _selected = index is { } i ? near[i].Star : null;
        _selectedAt = index is { } j ? Screen(near[j].Star.At) : null;

        InvalidateVisual();
        Picked?.Invoke(_selected);
    }

    /// <summary>Every leg in the view's face coordinates at the viewport's angles.</summary>
    private static StreamGeometry Legs(TravelPicture travels, GalaxyViewport viewport)
    {
        var geometry = new StreamGeometry();

        using var open = geometry.Open();

        foreach (var line in travels.Lines.Where(line => line.Count >= 2))
        {
            open.BeginFigure(Faced(viewport, line[0]), false);

            foreach (var at in line.Skip(1))
            {
                open.LineTo(Faced(viewport, at));
            }

            open.EndFigure(false);
        }

        return geometry;
    }

    private StreamGeometry Squares(IReadOnlyList<StarPosition> systems)
    {
        var geometry = new StreamGeometry();
        const double Half = Visited / 2;

        using var open = geometry.Open();

        foreach (var system in systems)
        {
            var at = Screen(system);

            if (!InView(at, Visited))
            {
                continue;
            }

            open.BeginFigure(new Point(at.X - Half, at.Y - Half), true);
            open.LineTo(new Point(at.X + Half, at.Y - Half));
            open.LineTo(new Point(at.X + Half, at.Y + Half));
            open.LineTo(new Point(at.X - Half, at.Y + Half));
            open.EndFigure(true);
        }

        return geometry;
    }

    /// <summary>A 5 px square outline, 1 px wide, on whole pixels round each point.</summary>
    private static StreamGeometry Outlines(IEnumerable<Point> points)
    {
        var geometry = new StreamGeometry();
        const double Inner = GalaxyMapLayout.UnvisitedSide - 1;

        using var open = geometry.Open();

        foreach (var at in points)
        {
            var left = Math.Floor(at.X - (GalaxyMapLayout.UnvisitedSide / 2)) + 0.5;
            var top = Math.Floor(at.Y - (GalaxyMapLayout.UnvisitedSide / 2)) + 0.5;

            open.BeginFigure(new Point(left, top), false);
            open.LineTo(new Point(left + Inner, top));
            open.LineTo(new Point(left + Inner, top + Inner));
            open.LineTo(new Point(left, top + Inner));
            open.EndFigure(true);
        }

        return geometry;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Size != _builtFor)
        {
            _stale = true;
            Rebuild();
        }

        var size = Bounds.Size;
        context.DrawRectangle(Brush(ThemeManager.BgKey), null, new Rect(size));

        var scale = Scale;
        var centre = _viewport.Face(new StarPosition(_viewport.CentreX, _viewport.CentreY, _viewport.CentreZ));
        var toScreen = new Matrix(
            scale, 0,
            0, -scale,
            (size.Width / 2) - (centre.Across * scale),
            (size.Height / 2) + (centre.Up * scale));

        if (_scene.Regions)
        {
            if (!EdgeOn)
            {
                Ground(context, size);

                using (context.PushTransform(toScreen))
                {
                    context.DrawGeometry(null, new Pen(Brush(ThemeManager.Line2Key), 1 / scale), Borders(_viewport));
                }
            }

            if (Wide)
            {
                RegionNames(context, size, AngleBox());
            }
        }

        if (_unvisited is { } unvisited)
        {
            context.DrawGeometry(null, new Pen(Brush(ThemeManager.GreyKey), 1), unvisited);
        }

        if (_legs is { } legs && _scene.Travels is not null)
        {
            using (context.PushTransform(toScreen))
            {
                context.DrawGeometry(null, new Pen(Brush(ThemeManager.Grey2Key), 1 / scale), legs);
            }
        }

        if (_visited is { } visited)
        {
            context.DrawGeometry(Brush(ThemeManager.AKey), null, visited);
        }

        var taken = new List<Rect>();

        if (AngleBox() is { } readout)
        {
            taken.Add(readout);
        }

        if (_layout.Route.Count > 0)
        {
            Line(context, [.. _layout.Route.Take(_layout.HereIndex + 1)], ThemeManager.GreyKey);
            Line(context, [.. _layout.Route.Skip(Math.Max(_layout.HereIndex, 0))], ThemeManager.AKey);

            if (_layout.Detailed)
            {
                foreach (var (kind, at) in _layout.Stars)
                {
                    RouteMarks.Star(context, Brush, kind, at, 1);
                }

                RouteMarks.Start(context, Brush, _layout.Route[0]);
                RouteMarks.Destination(context, Brush, _layout.Route[^1]);
            }
        }

        foreach (var mark in _layout.Marks)
        {
            DrawMark(context, mark);
        }

        if (_layout.Detailed)
        {
            if (_layout.You is { } here && !string.IsNullOrEmpty(_scene.Here))
            {
                taken.Add(DrawName(context, _scene.Here, here, ThemeManager.CyanKey, size, taken));
            }

            if (_layout.Route.Count > 0 && !string.IsNullOrEmpty(_layout.Destination))
            {
                DrawName(context, _layout.Destination, _layout.Route[^1], ThemeManager.AKey, size, taken);
            }
        }

        if (_layout.You is { } you)
        {
            RouteMarks.You(context, Brush, you);
        }

        if (_selectedAt is { } picked && _selected is { } star)
        {
            var bounds = SelectionBounds(picked, star.Name);

            Brackets(context, bounds);

            var named = string.Equals(star.Name, _scene.Here, StringComparison.OrdinalIgnoreCase)
                || (_layout.Detailed && _layout.Route.Count > 0 && string.Equals(star.Name, _layout.Destination, StringComparison.OrdinalIgnoreCase));

            if (!named)
            {
                SelectedName(context, star.Name, bounds, size);
            }
        }

        if (_fitInset is null)
        {
            ScaleBar(context, size);
        }

        DrawAngles(context, size);
    }

    /// <summary>"heading 330° · tilt 30°"; the map at rest has no readout.</summary>
    internal static string AngleWords(double heading, double tilt) =>
        string.Create(CultureInfo.InvariantCulture, $"heading {heading:0}° · tilt {tilt:0}°");

    private bool Turned => _viewport.Heading != 0 || _viewport.Tilt != 0;

    private TextLayout AngleText() => Text(AngleWords(_viewport.Heading, _viewport.Tilt), 11, ThemeManager.GreyKey, 0, Fonts.MonoFamily);

    /// <summary>Where the angle readout draws: 12 px in from the left with its baseline 20 px down; null at rest.</summary>
    private Rect? AngleBox()
    {
        if (!Turned)
        {
            return null;
        }

        var text = AngleText();

        return new Rect(ScaleInset, AngleBaseline - text.Baseline, text.Width, text.Height);
    }

    private void DrawAngles(DrawingContext context, Size size)
    {
        if (AngleBox() is not { } box)
        {
            return;
        }

        AngleText().Draw(context, box.TopLeft);

        if (!EdgeOn)
        {
            return;
        }

        var y = Math.Round(Screen(new StarPosition(_viewport.CentreX, 0, _viewport.CentreZ)).Y) + 0.5;

        context.DrawLine(new Pen(Brush(ThemeManager.LineKey), 1), new Point(0, y), new Point(size.Width, y));

        var plane = new TextLayout(
            "GALACTIC PLANE",
            new Typeface(new FontFamily(Fonts.ChromeFamily), FontStyle.Normal, FontWeight.SemiBold),
            11,
            Brush(ThemeManager.GreyKey),
            letterSpacing: 11 * 0.06);

        plane.Draw(context, new Point(PlaneInset, y - PlaneGap - plane.Height));
    }

    private void Ground(DrawingContext context, Size size)
    {
        var colour = (Brush(ThemeManager.MapGroundKey) as ISolidColorBrush)?.Color ?? Colors.Transparent;
        var ground = GroundImage(colour);
        var extent = GalacticRegions.Extent;

        if (_viewport.Heading != 0 || _viewport.Tilt != 0)
        {
            var cell = GroundCell();
            var origin = Screen(new StarPosition(extent.X0, 0, extent.Z1));
            var across = Screen(new StarPosition(extent.X0 + cell, 0, extent.Z1)) - origin;
            var down = Screen(new StarPosition(extent.X0, 0, extent.Z1 - cell)) - origin;
            var pixels = new Rect(0, 0, ground.PixelSize.Width, ground.PixelSize.Height);

            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            using (context.PushTransform(new Matrix(across.X, across.Y, down.X, down.Y, origin.X, origin.Y)))
            {
                context.DrawImage(ground, pixels, pixels);
            }

            return;
        }

        var topLeft = Screen(new StarPosition(extent.X0, 0, extent.Z1));
        var bottomRight = Screen(new StarPosition(extent.X1, 0, extent.Z0));
        var whole = new Rect(topLeft, bottomRight);
        var shown = whole.Intersect(new Rect(size));

        if (shown.Width <= 0 || shown.Height <= 0)
        {
            return;
        }

        var perPixelX = ground.PixelSize.Width / whole.Width;
        var perPixelY = ground.PixelSize.Height / whole.Height;
        var source = new Rect(
            (shown.X - whole.X) * perPixelX,
            (shown.Y - whole.Y) * perPixelY,
            shown.Width * perPixelX,
            shown.Height * perPixelY);

        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
        {
            context.DrawImage(ground, source, shown);
        }
    }

    /// <summary>One pixel per region cell, the ground colour inside the grid and clear outside; drawn once per colour.</summary>
    private static WriteableBitmap GroundImage(Color colour)
    {
        if (_ground is { } cached && _groundColour == colour)
        {
            return cached;
        }

        var extent = GalacticRegions.Extent;
        var runs = GalacticRegions.Runs();
        var cell = GroundCell();
        var width = Math.Max(1, (int)Math.Round((extent.X1 - extent.X0) / cell));
        var height = Math.Max(1, (int)Math.Round((extent.Z1 - extent.Z0) / cell));
        var pixels = new int[width * height];
        var ink = (colour.A << 24) | (colour.R << 16) | (colour.G << 8) | colour.B;

        foreach (var run in runs)
        {
            var row = height - 1 - (int)Math.Round((run.Z0 - extent.Z0) / cell);
            var from = (int)Math.Round((run.X0 - extent.X0) / cell);
            var to = Math.Min(width, (int)Math.Round((run.X1 - extent.X0) / cell));

            if (row >= 0 && row < height && from < to)
            {
                Array.Fill(pixels, ink, (row * width) + from, to - from);
            }
        }

        var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        using (var frame = bitmap.Lock())
        {
            for (var row = 0; row < height; row++)
            {
                Marshal.Copy(pixels, row * width, frame.Address + (row * frame.RowBytes), width);
            }
        }

        _ground?.Dispose();
        _ground = bitmap;
        _groundColour = colour;

        return bitmap;
    }

    /// <summary>The light years one ground pixel covers on each side.</summary>
    private static double GroundCell()
    {
        var runs = GalacticRegions.Runs();

        return runs.Count == 0 ? 1 : runs[0].Z1 - runs[0].Z0;
    }

    private static Point Faced(GalaxyViewport viewport, StarPosition at)
    {
        var (across, up) = viewport.Face(at);

        return new Point(across, up);
    }

    /// <summary>Every region border in the view's face coordinates; the set at rest and the last turned set are kept.</summary>
    private static StreamGeometry Borders(GalaxyViewport viewport)
    {
        var angles = (viewport.Heading, viewport.Tilt);
        var atRest = angles == (0, 0);

        if (atRest && _borders is { } still)
        {
            return still;
        }

        if (!atRest && _angledBorders is { } turned && _angledFor == angles)
        {
            return turned;
        }

        var geometry = new StreamGeometry();

        using (var open = geometry.Open())
        {
            foreach (var edge in GalacticRegions.Borders())
            {
                open.BeginFigure(Faced(viewport, new StarPosition(edge.X0, 0, edge.Z0)), false);
                open.LineTo(Faced(viewport, new StarPosition(edge.X1, 0, edge.Z1)));
                open.EndFigure(false);
            }
        }

        if (atRest)
        {
            return _borders = geometry;
        }

        _angledFor = angles;

        return _angledBorders = geometry;
    }

    /// <summary>Each region's name and label point, largest region first.</summary>
    private static IReadOnlyList<(string Name, StarPosition At)> RegionLabels() =>
        _regionNames ??= [.. GalacticRegions.Runs()
            .GroupBy(run => run.RegionId)
            .OrderByDescending(region => region.Sum(run => (run.X1 - run.X0) * (run.Z1 - run.Z0)))
            .Select(region => (Name: GalacticRegions.NameOf(region.Key), At: GalacticRegions.LabelAt(region.Key)))
            .Where(label => label.Name is not null && label.At is not null)
            .Select(label => (label.Name!, label.At!.Value))];

    private void RegionNames(DrawingContext context, Size size, Rect? readout)
    {
        var taken = new List<Rect>();

        if (readout is { } held)
        {
            taken.Add(held);
        }

        var room = new Rect(size).Deflate(EdgeMargin);

        foreach (var (name, at) in RegionLabels())
        {
            var centre = Screen(at);
            var lines = Wrap(name.ToUpperInvariant(), RegionNameWrap)
                .Select(line => Text(line, 10, ThemeManager.GreyKey, 0.6))
                .ToList();

            var width = lines.Max(line => line.Width);
            var height = lines.Sum(line => line.Height);
            var box = new Rect(centre.X - (width / 2), centre.Y - (height / 2), width, height);

            if (!room.Contains(box) || taken.Any(other => other.Inflate(NameGap).Intersects(box)))
            {
                continue;
            }

            taken.Add(box);

            var y = box.Y;

            foreach (var line in lines)
            {
                line.Draw(context, new Point(centre.X - (line.Width / 2), y));
                y += line.Height;
            }
        }
    }

    /// <summary>Words joined into lines of no more than <paramref name="most"/> characters, a longer word alone.</summary>
    internal static IReadOnlyList<string> Wrap(string text, int most)
    {
        var lines = new List<string>();
        var line = string.Empty;

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length == 0)
            {
                line = word;
            }
            else if (line.Length + 1 + word.Length <= most)
            {
                line += " " + word;
            }
            else
            {
                lines.Add(line);
                line = word;
            }
        }

        if (line.Length > 0)
        {
            lines.Add(line);
        }

        return lines;
    }

    private void Line(DrawingContext context, IReadOnlyList<Point> stops, string key)
    {
        if (stops.Count < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();

        using (var open = geometry.Open())
        {
            open.BeginFigure(stops[0], false);

            foreach (var stop in stops.Skip(1))
            {
                open.LineTo(stop);
            }

            open.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(Brush(key), 2, lineJoin: PenLineJoin.Round, lineCap: PenLineCap.Round), geometry);
    }

    /// <summary>
    /// The rectangle the selection brackets stand on: 5 px clear of every mark drawn for the system, and at least the
    /// 24 px square round a lone star.
    /// </summary>
    private Rect SelectionBounds(Point at, string name)
    {
        var bounds = new Rect(at.X - BracketHalf, at.Y - BracketHalf, 2 * BracketHalf, 2 * BracketHalf);

        foreach (var mark in _layout.Marks.Where(mark => string.Equals(mark.Star.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            bounds = bounds.Union(MarkBounds(mark).Inflate(BracketClear));
        }

        return bounds;
    }

    private static Rect MarkBounds(GalaxyMapLayout.Mark mark)
    {
        var (left, top, right, bottom) = mark.Kind switch
        {
            GalaxyMapMarkKind.Engineer => (-7.0, -7.0, 7.0, 7.0),
            GalaxyMapMarkKind.Bookmark => (-5.5, -7.0, 5.5, 8.5),
            GalaxyMapMarkKind.Carrier => (-8.5, -5.5, 8.5, 5.5),
            GalaxyMapMarkKind.GuardianBeacon => (-4.6, -9.6, 4.6, 7.6),
            GalaxyMapMarkKind.FarmingSite => (-6.3, -7.2, 6.3, 7.2),
            GalaxyMapMarkKind.Mission => (-6.0, -7.5, 8.5, 7.5),
            GalaxyMapMarkKind.SystemResult => (-9.4, -9.5, 9.4, 6.75),
            GalaxyMapMarkKind.StationResult => (-8.25, -6.25, 8.25, 6.25),
            GalaxyMapMarkKind.BodyResult => (-7.5, -7.5, 7.5, 7.5),
            _ => (-7.5, -8.5, mark.Count >= 2 ? CountGap + (6 * mark.Count.ToString("N0", CultureInfo.InvariantCulture).Length) + 1.5 : 7.5, 6.5),
        };

        return new Rect(mark.At.X + left, mark.At.Y + top, right - left, bottom - top);
    }

    private void DrawMark(DrawingContext context, GalaxyMapLayout.Mark mark)
    {
        if (Hollow(mark.Kind, mark.At, 1) is { } hollow)
        {
            context.DrawGeometry(Brush(ThemeManager.BgKey), new Pen(Brush(ThemeManager.BgKey), HollowHalo), hollow.Outline);
            DrawHollow(context, Brush, hollow);
            return;
        }

        var (halo, ink, key) = mark.Kind switch
        {
            GalaxyMapMarkKind.Engineer => (CrossHalo, Cross, ThemeManager.WhiteKey),
            GalaxyMapMarkKind.Bookmark => (RibbonHalo, Ribbon, ThemeManager.CyanKey),
            GalaxyMapMarkKind.Carrier => (HullHalo, Hull, ThemeManager.YellowKey),
            GalaxyMapMarkKind.GuardianBeacon => (PylonHalo, Pylon, ThemeManager.AKey),
            GalaxyMapMarkKind.FarmingSite => (HexHalo, Hex, ThemeManager.AKey),
            GalaxyMapMarkKind.Mission => (FlagHalo, Flag, ThemeManager.CyanKey),
            _ => (ChevronHalo, Chevron, ThemeManager.YellowKey),
        };

        context.DrawGeometry(Brush(ThemeManager.BgKey), null, Polygon(halo, mark.At));
        context.DrawGeometry(Brush(key), null, Polygon(ink, mark.At));

        if (mark.Kind != GalaxyMapMarkKind.Ships || mark.Count < 2)
        {
            return;
        }

        var count = new FormattedText(
            mark.Count.ToString("N0", CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily(Fonts.MonoFamily)),
            10,
            Brush(ThemeManager.YellowKey));

        if (count.BuildGeometry(new Point(mark.At.X + CountGap, mark.At.Y - (count.Height / 2))) is { } glyphs)
        {
            context.DrawGeometry(null, new Pen(Brush(ThemeManager.BgKey), 3, lineJoin: PenLineJoin.Round), glyphs);
            context.DrawGeometry(Brush(ThemeManager.YellowKey), null, glyphs);
        }
    }

    /// <summary>
    /// Draws the map's ink for <paramref name="kind"/> without its halo, centred on <paramref name="at"/> and scaled by
    /// <paramref name="scale"/>: a solid shape, or the outline of an answer from the last search; in <paramref name="key"/>
    /// where given, otherwise in the kind's own ink.
    /// </summary>
    internal static void DrawInk(DrawingContext context, Func<string, IBrush> ink, GalaxyMapMarkKind kind, Point at, double scale, string? key = null)
    {
        if (Hollow(kind, at, scale) is { } hollow)
        {
            DrawHollow(context, key is null ? ink : _ => ink(key), hollow);
            return;
        }

        var (shape, own) = MarkInk(kind, at, scale);
        context.DrawGeometry(ink(key ?? own), null, shape);
    }

    /// <summary>The outline of an answer from the last search, and the slot filled inside a station's; null for a solid mark.</summary>
    private static (StreamGeometry Outline, StreamGeometry? Filled, double Pen)? Hollow(GalaxyMapMarkKind kind, Point at, double scale)
    {
        (double X, double Y)[] Scaled((double X, double Y)[] points) => [.. points.Select(point => (point.X * scale, point.Y * scale))];

        return kind switch
        {
            GalaxyMapMarkKind.SystemResult => (Polygon(Scaled(Triangle), at), null, HollowPen * scale),
            GalaxyMapMarkKind.StationResult => (Polygon(Scaled(Box), at), Polygon(Scaled(Slot), at), HollowPen * scale),
            GalaxyMapMarkKind.BodyResult => (Polygon(Scaled(Octagon), at), null, HollowPen * scale),
            _ => null,
        };
    }

    private static void DrawHollow(DrawingContext context, Func<string, IBrush> ink, (StreamGeometry Outline, StreamGeometry? Filled, double Pen) hollow)
    {
        context.DrawGeometry(null, new Pen(ink(ThemeManager.AKey), hollow.Pen), hollow.Outline);

        if (hollow.Filled is { } filled)
        {
            context.DrawGeometry(ink(ThemeManager.AKey), null, filled);
        }
    }

    /// <summary>The map's solid ink for <paramref name="kind"/> without its halo, centred on <paramref name="at"/> and scaled by <paramref name="scale"/>, and the key of its colour.</summary>
    private static (StreamGeometry Ink, string Key) MarkInk(GalaxyMapMarkKind kind, Point at, double scale)
    {
        var (points, key) = kind switch
        {
            GalaxyMapMarkKind.Engineer => (Cross, ThemeManager.WhiteKey),
            GalaxyMapMarkKind.Bookmark => (Ribbon, ThemeManager.CyanKey),
            GalaxyMapMarkKind.Carrier => (Hull, ThemeManager.YellowKey),
            GalaxyMapMarkKind.GuardianBeacon => (Pylon, ThemeManager.AKey),
            GalaxyMapMarkKind.FarmingSite => (Hex, ThemeManager.AKey),
            GalaxyMapMarkKind.Mission => (Flag, ThemeManager.CyanKey),
            _ => (Chevron, ThemeManager.YellowKey),
        };

        return (Polygon([.. points.Select(point => (point.X * scale, point.Y * scale))], at), key);
    }

    private static StreamGeometry Polygon((double X, double Y)[] points, Point at)
    {
        var geometry = new StreamGeometry();

        using var open = geometry.Open();

        open.BeginFigure(new Point(at.X + points[0].X, at.Y + points[0].Y), true);

        foreach (var (x, y) in points.Skip(1))
        {
            open.LineTo(new Point(at.X + x, at.Y + y));
        }

        open.EndFigure(true);

        return geometry;
    }

    /// <summary>Four white corner brackets with 6 px legs on <paramref name="box"/>, on a wider Bg halo.</summary>
    private void Brackets(DrawingContext context, Rect box)
    {
        var geometry = new StreamGeometry();

        using (var open = geometry.Open())
        {
            foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                var x = sx < 0 ? box.Left : box.Right;
                var y = sy < 0 ? box.Top : box.Bottom;

                open.BeginFigure(new Point(x, y - (sy * BracketLeg)), false);
                open.LineTo(new Point(x, y));
                open.LineTo(new Point(x - (sx * BracketLeg), y));
                open.EndFigure(false);
            }
        }

        context.DrawGeometry(null, new Pen(Brush(ThemeManager.BgKey), 3.5, lineCap: PenLineCap.Square), geometry);
        context.DrawGeometry(null, new Pen(Brush(ThemeManager.WhiteKey), 1.5, lineCap: PenLineCap.Square), geometry);
    }

    /// <summary>The selected system's name beside its brackets, on the left near the right edge, kept inside the map.</summary>
    private void SelectedName(DrawingContext context, string name, Rect box, Size size)
    {
        var text = Text(name.ToUpperInvariant(), 12, ThemeManager.AKey, 0.72);
        var x = box.Right + BracketGap;

        if (x + text.Width > size.Width - BracketGap)
        {
            x = box.Left - BracketGap - text.Width;
        }

        var y = Math.Clamp(box.Center.Y - (text.Height / 2), EdgeMargin, Math.Max(EdgeMargin, size.Height - EdgeMargin - text.Height));
        text.Draw(context, new Point(x, y));
    }

    /// <summary>Draws a system name beside its mark unless it would overlap one already drawn; returns its box.</summary>
    private Rect DrawName(DrawingContext context, string name, Point at, string key, Size size, List<Rect> taken)
    {
        var text = Text(name.ToUpperInvariant(), 12, key, 0.72);
        var x = at.X + NameOffset;

        if (x + text.Width > size.Width - EdgeMargin)
        {
            x = at.X - NameOffset - text.Width;
        }

        var box = new Rect(x, at.Y - (text.Height / 2), text.Width, text.Height);

        if (!taken.Any(other => other.Intersects(box)))
        {
            text.Draw(context, box.TopLeft);
        }

        return box;
    }

    /// <summary>A bracket one pan step long, a quarter of the view, with its length above it.</summary>
    private void ScaleBar(DrawingContext context, Size size)
    {
        var length = Math.Min(size.Width, size.Height) / 4;
        var bottom = size.Height - ScaleInset - BottomInset;
        var left = ScaleInset;
        var pen = new Pen(Brush(ThemeManager.GreyKey), 1);

        var geometry = new StreamGeometry();

        using (var open = geometry.Open())
        {
            open.BeginFigure(new Point(left + 0.5, bottom - 5.5), false);
            open.LineTo(new Point(left + 0.5, bottom));
            open.LineTo(new Point(left + length - 0.5, bottom));
            open.LineTo(new Point(left + length - 0.5, bottom - 5.5));
            open.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);

        var label = Text(ScaleWords(_viewport.LightYearsAcross), 11, ThemeManager.GreyKey, 0, Fonts.MonoFamily);
        label.Draw(context, new Point(left, bottom - 5.5 - 3 - label.Height));
    }

    /// <summary>The scale bar's label for a view this many light years across.</summary>
    internal static string ScaleWords(double across) =>
        $"{(across / 4).ToString("N0", CultureInfo.InvariantCulture)} LY";

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (PanelView.OnHeadset(this) || e.Delta.Y == 0)
        {
            return;
        }

        e.Handled = true;
        Move(e.Delta.Y > 0 ? _viewport.ZoomIn() : _viewport.ZoomOut());
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var properties = e.GetCurrentPoint(this).Properties;

        _pressedButton = properties.IsLeftButtonPressed ? MouseButton.Left
            : properties.IsRightButtonPressed ? MouseButton.Right
            : properties.IsMiddleButtonPressed ? MouseButton.Middle
            : MouseButton.None;

        if (_pressedButton == MouseButton.None)
        {
            return;
        }

        _pressedAt = e.GetPosition(this);
        _pressedView = _viewport;
        _dragging = false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (PanelView.OnHeadset(this) || _pressedAt is not { } from || _pressedView is not { } start)
        {
            return;
        }

        var at = e.GetPosition(this);
        var moved = at - from;

        if (!_dragging && Math.Sqrt((moved.X * moved.X) + (moved.Y * moved.Y)) < DragStart)
        {
            return;
        }

        if (!_dragging)
        {
            _dragging = true;
            e.Pointer.Capture(this);
        }

        Move(_pressedButton == MouseButton.Left
            ? start.Rotated(moved.X * GalaxyViewport.DegreesPerPixel, -moved.Y * GalaxyViewport.DegreesPerPixel)
            : start.Dragged(moved.X, moved.Y, Bounds.Width, Bounds.Height));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_dragging)
        {
            e.Pointer.Capture(null);
        }

        var tapped = !_dragging && _pressedAt is { } from && Distance(from, e.GetPosition(this)) < DragStart;

        _pressedAt = null;
        _pressedView = null;
        _pressedButton = MouseButton.None;
        _dragging = false;

        if (tapped && e.InitialPressMouseButton == MouseButton.Left)
        {
            Pick(e.GetPosition(this));
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        _pressedAt = null;
        _pressedView = null;
        _pressedButton = MouseButton.None;
        _dragging = false;
    }

    private void Move(GalaxyViewport to)
    {
        if (to == _viewport)
        {
            return;
        }

        View(to);
        ViewportChanged?.Invoke(to);
    }

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private TextLayout Text(string text, double size, string key, double spacing, string family = Fonts.ChromeFamily) =>
        new(
            text,
            new Typeface(new FontFamily(family), FontStyle.Normal, FontWeight.Medium),
            size,
            Brush(key),
            letterSpacing: spacing);

    private IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var found) && found is IBrush brush
            ? brush
            : Brushes.Transparent;
}
