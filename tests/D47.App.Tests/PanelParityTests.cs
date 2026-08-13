using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.LogicalTree;
using D47.App.Panel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The claim behind "one widget tree renders to both surfaces": the desktop window and the VR
/// overlay each instantiate <see cref="PanelView"/>, both bind to one
/// <see cref="PanelViewModel"/>, and neither can therefore be showing something the other is
/// not (list.md Phase 9, "TheApp's panel works in VR").
/// <para>
/// The part worth pinning by test is the part the framework will not do for us. A
/// <c>Visual</c> belongs to exactly one visual tree, so there is no single instance rendered
/// twice — what makes the constraint hold is the shared view model, and a second view that
/// quietly stopped reading it would look completely fine until the two disagreed.
/// </para>
/// </summary>
public class PanelParityTests
{
    [AvaloniaFact]
    public void TwoViewsBoundToOneModelShowTheSameText()
    {
        var model = new PanelViewModel();

        var inAWindow = Host(model);
        var offscreen = Bind(model);

        model.Append("Fixture One, docked.");

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Rendered rather than merely constructed, because the claim is about what the two
        // surfaces show and an unlaid-out view shows nothing at all.
        Render(offscreen, 1024, 640);

        Assert.Equal(
            Text(((Control)inAWindow.Content!).GetLogicalDescendants()),
            Text(offscreen.GetLogicalDescendants()));

        Assert.Contains("Fixture One, docked.", Text(offscreen.GetLogicalDescendants()));

        inAWindow.Close();
    }

    /// <summary>
    /// The load-bearing half of minimise-safety: the panel renders — text, fonts, layout,
    /// borders and all — with nothing shown on the desktop. The surface's host window is
    /// constructed and never shown, so there is no window state for a minimise to put it in,
    /// which is what makes "keep working when the main window is minimized" structural rather
    /// than something to defend (architecture.md D1, as amended in Phase 9).
    /// </summary>
    [AvaloniaFact]
    public void ThePanelRendersWithNoWindowAndNoTopLevel()
    {
        var model = new PanelViewModel();
        model.Append("Rendered by a surface with nothing on screen to show for it.");
        model.TurnLine = "routed: keyword";

        var view = Bind(model);
        var frame = Render(view, 1024, 640);

        // Nothing was shown. The surface's host window exists so that styling and templating
        // have a root to run against, and is never shown — so there is no window state for a
        // minimise to put it in (architecture.md D1, as amended in Phase 9).
        Assert.False(((Window)TopLevel.GetTopLevel(view)!).IsVisible);

        Assert.NotNull(frame);
        frame.Save(
            Path.Combine(TestSurface.CaptureDirectory, "vr-panel-full.png"),
            new PngBitmapEncoderOptions());
    }

    /// <summary>
    /// Mini is a mode of the same panel, not a second surface and not a scaled-down copy. What
    /// that has to mean concretely is that the content set shrinks: the ask box, the gear and
    /// the banners go, and the transcript stays.
    /// </summary>
    [AvaloniaFact]
    public void MiniModeDropsContentRatherThanShrinkingTheRendering()
    {
        var model = new PanelViewModel();
        model.ErrorText = "Something to hide in mini.";
        model.Append("Fixture Anchorage, 12.4 ly.");
        model.TurnLine = "routed: keyword";

        var view = Bind(model);
        using var surface = new OffscreenSurface(view, new PixelSize(1024, 640));
        surface.Render();

        var rows = new[] { "Header", "AskRow", "ErrorBanner" }
            .Select(name => Named(view, name))
            .ToArray();

        Assert.All(rows, row => Assert.True(row.IsVisible));

        model.Mode = PanelMode.Mini;

        // Resized as well as re-rendered, because mini is a smaller image and not the same
        // image hung nearer: apparent text size in a headset is pixels and metres together.
        surface.Resize(new PixelSize(640, 280));
        var frame = surface.Render();

        Assert.All(rows, row => Assert.False(row.IsVisible));

        // Still the same panel, still bound to the same model — the transcript is untouched.
        Assert.Contains("Fixture Anchorage", model.TranscriptText, StringComparison.Ordinal);

        Assert.NotNull(frame);
        frame.Save(
            Path.Combine(TestSurface.CaptureDirectory, "vr-panel-mini.png"),
            new PngBitmapEncoderOptions());
    }

    [AvaloniaFact]
    public void TheTailIsTheLastFewLinesAndNothingElse()
    {
        var model = new PanelViewModel();
        model.Append("one\ntwo\nthree\nfour");

        Assert.Equal("two\nthree\nfour", model.Tail(3));
        Assert.Equal("one\ntwo\nthree\nfour", model.Tail(10));
    }

    private static Control Named(Control view, string name) =>
        view.GetLogicalDescendants().OfType<Control>().Single(control => control.Name == name);

    /// <summary>Applies the theme once so the captures are the shipped palette rather than Fluent's.</summary>
    private static PanelView Bind(PanelViewModel model)
    {
        new Theming.ThemeManager(Application.Current!, NullLogger<Theming.ThemeManager>.Instance)
            .Apply(D47.Core.Interface.ThemeCatalog.Elite);

        return new PanelView { DataContext = model };
    }

    private static Window Host(PanelViewModel model)
    {
        var window = new Window { Width = 820, Height = 640, Content = Bind(model) };
        window.Show();
        return window;
    }

    /// <summary>
    /// The offscreen path the VR overlay uses, and the same class it uses. Rendering these
    /// through a hand-rolled measure/arrange here would be testing something the app does not
    /// do — which is exactly how the detached-render blank got as far as it did.
    /// </summary>
    private static RenderTargetBitmap Render(Control view, int width, int height)
    {
        var surface = new OffscreenSurface(view, new PixelSize(width, height));
        return surface.Render();
    }

    private static string Text(IEnumerable<ILogical> tree) =>
        string.Join(
            "\n",
            tree.OfType<SelectableTextBlock>().Where(block => block.IsVisible).Select(block => block.Text));
}
