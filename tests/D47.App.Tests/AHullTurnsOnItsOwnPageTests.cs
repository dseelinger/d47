using System.Numerics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Hulls;
using D47.Core.Interface;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A hull with a mesh is turned on its own page, in place of the 4K still (#625).</summary>
public class AHullTurnsOnItsOwnPageTests
{
    private static readonly GuiColourMatrix Blue = new(0x1A / 255.0, 0, 0, 0, 1, 0, 0, 0, 255.0 / 0x1A);

    /// <summary>A cube two metres across, written where the app reads a hull's mesh.</summary>
    private static void WriteCube(string path)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<int>();

        foreach (var n in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ })
        {
            var u = MathF.Abs(n.Y) > 0.5f ? Vector3.UnitX : Vector3.UnitY;
            var v = Vector3.Cross(n, u);
            var first = positions.Count;

            foreach (var (su, sv) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                positions.Add(n + (u * su) + (v * sv));
                normals.Add(n);
            }

            indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
        }

        var mesh = new HullMesh(
            [.. positions], [.. normals], [.. indices], [new HullPart("hull", 0, 12, false)], MathF.Sqrt(3),
            Vector3.Normalize(new Vector3(1, 0.6f, 1.4f)));

        using var file = File.Create(path);
        mesh.Write(file);
    }

    private static (Window Window, PanelView Panel) Page(
        bool mesh = true, bool pictures = true, bool headset = false, string themeId = ThemeCatalog.Elite)
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-hull-viewer"));

        paths.EnsureCreated();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ships", "corsair.4k.png"), Path.Combine(paths.Ships, "corsair.4k.png"));

        if (mesh)
        {
            WriteCube(Path.Combine(paths.Ships, "corsair.mesh"));
        }

        ShipArt.Shipped = null;
        ShipArt.Folder = paths.Ships;
        HullTurntable.Stop();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        ships.BuildFor(12, "Corsair", "Reaper");

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableLoadout(ships, checklists, () => null, null, null, () => pictures);

        var window = new Window { Content = panel, Width = 1400, Height = 860 };

        window.Show();
        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();

        panel.GetVisualDescendants()
            .OfType<Button>()
            .First(button => button.GetVisualDescendants()
                .OfType<TextBlock>()
                .Any(block => (block.Text ?? string.Empty).Contains("Reaper", StringComparison.OrdinalIgnoreCase)))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Frame();

        return (window, panel);
    }

    /// <summary>Runs the queued work and one frame, which is when a viewer draws.</summary>
    private static void Frame()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static HullViewer Viewer(Visual root) => root.GetVisualDescendants().OfType<HullViewer>().Single();

    private static HullPose Pose(HullViewer viewer) => viewer.Pose;

    private static Button Named(Visual root, string name) =>
        root.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == name);

    private static void Key(Control target, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers, Source = target });
        Frame();
    }

    [AvaloniaFact]
    public void AHullWithAMeshIsTurnedInPlaceOfItsStill()
    {
        var (window, panel) = Page();

        var picture = panel.GetVisualDescendants().OfType<HullPicture>().Single();

        Assert.Single(picture.GetVisualDescendants().OfType<HullViewer>());
        Assert.DoesNotContain(picture.GetVisualDescendants().OfType<Image>(), image => image.Source is Bitmap);
        Assert.DoesNotContain(picture.GetVisualDescendants().OfType<Button>(), button => button.Content as string == "ZOOM");
        Named(picture, "Reset the view");
        Named(picture, "Whole window");
        Assert.Contains(picture.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == HullMarks.Hints);
        Assert.Contains(picture.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "LIGHT");

        window.Close();
    }

    [AvaloniaFact]
    public void HullPicturesOffShowsNoViewer()
    {
        var (window, panel) = Page(pictures: false);

        Assert.Empty(panel.GetVisualDescendants().OfType<HullViewer>());
        Assert.Empty(panel.GetVisualDescendants().OfType<HullPicture>());

        window.Close();
    }

    [AvaloniaFact]
    public void AHullWithNoMeshKeepsItsStill()
    {
        var (window, panel) = Page(mesh: false);

        Assert.Empty(panel.GetVisualDescendants().OfType<HullViewer>());
        Assert.Contains(
            panel.GetVisualDescendants().OfType<HullPicture>().Single().GetVisualDescendants().OfType<Image>(),
            image => image.Source is Bitmap);

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetPanelKeepsTheStill()
    {
        var (window, panel) = Page(headset: true);

        Assert.Empty(panel.GetVisualDescendants().OfType<HullViewer>());
        Assert.Contains(
            panel.GetVisualDescendants().OfType<HullPicture>().Single().GetVisualDescendants().OfType<Image>(),
            image => image.Source is Bitmap);

        window.Close();
    }

    [AvaloniaFact]
    public void TheKeyboardTurnsRollsPansZoomsAndRests()
    {
        var (window, panel) = Page();
        var viewer = Viewer(panel);
        var pose = Pose(viewer);
        var rest = pose.Camera;

        viewer.Focus();

        Key(viewer, Avalonia.Input.Key.Right);
        var turned = pose.Camera;
        Assert.NotEqual(rest.Orbit, turned.Orbit);

        Key(viewer, Avalonia.Input.Key.Up);
        Assert.NotEqual(turned.Orbit, pose.Camera.Orbit);

        var before = pose.Camera;
        Key(viewer, Avalonia.Input.Key.Left, KeyModifiers.Shift);
        var rolled = pose.Camera;
        Assert.NotEqual(before.Orbit, rolled.Orbit);

        Key(viewer, Avalonia.Input.Key.Right, KeyModifiers.Control);
        Assert.NotEqual(Vector3.Zero, pose.Camera.Target);
        Assert.Equal(rolled.Orbit, pose.Camera.Orbit);

        var distance = pose.Camera.Distance;
        Key(viewer, Avalonia.Input.Key.PageUp);
        Assert.True(pose.Camera.Distance < distance);
        Key(viewer, Avalonia.Input.Key.OemMinus);
        Key(viewer, Avalonia.Input.Key.OemMinus);
        Assert.True(pose.Camera.Distance > distance);

        Key(viewer, Avalonia.Input.Key.Home);
        Assert.Equal(rest, pose.Camera);

        window.Close();
    }

    [AvaloniaFact]
    public void TheMouseTurnsRollsPansZoomsAndADoubleClickRests()
    {
        var (window, panel) = Page();
        var viewer = Viewer(panel);
        var pose = Pose(viewer);
        var rest = pose.Camera;
        var centre = viewer.TranslatePoint(new Point(viewer.Bounds.Width / 2, viewer.Bounds.Height / 2), window)!.Value;

        void Drag(MouseButton button, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            window.MouseDown(centre, button, modifiers);
            window.MouseMove(centre + new Point(40, 25), modifiers | Held(button));
            window.MouseUp(centre + new Point(40, 25), button, modifiers);
            Frame();
        }

        Drag(MouseButton.Left);
        Assert.NotEqual(rest.Orbit, pose.Camera.Orbit);
        Assert.Equal(Vector3.Zero, pose.Camera.Target);

        var turned = pose.Camera;
        Drag(MouseButton.Left, RawInputModifiers.Shift);
        Assert.NotEqual(turned.Orbit, pose.Camera.Orbit);

        Drag(MouseButton.Right);
        Assert.NotEqual(Vector3.Zero, pose.Camera.Target);

        var distance = pose.Camera.Distance;
        window.MouseWheel(centre, new Avalonia.Vector(0, 1));
        Frame();
        Assert.True(pose.Camera.Distance < distance);

        // Clicking turns nothing and opens nothing: only the □ mark expands the viewer.
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        Frame();
        Assert.Equal(rest, pose.Camera);
        Assert.Empty(window.GetVisualDescendants().OfType<HullViewerFull>());

        window.Close();
    }

    private static RawInputModifiers Held(MouseButton button) => button switch
    {
        MouseButton.Left => RawInputModifiers.LeftMouseButton,
        MouseButton.Right => RawInputModifiers.RightMouseButton,
        _ => RawInputModifiers.MiddleMouseButton,
    };

    [AvaloniaFact]
    public void TheLightLevelRelightsTheHull()
    {
        var (window, panel) = Page();
        var pose = Pose(Viewer(panel));
        var level = panel.GetVisualDescendants().OfType<D47.App.Controls.Level>().Single(l => AutomationProperties.GetName(l) == "Light");

        Assert.Equal(1, level.Value, 3);

        level.Focus();
        level.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Right, Source = level });
        Frame();

        Assert.Equal(1.1f, pose.Camera.Light, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void TheWholeWindowOpensAtThePagesPoseAndEscapeKeepsIt()
    {
        using var look = AppLook.Put();

        var (window, panel) = Page();
        var viewer = Viewer(panel);
        var pose = Pose(viewer);

        viewer.Focus();
        Key(viewer, Avalonia.Input.Key.Right);
        var page = pose.Camera;

        Named(panel, "Whole window").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Frame();

        var full = window.GetVisualDescendants().OfType<HullViewerFull>().Single();
        var wide = full.GetVisualDescendants().OfType<HullViewer>().Single();

        Assert.Same(pose, Pose(wide));
        Assert.Equal(page, pose.Camera);
        Assert.True(wide.IsFocused);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("hull-viewer-whole-window.png");
        }

        Key(wide, Avalonia.Input.Key.Down);
        var turned = pose.Camera;
        Assert.NotEqual(page, turned);

        Key(wide, Avalonia.Input.Key.Escape);

        Assert.Empty(window.GetVisualDescendants().OfType<HullViewerFull>());
        Assert.Equal(turned, Pose(Viewer(panel)).Camera);

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    [InlineData(ThemeCatalog.ElitePaletteId)]
    public void TheShipPageIsCapturedWithItsViewerAtRest(string themeId)
    {
        using var look = AppLook.Put(themeId, themeId == ThemeCatalog.ElitePaletteId ? Blue : null);

        var (window, panel) = Page(themeId: themeId);
        var viewer = Viewer(panel);

        Assert.Equal(HullCamera.Rest(Pose(viewer).Mesh), Pose(viewer).Camera);

        var path = $"hull-viewer-{themeId}.png";

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture(path);
        }

        // Each face is mixed from the ground towards the text colour, so in every theme a lit face stands
        // further from the ground than an unlit one.
        static Color Of(string key) => ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;
        static uint Packed(Color c) => ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
        static int Distance(uint a, uint b) =>
            Math.Abs((int)(a & 0xFF) - (int)(b & 0xFF))
            + Math.Abs((int)((a >> 8) & 0xFF) - (int)((b >> 8) & 0xFF))
            + Math.Abs((int)((a >> 16) & 0xFF) - (int)((b >> 16) & 0xFF));

        var shade = new HullShade(Packed(Of(ThemeManager.WhiteKey)), Packed(Of(ThemeManager.BgKey)));
        var lit = HullRasteriser.Mix(shade, HullRasteriser.Lighting(1f, 1f));
        var unlit = HullRasteriser.Mix(shade, HullRasteriser.Lighting(0f, 1f));

        Assert.True(Distance(lit, shade.Background) > Distance(unlit, shade.Background), themeId);

        // And the drawn hull shows that spread: at least two shades of hull besides the ground.
        var drawnShades = DrawnShades(viewer);

        Assert.True(drawnShades.Count(c => c != shade.Background) >= 2, $"{themeId}: {drawnShades.Count} shades");
        Assert.Contains(shade.Background, drawnShades);
        Assert.True(drawnShades.Max(c => Distance(c, shade.Background)) > Distance(unlit, shade.Background), themeId);

        window.Close();
    }

    /// <summary>The distinct colours in the viewer's own bitmap.</summary>
    private static HashSet<uint> DrawnShades(HullViewer viewer)
    {
        var bitmap = viewer.Frame ?? throw new InvalidOperationException("The viewer has not drawn.");

        using var frame = bitmap.Lock();
        var row = new int[bitmap.PixelSize.Width];
        var shades = new HashSet<uint>();

        for (var y = 0; y < bitmap.PixelSize.Height; y++)
        {
            System.Runtime.InteropServices.Marshal.Copy(frame.Address + (y * frame.RowBytes), row, 0, row.Length);

            foreach (var pixel in row)
            {
                shades.Add((uint)pixel);
            }
        }

        return shades;
    }
}
