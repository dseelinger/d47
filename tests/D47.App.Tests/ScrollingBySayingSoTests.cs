using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Moving a page by saying so (<a href="https://github.com/dseelinger/d47/issues/34">#34</a>).
/// <para>
/// <b>Asked for the two headset panels, and by the time it was built the flat strip needed it
/// more.</b> In a headset a ray on a twelve-pixel bar is the only way to scroll; on the strip there
/// was no way at all, because the pointer goes straight through it and so does the wheel — and
/// 0.67.0 had just given that surface the checklist and the engineer pages, which are the ones with
/// more in them than 512 by 280 holds.
/// </para>
/// </summary>
public class ScrollingBySayingSoTests
{
    [AvaloniaFact]
    public void PageDownMovesTheTranscriptAndPageUpBringsItBack()
    {
        var (window, panel) = Open();

        var scroller = panel.GetControl<ScrollViewer>("TranscriptScroller");

        Assert.True(scroller.Extent.Height > scroller.Viewport.Height, "The fixture does not overflow.");

        // At the end, because the transcript follows the newest line until told otherwise.
        var bottom = scroller.Offset.Y;

        Assert.True(panel.Scroll(PanelScrollStep.PageUp));
        Dispatcher.UIThread.RunJobs();

        var up = scroller.Offset.Y;

        Assert.True(up < bottom, "Page up did not move the transcript.");

        Assert.True(panel.Scroll(PanelScrollStep.PageDown));
        Dispatcher.UIThread.RunJobs();

        Assert.True(scroller.Offset.Y > up, "Page down did not move the transcript back.");

        window.Close();
    }

    /// <summary>
    /// A nudge is smaller than a screenful, which is the whole difference between the two pairs of
    /// phrases the Commander asked for.
    /// </summary>
    [AvaloniaFact]
    public void AScrollIsSmallerThanAPage()
    {
        var (window, panel) = Open();

        var scroller = panel.GetControl<ScrollViewer>("TranscriptScroller");
        var bottom = scroller.Offset.Y;

        panel.Scroll(PanelScrollStep.LineUp);
        Dispatcher.UIThread.RunJobs();

        var nudged = bottom - scroller.Offset.Y;

        scroller.Offset = scroller.Offset.WithY(bottom);
        Dispatcher.UIThread.RunJobs();

        panel.Scroll(PanelScrollStep.PageUp);
        Dispatcher.UIThread.RunJobs();

        var paged = bottom - scroller.Offset.Y;

        Assert.True(nudged > 0, "A nudge moved nothing.");
        Assert.True(paged > nudged, $"A page ({paged}) was not further than a nudge ({nudged}).");

        window.Close();
    }

    /// <summary>
    /// <b>At the end, nothing happens and it says so.</b> A surface that answered "scrolled" while
    /// standing still would swallow the phrase, and a Commander at the bottom of a page would watch
    /// nothing and wonder whether they had been heard.
    /// </summary>
    [AvaloniaFact]
    public void AtTheEndItDeclinesRatherThanPretending()
    {
        var (window, panel) = Open();

        Assert.False(panel.Scroll(PanelScrollStep.PageDown));

        var scroller = panel.GetControl<ScrollViewer>("TranscriptScroller");

        scroller.Offset = scroller.Offset.WithY(0);
        Dispatcher.UIThread.RunJobs();

        Assert.False(panel.Scroll(PanelScrollStep.PageUp));

        window.Close();
    }

    /// <summary>A page with nothing over the fold has nothing to scroll, and says so.</summary>
    [AvaloniaFact]
    public void APageThatFitsIsNotScrolled()
    {
        var (window, panel) = Open(lines: 1);

        Assert.False(panel.Scroll(PanelScrollStep.PageDown));
        Assert.False(panel.Scroll(PanelScrollStep.PageUp));

        window.Close();
    }

    /// <summary>
    /// <b>Whichever region is showing</b>, not the transcript alone — which is the half that
    /// matters now that mini and the strip carry the checklist and the engineer pages.
    /// </summary>
    [AvaloniaFact]
    public void ItMovesTheFurnishedPageAndNotTheTranscriptBehindIt()
    {
        var (window, panel) = Open();

        var tall = new StackPanel();

        for (var line = 0; line < 200; line++)
        {
            tall.Children.Add(new TextBlock { Text = $"line {line}" });
        }

        panel.Furnish(
            PanelTab.Checklist,
            _ => new ScrollViewer { Content = tall },
            new NavCrumb("checklist", "Checklist"));

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        var transcript = panel.GetControl<ScrollViewer>("TranscriptScroller").Offset.Y;

        Assert.True(panel.Scroll(PanelScrollStep.PageDown));
        Dispatcher.UIThread.RunJobs();

        var page = panel.GetControl<Border>("PagePane")
            .GetVisualDescendants()
            .OfType<ScrollViewer>()
            .First();

        Assert.True(page.Offset.Y > 0, "The furnished page did not move.");

        // And the transcript stayed where it was, because it is not what is being read.
        Assert.Equal(transcript, panel.GetControl<ScrollViewer>("TranscriptScroller").Offset.Y);

        window.Close();
    }

    /// <summary>
    /// Scrolling up by voice means what scrolling up by hand means: <b>stop following the newest
    /// line</b>. Suppressing that would give the Commander a page that jumps back to the bottom on
    /// the next thing d47 said.
    /// </summary>
    [AvaloniaFact]
    public void ScrollingUpStopsTheTranscriptFollowing()
    {
        var (window, panel) = Open();

        var scroller = panel.GetControl<ScrollViewer>("TranscriptScroller");
        var model = (PanelViewModel)panel.DataContext!;

        panel.Scroll(PanelScrollStep.PageUp);
        Dispatcher.UIThread.RunJobs();

        var parked = scroller.Offset.Y;

        model.Append("\nAnd another line arrives while they are reading further up.");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(parked, scroller.Offset.Y);

        window.Close();
    }

    /// <summary>
    /// <b>The headset, through what it actually draws.</b> This is the surface the request was made
    /// from, and the assertion has to be pixels rather than an offset: a scroll that moved the
    /// viewer and did not mark the surface dirty is a page that has moved and a Commander still
    /// looking at the frame before it.
    /// </summary>
    [AvaloniaFact]
    public void TheHeadsetPanelScrollsAndRedraws()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var (settings, _, _) = TestSurface.Create();
        var model = new PanelViewModel();

        for (var line = 0; line < 200; line++)
        {
            model.Append($"Line {line}: longer than the quad it is on.");
        }

        using var headset = new D47.App.Headset.VrPanelSurface(model, settings, _ => null);

        headset.ApplyMode();

        var before = Pixels(headset);

        Assert.True(headset.Scroll(PanelScrollStep.PageUp), "The headset panel declined to scroll.");

        var after = Pixels(headset);

        Assert.NotEqual(before, after);
    }

    /// <summary>
    /// <b>The strip, which had no other way at all.</b> The pointer goes straight through it, so
    /// the wheel does too — and it carries the checklist and the engineer pages now.
    /// </summary>
    [AvaloniaFact]
    public void TheFlatStripScrollsAndDeclinesWhenItIsNotOnScreen()
    {
        var (settings, viewState, _) = TestSurface.Create();
        var model = new PanelViewModel();

        for (var line = 0; line < 200; line++)
        {
            model.Append($"Line {line}: longer than 512 by 280 holds.");
        }

        var strip = new D47.App.Windowing.OverlayPanel(
            model, settings, viewState, NullLogger<D47.App.Windowing.OverlayPanel>.Instance);

        // Hidden, it declines: a phrase must not be swallowed by a surface nobody can see, or the
        // Commander gets silence from the one that was showing too.
        Assert.False(strip.Scroll(PanelScrollStep.PageUp));

        strip.Show();
        Dispatcher.UIThread.RunJobs();

        model.Append("And the newest line, with somewhere to put it.");
        Dispatcher.UIThread.RunJobs();

        Assert.True(strip.Scroll(PanelScrollStep.PageUp), "The strip declined to scroll.");

        strip.Close();
    }

    /// <summary>Drives the headset's real draw path, into a buffer shaped like the staging texture.</summary>
    private static byte[] Pixels(D47.App.Headset.VrPanelSurface surface)
    {
        var (width, height) = surface.Size;
        var rowBytes = width * 4;
        var buffer = new byte[rowBytes * height];

        unsafe
        {
            fixed (byte* into = buffer)
            {
                surface.Draw((IntPtr)into, rowBytes);
            }
        }

        return buffer;
    }

    private static (Window Window, PanelView Panel) Open(int lines = 200)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var model = new PanelViewModel();

        for (var line = 0; line < lines; line++)
        {
            model.Append($"Line {line}: the transcript has to be longer than the pane it is in.\n");
        }

        var panel = new PanelView { DataContext = model };
        var window = new Window { Content = panel, Width = 700, Height = 460 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // One more line once there is a laid-out window to follow in. Everything above was
        // appended to a panel with no extent, so the follow that ran then scrolled to the end of
        // nothing — which is the top, and leaves a fixture that is not where a live transcript
        // would be. This is the same reason the headset needs `KeepUp` between its layout and its
        // rasterise.
        model.Append("And the newest line, with somewhere to put it.");
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }
}
