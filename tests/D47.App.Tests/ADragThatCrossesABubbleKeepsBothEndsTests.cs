using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A drag on the conversation page that starts in one bubble and ends in another keeps both ends and
/// every bubble between them, rather than only the bubble it started in (#114).
/// </summary>
public class ADragThatCrossesABubbleKeepsBothEndsTests
{
    private static (Window Window, PanelView Panel) Laid(PanelView panel)
    {
        var window = new Window { Width = 900, Height = 700, Content = panel };

        window.Show();

        var bounds = new Rect(0, 0, 900, 700);
        window.Measure(bounds.Size);
        window.Arrange(bounds);
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    /// <summary>Ship, Commander, ship — three turns, so a drag across all of them has one to cross in the
    /// middle without being either end.</summary>
    private static PanelViewModel Exchange()
    {
        var model = new PanelViewModel();

        model.Append("Standing by, Commander.");
        model.Append("acknowledged", voice: TranscriptVoice.Commander);
        model.Append("Holding at Fixture Anchorage.");

        return model;
    }

    private static string Full(SelectableTextBlock block) => block.Inlines?.Text ?? string.Empty;

    /// <summary>A point a fraction of the way along a bubble's own text, in <c>Bubbles</c>'s coordinate
    /// space — where the panel's own drag handling reads pointer positions from (#114).</summary>
    private static Point PointIn(PanelView panel, SelectableTextBlock block, double fraction)
    {
        var bubbles = panel.GetControl<StackPanel>("Bubbles");
        var origin = block.TranslatePoint(new Point(0, 0), bubbles)!.Value;

        return origin + new Point(block.Bounds.Width * fraction, block.Bounds.Height / 2);
    }

    private static MenuItem CopyItem(PanelView panel) =>
        panel.GetControl<SelectableTextBlock>("Transcript").ContextMenu!.Items
            .OfType<MenuItem>()
            .Single(entry => entry.Name == "CopySelectionItem");

    [AvaloniaFact]
    public void ADragFromOneBubbleIntoALaterOneKeepsEveryBubbleBetween()
    {
        var (_, panel) = Laid(new PanelView { DataContext = Exchange() });
        var blocks = panel.TranscriptBlocks;

        panel.BeginBubbleDrag(PointIn(panel, blocks[0], 0.1));
        panel.ContinueBubbleDrag(PointIn(panel, blocks[^1], 0.9));

        var first = blocks[0].SelectedText ?? string.Empty;
        var last = blocks[^1].SelectedText ?? string.Empty;

        // The drag began partway through the first bubble and ended partway through the last, so each
        // end keeps only the part the drag actually crossed: from where it started to the end of the
        // first bubble, and from the start of the last bubble to where it ended.
        Assert.True(first.Length > 0 && first.Length < Full(blocks[0]).Length, first);
        Assert.EndsWith(first, Full(blocks[0]), StringComparison.Ordinal);

        Assert.Equal("acknowledged", blocks[1].SelectedText);

        Assert.True(last.Length > 0 && last.Length < Full(blocks[^1]).Length, last);
        Assert.StartsWith(last, Full(blocks[^1]), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void ADragTheOtherWayKeepsEveryBubbleBetweenToo()
    {
        var (_, panel) = Laid(new PanelView { DataContext = Exchange() });
        var blocks = panel.TranscriptBlocks;

        panel.BeginBubbleDrag(PointIn(panel, blocks[^1], 0.9));
        panel.ContinueBubbleDrag(PointIn(panel, blocks[0], 0.1));

        var first = blocks[0].SelectedText ?? string.Empty;
        var last = blocks[^1].SelectedText ?? string.Empty;

        Assert.True(first.Length > 0 && first.Length < Full(blocks[0]).Length, first);
        Assert.EndsWith(first, Full(blocks[0]), StringComparison.Ordinal);

        Assert.Equal("acknowledged", blocks[1].SelectedText);

        Assert.True(last.Length > 0 && last.Length < Full(blocks[^1]).Length, last);
        Assert.StartsWith(last, Full(blocks[^1]), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task CopyPutsEveryBubbleTheDragCrossedOnTheClipboardInOrder()
    {
        var (window, panel) = Laid(new PanelView { DataContext = Exchange() });
        var blocks = panel.TranscriptBlocks;

        panel.BeginBubbleDrag(PointIn(panel, blocks[0], 0.1));
        panel.ContinueBubbleDrag(PointIn(panel, blocks[^1], 0.9));
        Dispatcher.UIThread.RunJobs();

        var copy = CopyItem(panel);

        Assert.True(copy.IsEnabled);

        copy.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var written = await TopLevel.GetTopLevel(window)!.Clipboard!.TryGetTextAsync();

        Assert.NotNull(written);
        Assert.Contains(blocks[0].SelectedText!, written);
        Assert.Contains("acknowledged", written);
        Assert.Contains(blocks[^1].SelectedText!, written);

        Assert.True(
            written.IndexOf("Commander", StringComparison.Ordinal)
            < written.IndexOf("acknowledged", StringComparison.Ordinal));

        Assert.True(
            written.IndexOf("acknowledged", StringComparison.Ordinal)
            < written.IndexOf("Holding at", StringComparison.Ordinal));
    }

    /// <summary>A drag that never leaves the bubble it began in is left to that block's own selection —
    /// nothing here touches any bubble but the one the drag is in.</summary>
    [AvaloniaFact]
    public void ADragThatStaysInOneBubbleTouchesNoOtherBubble()
    {
        var (_, panel) = Laid(new PanelView { DataContext = Exchange() });
        var blocks = panel.TranscriptBlocks;

        panel.BeginBubbleDrag(PointIn(panel, blocks[0], 0.1));
        panel.ContinueBubbleDrag(PointIn(panel, blocks[0], 0.9));

        Assert.Empty(blocks[1].SelectedText ?? string.Empty);
        Assert.Empty(blocks[^1].SelectedText ?? string.Empty);
    }

    /// <summary>Starting a new drag clears what an earlier one left selected, rather than adding to it.</summary>
    [AvaloniaFact]
    public void StartingANewDragClearsTheOldSelection()
    {
        var (_, panel) = Laid(new PanelView { DataContext = Exchange() });
        var blocks = panel.TranscriptBlocks;

        panel.BeginBubbleDrag(PointIn(panel, blocks[0], 0.1));
        panel.ContinueBubbleDrag(PointIn(panel, blocks[^1], 0.9));

        Assert.NotEmpty(blocks[0].SelectedText ?? string.Empty);
        Assert.NotEmpty(blocks[1].SelectedText ?? string.Empty);

        panel.BeginBubbleDrag(PointIn(panel, blocks[1], 0.1));
        panel.ContinueBubbleDrag(PointIn(panel, blocks[1], 0.9));

        Assert.Empty(blocks[0].SelectedText ?? string.Empty);
        Assert.NotEmpty(blocks[1].SelectedText ?? string.Empty);
        Assert.Empty(blocks[^1].SelectedText ?? string.Empty);
    }

    /// <summary>A drag that returns to the bubble it began in, after leaving it, collapses back to a
    /// plain selection inside that one bubble.</summary>
    [AvaloniaFact]
    public void ADragThatLeavesAndComesBackCollapsesToOneBubble()
    {
        var (_, panel) = Laid(new PanelView { DataContext = Exchange() });
        var blocks = panel.TranscriptBlocks;

        panel.BeginBubbleDrag(PointIn(panel, blocks[0], 0.1));
        panel.ContinueBubbleDrag(PointIn(panel, blocks[^1], 0.9));
        panel.ContinueBubbleDrag(PointIn(panel, blocks[0], 0.9));

        Assert.NotEmpty(blocks[0].SelectedText ?? string.Empty);
        Assert.Empty(blocks[1].SelectedText ?? string.Empty);
        Assert.Empty(blocks[^1].SelectedText ?? string.Empty);
    }
}
