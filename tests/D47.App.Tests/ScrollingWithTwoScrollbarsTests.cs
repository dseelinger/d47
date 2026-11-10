using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using Xunit;

namespace D47.App.Tests;

/// <summary>Moving the left or right scroller of a page that has two.</summary>
public sealed class ScrollingWithTwoScrollbarsTests
{
    [AvaloniaFact]
    public void PageDownRightMovesTheDetailAndLeavesTheList()
    {
        var (panel, window, list, detail) = Open();

        Assert.Equal(PanelScrollOutcome.Moved, panel.Scroll(PanelScrollStep.PageDown, PanelScrollSide.Right));

        Assert.True(detail.Offset.Y > 0);
        Assert.Equal(0, list.Offset.Y);

        window.Close();
    }

    [AvaloniaFact]
    public void PageDownLeftAndPlainPageDownMoveTheList()
    {
        var (panel, window, list, detail) = Open();

        Assert.Equal(PanelScrollOutcome.Moved, panel.Scroll(PanelScrollStep.PageDown, PanelScrollSide.Left));

        var left = list.Offset.Y;

        Assert.True(left > 0);

        Assert.Equal(PanelScrollOutcome.Moved, panel.Scroll(PanelScrollStep.PageDown, PanelScrollSide.Either));

        Assert.True(list.Offset.Y > left);
        Assert.Equal(0, detail.Offset.Y);

        window.Close();
    }

    [AvaloniaFact]
    public void NewestLeftPutsTheListAtTheTop()
    {
        var (panel, window, list, _) = Open();

        panel.Scroll(PanelScrollStep.PageDown, PanelScrollSide.Left);
        Assert.True(list.Offset.Y > 0);

        Assert.Equal(PanelScrollOutcome.Moved, panel.Scroll(PanelScrollStep.Newest, PanelScrollSide.Left));
        Assert.Equal(0, list.Offset.Y);

        window.Close();
    }

    [AvaloniaFact]
    public void TheBottomRightIsNewestAndThenAlreadyThere()
    {
        var (panel, window, _, detail) = Open();

        Assert.Equal(PanelScrollOutcome.Moved, panel.Scroll(PanelScrollStep.Newest, PanelScrollSide.Right));
        Assert.Equal(detail.Extent.Height - detail.Viewport.Height, detail.Offset.Y, 1);
        Assert.Equal(PanelScrollOutcome.AlreadyThere, panel.Scroll(PanelScrollStep.Newest, PanelScrollSide.Right));

        window.Close();
    }

    private static (PanelView Panel, Window Window, ScrollViewer List, ScrollViewer Detail) Open()
    {
        var log = new JournalLog();
        var events = new List<JournalEvent>();

        for (var second = 0; second < 50; second++)
        {
            events.Add(new JournalEvent(
                new DateTimeOffset(2026, 8, 27, 12, 3, second, TimeSpan.Zero),
                "Docked",
                JsonDocument.Parse("""{"event":"Docked","StationName":"Jameson Memorial"}""").RootElement));
        }

        log.Add(events);

        var model = new PanelViewModel();

        model.JournalSource = noise => log.Read(noise);
        model.JournalDocumentSource = noise => log.Document(noise);

        var panel = new PanelView { DataContext = model };
        var window = new Window { Content = panel, Width = 1180, Height = 500 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Page = TranscriptPage.Journal;
        Dispatcher.UIThread.RunJobs();

        var scrollers = panel.GetVisualDescendants().OfType<ScrollViewer>().ToList();
        var list = scrollers.Single(view => view.Name == "JournalListScroller");
        var detail = scrollers.Single(view => view.Name == "JournalDetailScroller");

        // The detail pane overflows whatever is selected, so the fixture gives it room to.
        detail.Content = new Border { Height = 3000 };
        Dispatcher.UIThread.RunJobs();

        Assert.True(list.Extent.Height > list.Viewport.Height, "The list does not overflow.");
        Assert.True(detail.Extent.Height > detail.Viewport.Height, "The detail does not overflow.");

        return (panel, window, list, detail);
    }
}
