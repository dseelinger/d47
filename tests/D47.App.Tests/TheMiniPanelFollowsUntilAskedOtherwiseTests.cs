using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

public class TheMiniPanelFollowsUntilAskedOtherwiseTests
{
    private static (PanelViewModel Model, PanelView View, OffscreenSurface Surface) Mini(PanelMode mode = PanelMode.Mini)
    {
        var model = new PanelViewModel();

        for (var line = 0; line < 30; line++)
        {
            model.Append($"Line {line} of the transcript, long enough to wrap once or twice.\n", sourceKey: $"k{line}");
        }

        var view = new PanelView { DataContext = model, Mode = mode };
        var surface = new OffscreenSurface(view, new PixelSize(600, 400));

        Settle(surface, view);
        return (model, view, surface);
    }

    private static void Settle(OffscreenSurface surface, PanelView view)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            surface.Render(view.KeepUp);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static ScrollViewer Scroller(PanelView view) =>
        view.GetVisualDescendants().OfType<ScrollViewer>().First(found => found.Name == "TranscriptScroller");

    private static bool AtTheEnd(PanelView view)
    {
        var scroller = Scroller(view);
        return scroller.Offset.Y >= scroller.Extent.Height - scroller.Viewport.Height - 5;
    }

    private static void Append(PanelViewModel model, string key) =>
        Task.Run(() => model.Append($"Turn {key} that arrived afterwards, long enough to wrap.\n", sourceKey: key)).Wait();

    [AvaloniaFact]
    public void AnOffsetMoveNobodyAskedForDoesNotStopFollowing()
    {
        var (model, view, surface) = Mini();
        using var _ = surface;
        var scroller = Scroller(view);

        scroller.Offset = scroller.Offset.WithY(0);
        Settle(surface, view);

        Append(model, "after-layout");
        Settle(surface, view);

        Assert.True(AtTheEnd(view));
    }

    [AvaloniaFact]
    public void PageUpStaysPutAndTailFollowsAgain()
    {
        var (model, view, surface) = Mini();
        using var _ = surface;

        Assert.Equal(PanelScrollOutcome.Moved, view.Scroll(PanelScrollStep.PageUp));
        Settle(surface, view);
        var offset = Scroller(view).Offset.Y;

        Append(model, "one");
        Settle(surface, view);

        Assert.Equal(offset, Scroller(view).Offset.Y);
        Assert.False(AtTheEnd(view));

        Assert.Equal((PanelScrollStep.Newest, PanelScrollSide.Either), PanelScroll.Match("tail"));
        view.Scroll(PanelScrollStep.Newest);
        Settle(surface, view);
        Assert.True(AtTheEnd(view));

        Append(model, "two");
        Settle(surface, view);
        Assert.True(AtTheEnd(view));
    }

    [AvaloniaFact]
    public void ADragUpTheScrollbarStopsFollowing()
    {
        var (model, view, surface) = Mini();
        using var _ = surface;

        var bar = view.GetVisualDescendants()
            .OfType<ScrollBar>()
            .First(found => found.Orientation == Avalonia.Layout.Orientation.Vertical && found.Maximum > 0);
        var top = bar.TranslatePoint(new Point(0, 0), view)!.Value;

        OffscreenSurface.Aim(bar, view, new Point(top.X, top.Y));
        view.ScrollbarDragged();
        Settle(surface, view);
        var offset = Scroller(view).Offset.Y;

        Append(model, "after-drag");
        Settle(surface, view);

        Assert.Equal(offset, Scroller(view).Offset.Y);
        Assert.False(AtTheEnd(view));
    }
}
