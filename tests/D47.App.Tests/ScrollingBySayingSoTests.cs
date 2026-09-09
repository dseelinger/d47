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

/// <summary>Moving a page by saying so.</summary>
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

        Assert.Equal(PanelScrollOutcome.Moved, panel.Scroll(PanelScrollStep.PageUp));
        Dispatcher.UIThread.RunJobs();

        var up = scroller.Offset.Y;

        Assert.True(up < bottom, "Page up did not move the transcript.");

        Assert.Equal(PanelScrollOutcome.Moved, panel.Scroll(PanelScrollStep.PageDown));
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

    /// <summary>At the end, nothing happens and it says so.</summary>
    [AvaloniaFact]
    public void AtTheEndItDeclinesRatherThanPretending()
    {
        var (window, panel) = Open();

        Assert.Equal(PanelScrollOutcome.AlreadyThere, panel.Scroll(PanelScrollStep.PageDown));

        var scroller = panel.GetControl<ScrollViewer>("TranscriptScroller");

        scroller.Offset = scroller.Offset.WithY(0);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelScrollOutcome.AlreadyThere, panel.Scroll(PanelScrollStep.PageUp));

        window.Close();
    }

    /// <summary>
    /// A page with nothing over the fold has nothing to scroll, and says so — which is a different
 /// answer from being at the end of one, and the difference is what the Commander hears.
    /// </summary>
    [AvaloniaFact]
    public void APageThatFitsIsNotScrolled()
    {
        var (window, panel) = Open(lines: 1);

        Assert.Equal(PanelScrollOutcome.NothingToScroll, panel.Scroll(PanelScrollStep.PageDown));
        Assert.Equal(PanelScrollOutcome.NothingToScroll, panel.Scroll(PanelScrollStep.PageUp));

        window.Close();
    }

    /// <summary>
    /// Whichever region is showing, not the transcript alone — which is the half that matters now that
    /// mini and the strip carry the checklist and the engineer pages.
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

        Assert.Equal(PanelScrollOutcome.Moved, panel.Scroll(PanelScrollStep.PageDown));
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
    /// Scrolling up by voice means what scrolling up by hand means: stop following the newest line.
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

    /// <summary>The headset, through what it actually draws.</summary>
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

        Assert.Equal(PanelScrollOutcome.Moved, headset.Scroll(PanelScrollStep.PageUp));

        var after = Pixels(headset);

        Assert.NotEqual(before, after);
    }

    /// <summary>The strip, which had no other way at all.</summary>
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

        // Hidden, it declines: a phrase must not be swallowed by a surface nobody can see, or the Commander
        // gets silence from the one that was showing too.
        Assert.Equal(PanelScrollOutcome.NothingToScroll, strip.Scroll(PanelScrollStep.PageUp));

        strip.Show();
        Dispatcher.UIThread.RunJobs();

        model.Append("And the newest line, with somewhere to put it.");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelScrollOutcome.Moved, strip.Scroll(PanelScrollStep.PageUp));

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

        // One more line once there is a laid-out window to follow in.
        model.Append("And the newest line, with somewhere to put it.");
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }
}
