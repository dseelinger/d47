using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Panel;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary> The conversation is a conversation, so the Conversation page is drawn as one — a turn to a bubble,
/// the Commander's on the right and the ship's on the left, each side its own colour. </summary>
public class TheConversationLooksLikeOneTests
{
    /// <summary>An exchange with both sides in it, and the panel's own note about the core.</summary>
    private static PanelViewModel Exchange()
    {
        var model = new PanelViewModel();

        model.Append("Standing by, Commander.");
        model.Mark("Switched to Sentinel");
        model.Append("where am I", voice: TranscriptVoice.Commander);
        model.Append("Holding at Fixture Anchorage.");

        return model;
    }

    private static IReadOnlyList<Control> Turns(PanelView panel) =>
        [.. panel.GetControl<StackPanel>("Bubbles").Children];

    /// <summary>The bubble inside a turn's row, or null for a turn that is drawn without one.</summary>
    private static Border? Bubble(Control turn) =>
        turn is Grid row ? row.Children.OfType<Border>().Single() : null;

    private static Color? Colour(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    /// <summary>How much of the pane's width a turn may take before it wraps.</summary>
    private static double Share(PanelView panel)
    {
        var columns = ((Grid)Turns(panel)[0]).ColumnDefinitions;

        return columns[0].Width.Value / (columns[0].Width.Value + columns[^1].Width.Value);
    }

    /// <summary>What one turn says.</summary>
    private static string Said(SelectableTextBlock block) =>
        string.Concat(block.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text));

    [AvaloniaFact]
    public void TheCommanderIsOnTheRightAndTheShipOnTheLeft()
    {
        var panel = Laid(new PanelView { DataContext = Exchange() });

        var sides = Turns(panel)
            .Select(Bubble)
            .Where(bubble => bubble is not null)
            .Select(bubble => bubble!.HorizontalAlignment);

        Assert.Equal(
            [HorizontalAlignment.Left, HorizontalAlignment.Right, HorizontalAlignment.Left],
            sides);
    }

    /// <summary>
    /// And by colour as well as by side, which is the convention every messaging application on the
    /// Commander's phone already taught them.
    /// </summary>
    [AvaloniaFact]
    public void EachSideHasItsOwnColour()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(themeId: null);

        var window = new Window { Width = 900, Height = 560 };
        var panel = Laid(new PanelView { DataContext = Exchange() }, window);

        var fills = Turns(panel)
            .Select(Bubble)
            .Where(bubble => bubble is not null)
            .Select(bubble => Colour(bubble!.Background))
            .ToArray();

