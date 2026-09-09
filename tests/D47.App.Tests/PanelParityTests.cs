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
/// The claim behind "one widget tree renders to both surfaces": the desktop window and the VR overlay
/// each instantiate <see cref="PanelView"/>, both bind to one <see cref="PanelViewModel"/>, and neither
/// can therefore be showing something the other is not.
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

        // Rendered rather than merely constructed, because the claim is about what the two surfaces show and
        // an unlaid-out view shows nothing at all.
        Render(offscreen, 1024, 640);

        Assert.Equal(
            Text(((Control)inAWindow.Content!).GetLogicalDescendants()),
            Text(offscreen.GetLogicalDescendants()));

        Assert.Contains("Fixture One, docked.", Text(offscreen.GetLogicalDescendants()));

        inAWindow.Close();
    }

    /// <summary>
    /// The essential half of minimise-safety: the panel renders — text, fonts, layout, borders and
    /// all — with nothing shown on the desktop.
    /// </summary>
    [AvaloniaFact]
    public void ThePanelRendersWithNothingShownOnTheDesktop()
    {
        var model = new PanelViewModel();
        model.Append("Rendered by a surface with nothing on screen to show for it.");
        model.TurnLine = "routed: keyword";

        var view = Bind(model);
        var frame = Render(view, 1024, 640);

        // Nothing was shown.
        Assert.False(((Window)TopLevel.GetTopLevel(view)!).IsVisible);

        Assert.NotNull(frame);
        frame.Save(
            Path.Combine(TestSurface.CaptureDirectory, "vr-panel-full.png"),
            new PngBitmapEncoderOptions());
    }

    /// <summary>Mini is a mode of the same panel, not a second surface and not a scaled-down copy.</summary>
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

        // The banner itself stays "visible" in mini: its own visibility is about whether there is an error,
        // and the region's is about the mode.
        Assert.True(Named(view, "ErrorBanner").IsVisible);

        view.Mode = PanelMode.Mini;

        // Resized as well as re-rendered, because mini is a smaller image and not the same image hung nearer:
        // apparent text size in a headset is pixels and metres together.
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

    /// <summary>The offscreen path the VR overlay uses, and the same class it uses.</summary>
    private static RenderTargetBitmap Render(Control view, int width, int height)
    {
        var surface = new OffscreenSurface(view, new PixelSize(width, height));
        return surface.Render();
    }

    private static string Text(IEnumerable<ILogical> tree) =>
        string.Join(
            "\n",
            tree.OfType<SelectableTextBlock>().Where(block => block.IsVisible).Select(Shown));

    /// <summary>What a block is showing, whether it was given a string or runs.</summary>
    internal static string Shown(SelectableTextBlock block) =>
        block.Inlines is { Count: > 0 } inlines
            ? string.Concat(inlines.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text))
            : block.Text ?? string.Empty;
}

/// <summary> The headset's copy of the panel, over time rather than in one frame. </summary>
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

    /// <summary>A line that was already showing does not vanish when the next one arrives: rebuilt runs that are never laid out leave the page empty rather than stale.</summary>
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
        model.Append("[12:00:00] Microphone open, listening.\n");

        var view = new PanelView { DataContext = model };
        using var surface = new OffscreenSurface(view, Quad);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var conversation = Frame(surface);

        view.Page = TranscriptPage.Log;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var log = Frame(surface);

        // The log file is a different thing from the conversation, so it cannot be the same picture.
        Assert.NotEqual(conversation, log);

        // And the journal is a third shape again: a list and the fields beside it, not runs through the
        // shared scroller.
        view.Page = TranscriptPage.Journal;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(log, Frame(surface));
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
