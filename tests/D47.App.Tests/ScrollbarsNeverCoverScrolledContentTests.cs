using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace D47.App.Tests;

/// <summary>The content column and the vertical bar's column never draw over each other.</summary>
public class ScrollbarsNeverCoverScrolledContentTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static (Window Window, ScrollViewer Scroller) Filled()
    {
        var stack = new StackPanel();

        for (var line = 0; line < 60; line++)
        {
            stack.Children.Add(new TextBlock { Text = $"Line {line} of overflowing content." });
        }

        var scroller = new ScrollViewer
        {
            Content = stack,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        var window = new Window { Content = scroller, Width = 300, Height = 200 };
        window.Show();
        Jobs();

        return (window, scroller);
    }

    private static Rect BoundsIn(Control control, Visual ancestor)
    {
        var corner = control.TranslatePoint(new Point(0, 0), ancestor);
        Assert.NotNull(corner);
        return new Rect(corner.Value, control.Bounds.Size);
    }

    private static bool Overlap(Rect a, Rect b) => a.Intersects(b) && (a.Intersect(b)).Width > 0 && a.Intersect(b).Height > 0;

    [AvaloniaFact]
    public void TheContentAndTheVerticalBarNeverOverlap()
    {
        var (window, scroller) = Filled();

        var presenter = scroller.GetVisualDescendants().OfType<ScrollContentPresenter>().First();
        var bar = scroller.GetVisualDescendants().OfType<ScrollBar>()
            .First(found => found.Orientation == Orientation.Vertical);

        Assert.True(bar.IsVisible, "there is more content than fits, so the bar is shown");

        var contentBounds = BoundsIn(presenter, window);
        var barBounds = BoundsIn(bar, window);

        Assert.False(Overlap(contentBounds, barBounds), "collapsed: the bar still covers the content");

        // The pointer is on the bar, where the thumb widens to full width.
        window.MouseMove(new Point(barBounds.Center.X, barBounds.Center.Y));
        Jobs();

        contentBounds = BoundsIn(presenter, window);
        barBounds = BoundsIn(bar, window);

        Assert.False(Overlap(contentBounds, barBounds), "expanded: the bar still covers the content");
    }

    [AvaloniaFact]
    public void TheThumbIsFullWidthWhetherOrNotThePointerIsOnIt()
    {
        var (window, scroller) = Filled();

        var bar = scroller.GetVisualDescendants().OfType<ScrollBar>()
            .First(found => found.Orientation == Orientation.Vertical);
        var thumb = bar.GetVisualDescendants().OfType<Thumb>().First();

        var collapsedWidth = thumb.Bounds.Width;

        window.MouseMove(new Point(bar.Bounds.Center.X, bar.Bounds.Center.Y));
        Jobs();

        Assert.Equal(collapsedWidth, thumb.Bounds.Width, 1);
    }

    [AvaloniaFact]
    public void ContentThatFitsTakesTheFullWidthAndShowsNoBar()
    {
        var scroller = new ScrollViewer
        {
            Content = new TextBlock { Text = "One short line." },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        var window = new Window { Content = scroller, Width = 300, Height = 200 };
        window.Show();
        Jobs();

        var presenter = scroller.GetVisualDescendants().OfType<ScrollContentPresenter>().First();
        var bar = scroller.GetVisualDescendants().OfType<ScrollBar>()
            .First(found => found.Orientation == Orientation.Vertical);

        Assert.False(bar.IsVisible, "nothing to scroll, so the bar does not show");
        Assert.Equal(scroller.Bounds.Width, presenter.Bounds.Width, 1);
    }
}
