using Avalonia;
using Avalonia.Controls;
using Shape = Avalonia.Controls.Shapes.Path;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using D47.App.Theming;
using Avalonia.LogicalTree;
using D47.App.Panel;
using D47.Core.Storage;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The ship AI's face.</summary>
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
        // One widget tree renders to both, so a face the window has and the headset does not would be exactly
        // the parity that item exists to protect.
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
        // Told apart at a glance and in greyscale — colour is never the only signal, so the geometry has to
        // differ as well as the brush.
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

        // Distinctness is asserted against the path data rather than the parsed geometry: Geometry.ToString
        // gives the type name, not the outline, so comparing parsed geometries compares eight identical
        // strings and passes while proving nothing.
        var drawn = Enum.GetValues<LoopState>().Select(AvatarView.PathFor).ToArray();

        Assert.Equal(drawn.Length, drawn.Distinct(StringComparer.Ordinal).Count());
    }

    [AvaloniaFact]
    public void TheFaceTakesItsColourFromTheThemeRatherThanAFallback()
    {
        // The first render capture of this showed a grey face: the colour was resolved with a one-time
        // TryFindResource in the constructor, against a control not yet attached to a tree, and it fell back
        // to grey without failing anything.
        var model = new PanelViewModel { LoopState = LoopState.Speaking };
        var view = Bind(model);

        using var surface = new OffscreenSurface(view, new PixelSize(1024, 640));
        surface.Render();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var core = view.GetLogicalDescendants().OfType<AvatarView>().Single()
            .GetLogicalDescendants().OfType<Shape>().Single();

        var accent = Assert.IsType<SolidColorBrush>(core.Fill, exactMatch: false);

        Assert.NotEqual(Colors.Gray, accent.Color);
        Assert.Equal(Palettes.Elite.A, accent.Color);
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
        frame.SaveCapture("panel-avatar.png");
    }
}

/// <summary>The Commander's own frames, resolved per state.</summary>
public class AvatarLibraryTests
{
    private readonly MemoryFileSystem _files = new();

    private readonly AppPaths _paths = new("C:/d47-test/avatar");

    private void Drop(LoopState state, params string[] names)
    {
        var folder = AvatarLibrary.FolderFor(_paths, state);

        foreach (var name in names)
        {
            _files.WriteText(Path.Combine(folder, name), "frame");
        }
    }

    [Fact]
    public void NothingDroppedInMeansNothingToOverride()
    {
        // The overwhelmingly common case.
        var library = AvatarLibrary.Load(_files, _paths);

        Assert.False(library.Any);
        Assert.All(Enum.GetValues<LoopState>(), state => Assert.Empty(library.For(state)));
    }

    [Fact]
    public void ReplacingOneStateLeavesTheOthersAlone()
    {
        // Per-state and all-or-nothing, the same shape the audio cues use.
        Drop(LoopState.Thinking, "01.png", "02.png");

        var library = AvatarLibrary.Load(_files, _paths);

        Assert.True(library.Any);
        Assert.Equal([LoopState.Thinking], library.Replaced);
        Assert.Equal(2, library.For(LoopState.Thinking).Count);
        Assert.Empty(library.For(LoopState.Idle));
    }

    [Fact]
    public void FramesComeBackInFilenameOrderSoAnAnimationDoesNotReshuffle()
    {
        Drop(LoopState.Speaking, "03.png", "01.png", "02.png");

        var frames = AvatarLibrary.Load(_files, _paths).For(LoopState.Speaking);

        Assert.Equal(
            ["01.png", "02.png", "03.png"],
            frames.Select(Path.GetFileName));
    }

    [Fact]
    public void AnEmptyOrUnknownFileIsNotOffered()
    {
        // Readability is checked here; decoding is not.
        var folder = AvatarLibrary.FolderFor(_paths, LoopState.Idle);

        _files.WriteText(Path.Combine(folder, "empty.png"), string.Empty);
        _files.WriteText(Path.Combine(folder, "README.txt"), "put your frames here");
        _files.WriteText(Path.Combine(folder, "good.png"), "frame");

        var frames = AvatarLibrary.Load(_files, _paths).For(LoopState.Idle);

        Assert.Equal(["good.png"], frames.Select(Path.GetFileName));
    }

    [Fact]
    public void NoSvgOrGifIsOffered()
    {
        // Avalonia decodes neither without a package, and offering an extension that silently never renders
        // is worse than not offering it.
        Assert.DoesNotContain(".svg", AvatarLibrary.Extensions);
        Assert.DoesNotContain(".gif", AvatarLibrary.Extensions);
    }
}