        Assert.Equal(Colour((IBrush?)window.FindResource(ThemeManager.SurfaceAltKey)), fills[0]);
        Assert.Equal(Colour((IBrush?)window.FindResource(ThemeManager.AccentMutedKey)), fills[1]);
        Assert.Equal(fills[0], fills[2]);
    }

    /// <summary>
    /// The panel's own note is not a side of the conversation, so it gets no bubble and sits across the
    /// middle — which is where a messaging thread puts the same kind of thing.
    /// </summary>
    [AvaloniaFact]
    public void ThePanelsOwnNoteSitsAcrossTheMiddle()
    {
        var panel = Laid(new PanelView { DataContext = Exchange() });

        var note = Assert.Single(Turns(panel).OfType<SelectableTextBlock>());

        Assert.Equal("[Switched to Sentinel]", Said(note));
        Assert.Equal(TextAlignment.Center, note.TextAlignment);
    }

    /// <summary>The framing a flat page needs is that page's way of saying who spoke.</summary>
    [AvaloniaFact]
    public void TheBubblesCarryWhatWasSaidAndNoneOfTheFraming()
    {
        var model = Exchange();
        var conversation = Laid(new PanelView { DataContext = model });

        Assert.Equal(
            ["Standing by, Commander.", "[Switched to Sentinel]", "where am I", "Holding at Fixture Anchorage."],
            conversation.TranscriptBlocks.Select(Said));

        // The buffer is untouched, mark and all — asserted on the buffer rather than through a page, because
 // the flat reading of the conversation was Details and Details is gone.
        Assert.Contains("\n\n> where am I\n", model.TranscriptText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheLogFileIsStillOneFlatBlock()
    {
        var model = Exchange();
        model.LogSource = () => "12:04 something happened";

        // One page rather than two.
        var panel = Laid(new PanelView { DataContext = model, Page = TranscriptPage.Log });

        Assert.Empty(Turns(panel));
        Assert.True(panel.GetControl<SelectableTextBlock>("Transcript").IsVisible);
    }

    /// <summary>
    /// Mini gets the same conversation and spends less on saying so: a headset panel with 512 pixels
    /// across it cannot give a fifth of them to a gutter when the colour already says which side a turn
    /// is on.
    /// </summary>
    [AvaloniaFact]
    public void MiniIsTheSameConversationMoreQuietly()
    {
        var model = Exchange();
        var full = Laid(new PanelView { DataContext = model, Mode = PanelMode.Full });
        var mini = Laid(new PanelView { DataContext = model, Mode = PanelMode.Mini });

        var wide = Bubble(Turns(full)[0])!;
        var tight = Bubble(Turns(mini)[0])!;

        Assert.True(
            tight.Padding.Left < wide.Padding.Left,
            $"mini padded the bubble {tight.Padding} against the window's {wide.Padding}");

        Assert.True(
            tight.Margin.Top < wide.Margin.Top,
            $"mini spaced the turns {tight.Margin} against the window's {wide.Margin}");

        // Still sided, and still coloured.
        Assert.Equal(wide.HorizontalAlignment, tight.HorizontalAlignment);
        Assert.Equal(Colour(wide.Background), Colour(tight.Background));

        // And the gutter it gives back is the point: a turn may run nearly the whole width.
        Assert.True(
            Share(mini) > Share(full),
            $"mini gave a turn {Share(mini):P0} of the width against the window's {Share(full):P0}");
    }

    /// <summary>A reply arrives a delta at a time, and each one redraws the page.</summary>
    [AvaloniaFact]
    public void AStreamingReplyOnlyRebuildsTheTurnItIsIn()
    {
        var model = Exchange();
        var panel = Laid(new PanelView { DataContext = model });

        var first = panel.TranscriptBlocks[0];
        var last = panel.TranscriptBlocks[^1];

        model.Append(" Fuel is at three quarters.");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Same(first, panel.TranscriptBlocks[0]);
        Assert.Same(last, panel.TranscriptBlocks[^1]);
        Assert.Equal("Holding at Fixture Anchorage. Fuel is at three quarters.", Said(last));
    }

    /// <summary>A new voice is a new bubble rather than more of the last one.</summary>
    [AvaloniaFact]
    public void TheOtherSideSpeakingStartsANewBubble()
    {
        var model = Exchange();
        var panel = Laid(new PanelView { DataContext = model });

        var before = Turns(panel).Count;

        model.Append("what's my fuel", voice: TranscriptVoice.Commander);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(before + 1, Turns(panel).Count);
        Assert.Equal(HorizontalAlignment.Right, Bubble(Turns(panel)[^1])!.HorizontalAlignment);
    }

    /// <summary>Searching still reaches every turn.</summary>
    [AvaloniaFact]
    public void AQueryHighlightsInsideWhicheverBubblesItMatched()
    {
        // The highlight is a bound resource, so the palette has to be in place for it to land.
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(themeId: null);

        var panel = Laid(new PanelView { DataContext = Exchange() });

        panel.EnableSearch();
        panel.GetControl<TextBox>("SearchInput").Text = "an";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var highlighted = panel.TranscriptBlocks
            .Where(block => block.Inlines!
                .OfType<Avalonia.Controls.Documents.Run>()
                .Any(run => run.Background is not null))
            .ToArray();

        Assert.True(
            highlighted.Length > 1,
            $"a query matching in three turns highlighted in {highlighted.Length} of "
            + $"{panel.TranscriptBlocks.Count}: {string.Join(" | ", panel.TranscriptBlocks.Select(Said))}");

        // And on the query rather than beside it.
        var marked = panel.TranscriptRuns
            .Where(run => run.Background is not null)
            .Select(run => run.Text);

        Assert.All(marked, text => Assert.Equal("an", text, ignoreCase: true));
    }

    /// <summary>
    /// The Commander's bubble sits flush against the transcript's right edge, which is exactly where
    /// <c>TranscriptScroller</c>'s default overlay scroll bar draws — thin when idle, wider (20px in
    /// the Fluent theme) when active.
    /// </summary>
    [AvaloniaFact]
    public void TheCommanderBubbleClearsTheScrollBar()
    {
        const double expandedScrollBarWidth = 20;

        var panel = Laid(new PanelView { DataContext = Exchange() });
        var scroller = panel.GetControl<ScrollViewer>("TranscriptScroller");

        var commanderBubble = Bubble(Turns(panel).First(turn =>
            Bubble(turn)?.HorizontalAlignment == HorizontalAlignment.Right))!;

        var bubbleRight = commanderBubble
            .TranslatePoint(new Point(commanderBubble.Bounds.Width, 0), scroller)!
            .Value.X;

        Assert.True(
            bubbleRight <= scroller.Bounds.Width - expandedScrollBarWidth,
            $"the Commander's bubble reaches x={bubbleRight} in a {scroller.Bounds.Width}-wide "
            + $"scroller, inside the {expandedScrollBarWidth}px the scroll bar can draw over");
    }

    private static PanelView Laid(PanelView panel, Window? into = null)
    {
        var window = into ?? new Window { Width = 900, Height = 560 };

        window.Width = 900;
        window.Height = 560;
        window.Content = panel;
        window.Show();

        var bounds = new Rect(0, 0, 900, 560);
        window.Measure(bounds.Size);
        window.Arrange(bounds);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return panel;
    }
}
