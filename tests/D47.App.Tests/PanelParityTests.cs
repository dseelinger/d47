using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
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
    public void ThePanelRendersWithNothingShownOnTheDesktop()
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

        var rows = new[] { "Header", "AskRow", "Banners" }
            .Select(name => Named(view, name))
            .ToArray();

        Assert.All(rows, row => Assert.True(row.IsVisible));

        // The banner itself stays "visible" in mini: its own visibility is about whether there
        // is an error, and the region's is about the mode. Two reasons, two properties.
        Assert.True(Named(view, "ErrorBanner").IsVisible);

        view.Mode = PanelMode.Mini;

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
            tree.OfType<SelectableTextBlock>().Where(block => block.IsVisible).Select(Shown));

    /// <summary>
    /// What a block is showing, whether it was given a string or runs. The transcript is written
    /// as runs — a marked line is drawn differently from the conversation around it — and a
    /// block using them leaves <c>Text</c> null.
    /// </summary>
    internal static string Shown(SelectableTextBlock block) =>
        block.Inlines is { Count: > 0 } inlines
            ? string.Concat(inlines.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text))
            : block.Text ?? string.Empty;
}

/// <summary>
/// The headset's copy of the panel, over time rather than in one frame
/// (remediation.md, "All tabs should update in the VR big panel").
/// <para>
/// Every other test of this surface renders it once. That is the one case that worked: the view
/// drew whatever its model held when its data context was set, and nothing after it — so the
/// panel in the headset was a photograph of the moment the session started, under a tab strip
/// that still lit up when the page changed.
/// </para>
/// </summary>
public class TheVrPanelKeepsUpTests
{
    private static readonly PixelSize Quad = new(1024, 640);

    [AvaloniaFact]
    public void ALineAppendedAfterTheFirstFrameIsDrawn()
    {
        var model = new PanelViewModel();
        var view = new PanelView { DataContext = model };
        using var surface = new OffscreenSurface(view, Quad);

        var blank = Frame(surface);

        model.Append("Fixture One, docked.");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var written = Frame(surface);
        Assert.NotEqual(blank, written);

        model.Append(" Cleared to depart.");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(written, Frame(surface));
    }

    /// <summary>
    /// And a line that was already showing does not vanish when the next one arrives, which is
    /// the shape the failure actually took: the rebuilt runs were never laid out, so the page
    /// went empty rather than stale.
    /// </summary>
    [AvaloniaFact]
    public void WhatWasShowingIsStillShowingAfterTheNextAppend()
    {
        var model = new PanelViewModel();
        model.Append("Fixture Two, in supercruise.");

        var view = new PanelView { DataContext = model };
        using var surface = new OffscreenSurface(view, Quad);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var showing = Frame(surface);

        var blankModel = new PanelViewModel();
        using var blankSurface = new OffscreenSurface(new PanelView { DataContext = blankModel }, Quad);
        var blank = Frame(blankSurface);

        Assert.NotEqual(blank, showing);

        model.Append(" And a second line.");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var after = Frame(surface);

        Assert.NotEqual(showing, after);
        Assert.NotEqual(blank, after);
    }

    /// <summary>Each page draws its own content, and changing pages redraws.</summary>
    [AvaloniaFact]
    public void EveryPageDrawsWhatItHolds()
    {
        var model = new PanelViewModel();
        model.Append("The Commander and the ship's AI.");
        model.Append("[12:00:00] Microphone open, listening.\n", TranscriptKind.Technical);

        var view = new PanelView { DataContext = model };
        using var surface = new OffscreenSurface(view, Quad);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var conversation = Frame(surface);

        view.Page = TranscriptPage.Technical;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var technical = Frame(surface);

        // Technical is the same runs with the diagnostics left in, so it cannot be the same
        // picture as the conversation when there is a diagnostic to leave in.
        Assert.NotEqual(conversation, technical);

        // And a line appended while Technical is showing lands on it.
        model.Append("[12:00:01] Speaking the answer.\n", TranscriptKind.Technical);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(technical, Frame(surface));
    }

    /// <summary>The rendered frame, as something two of them can be compared by.</summary>
    private static string Frame(OffscreenSurface surface)
    {
        var rendered = surface.Render();
        var size = rendered.PixelSize;
        var stride = size.Width * 4;
        var pixels = new byte[stride * size.Height];

        unsafe
        {
            fixed (byte* buffer = pixels)
            {
                rendered.CopyPixels(
                    new PixelRect(0, 0, size.Width, size.Height), (IntPtr)buffer, pixels.Length, stride);
            }
        }

        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels));
    }
}
