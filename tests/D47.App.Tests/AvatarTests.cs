using Avalonia;
using Avalonia.Controls;
using Shape = Avalonia.Controls.Shapes.Path;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using D47.App.Theming;
using Avalonia.LogicalTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The ship AI's face (list.md Phase 11, "Ship's AI Avatar").
/// <para>
/// Rendered rather than merely constructed, for the reason the panel parity tests already give:
/// an unlaid-out view shows nothing at all, and that is exactly how a blank surface gets as far
/// as it did once before.
/// </para>
/// </summary>
public class AvatarTests
{
    private static PanelView Bind(PanelViewModel model)
    {
        new Theming.ThemeManager(Application.Current!, NullLogger<Theming.ThemeManager>.Instance)
            .Apply(ThemeCatalog.Elite);

        return new PanelView { DataContext = model };
    }

    [AvaloniaFact]
    public void TheFaceIsOnBothSurfacesAndFollowsTheLoop()
    {
        // One widget tree renders to both, so a face the window has and the headset does not
        // would be exactly the parity that item exists to protect.
        var model = new PanelViewModel();

        var window = new Window { Width = 820, Height = 640, Content = Bind(model) };
        window.Show();

        var offscreen = Bind(model);
        using var surface = new OffscreenSurface(offscreen, new PixelSize(1024, 640));
        surface.Render();

        model.LoopState = LoopState.Thinking;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        foreach (var view in new[] { (PanelView)window.Content!, offscreen })
        {
            var avatar = view.GetLogicalDescendants().OfType<AvatarView>().Single();

            Assert.True(avatar.IsVisible);
            Assert.Contains(avatar.GetLogicalDescendants().OfType<Shape>(), path => path.IsVisible);
        }

        window.Close();
    }

    [AvaloniaFact]
    public void EveryStateDrawsSomethingAndTheyAreNotAllTheSame()
    {
        // Told apart at a glance and in greyscale — colour is never the only signal, so the
        // geometry has to differ as well as the brush.
        var model = new PanelViewModel();
        var view = Bind(model);

        using var surface = new OffscreenSurface(view, new PixelSize(1024, 640));
        surface.Render();

        var avatar = view.GetLogicalDescendants().OfType<AvatarView>().Single();

        foreach (var state in Enum.GetValues<LoopState>())
        {
            model.LoopState = state;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            surface.Render();

            var core = avatar.GetLogicalDescendants().OfType<Shape>().Single();
            Assert.NotNull(core.Data);
        }

        // Distinctness is asserted against the path data rather than the parsed geometry:
        // Geometry.ToString gives the type name, not the outline, so comparing parsed
        // geometries compares eight identical strings and passes while proving nothing.
        var drawn = Enum.GetValues<LoopState>().Select(AvatarView.PathFor).ToArray();

        Assert.Equal(drawn.Length, drawn.Distinct(StringComparer.Ordinal).Count());
    }

    [AvaloniaFact]
    public void TheFaceTakesItsColourFromTheThemeRatherThanAFallback()
    {
        // The first render capture of this showed a grey face: the colour was resolved with a
        // one-time TryFindResource in the constructor, against a control not yet attached to a
        // tree, and it fell back to grey without failing anything. Asserting the actual brush is
        // what makes that a test failure rather than a screenshot somebody has to look at.
        var model = new PanelViewModel { LoopState = LoopState.Speaking };
        var view = Bind(model);

        using var surface = new OffscreenSurface(view, new PixelSize(1024, 640));
        surface.Render();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var core = view.GetLogicalDescendants().OfType<AvatarView>().Single()
            .GetLogicalDescendants().OfType<Shape>().Single();

        var accent = Assert.IsType<SolidColorBrush>(core.Fill, exactMatch: false);

        Assert.NotEqual(Colors.Gray, accent.Color);
        Assert.Equal(Palettes.Elite.Accent, accent.Color);
    }

    [AvaloniaFact]
    public void ThePanelStillRendersWithTheFaceOnIt()
    {
        var model = new PanelViewModel { LoopState = LoopState.Speaking };
        model.Append("Fixture Anchorage, 12.4 ly.");

        var view = Bind(model);
        using var surface = new OffscreenSurface(view, new PixelSize(1024, 640));

        var frame = surface.Render();

        Assert.NotNull(frame);
        frame.Save(
            Path.Combine(TestSurface.CaptureDirectory, "panel-avatar.png"),
            new PngBitmapEncoderOptions());
    }
}

/// <summary>
/// The Commander's own frames, resolved per state (list.md Phase 11).
/// </summary>
public class AvatarLibraryTests
{
    private static (AppPaths Paths, string Root) Install()
    {
        var root = Path.Combine(Path.GetTempPath(), "d47-avatar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        return (new AppPaths(root), root);
    }

    private static void Drop(AppPaths paths, LoopState state, params string[] names)
    {
        var folder = AvatarLibrary.FolderFor(paths, state);
        Directory.CreateDirectory(folder);

        foreach (var name in names)
        {
            File.WriteAllBytes(Path.Combine(folder, name), [1, 2, 3, 4]);
        }
    }

    [Fact]
    public void NothingDroppedInMeansNothingToOverride()
    {
        // The overwhelmingly common case. An empty data/avatar is normal, not a missing asset —
        // the panel draws its own.
        var (paths, root) = Install();

        try
        {
            var library = AvatarLibrary.Load(paths);

            Assert.False(library.Any);
            Assert.All(Enum.GetValues<LoopState>(), state => Assert.Empty(library.For(state)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReplacingOneStateLeavesTheOthersAlone()
    {
        // Per-state and all-or-nothing, the same shape the audio cues use. That granularity is
        // the whole point of the folder-per-state layout.
        var (paths, root) = Install();

        try
        {
            Drop(paths, LoopState.Thinking, "01.png", "02.png");

            var library = AvatarLibrary.Load(paths);

            Assert.True(library.Any);
            Assert.Equal([LoopState.Thinking], library.Replaced);
            Assert.Equal(2, library.For(LoopState.Thinking).Count);
            Assert.Empty(library.For(LoopState.Idle));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FramesComeBackInFilenameOrderSoAnAnimationDoesNotReshuffle()
    {
        var (paths, root) = Install();

        try
        {
            Drop(paths, LoopState.Speaking, "03.png", "01.png", "02.png");

            var frames = AvatarLibrary.Load(paths).For(LoopState.Speaking);

            Assert.Equal(
                ["01.png", "02.png", "03.png"],
                frames.Select(Path.GetFileName));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AnEmptyOrUnknownFileIsNotOffered()
    {
        // Readability is checked here; decoding is not. A zero-byte file would render as
        // nothing, and a README dropped in beside the frames is not a frame.
        var (paths, root) = Install();

        try
        {
            var folder = AvatarLibrary.FolderFor(paths, LoopState.Idle);
            Directory.CreateDirectory(folder);

            File.WriteAllBytes(Path.Combine(folder, "empty.png"), []);
            File.WriteAllText(Path.Combine(folder, "README.txt"), "put your frames here");
            File.WriteAllBytes(Path.Combine(folder, "good.png"), [1, 2, 3]);

            var frames = AvatarLibrary.Load(paths).For(LoopState.Idle);

            Assert.Equal(["good.png"], frames.Select(Path.GetFileName));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NoSvgOrGifIsOffered()
    {
        // Avalonia decodes neither without a package, and offering an extension that silently
        // never renders is worse than not offering it.
        Assert.DoesNotContain(".svg", AvatarLibrary.Extensions);
        Assert.DoesNotContain(".gif", AvatarLibrary.Extensions);
    }
}
