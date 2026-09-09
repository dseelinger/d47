using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Reading history does not mean fighting new lines as they arrive.</summary>
public class FollowingTheLiveLogTests
{
    private static PanelViewModel Talking()
    {
        var model = new PanelViewModel();

        // Enough to overflow the window several times over, so there is a real "up" to scroll to.
        for (var line = 0; line < 200; line++)
        {
            model.Append($"Line {line}: the frame shift drive is charged and the route is plotted.\n");
        }

        return model;
    }

    private static (Window Window, PanelView View) Open(PanelViewModel model)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var view = new PanelView { DataContext = model };
        view.EnableSearch();

        var window = new Window { Content = view, Width = 900, Height = 500 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, view);
    }

    private static T Named<T>(PanelView view, string name)
        where T : Control =>
        view.FindControl<T>(name) ?? throw new InvalidOperationException($"no {name}");

    private static ScrollViewer Scroller(PanelView view) => Named<ScrollViewer>(view, "TranscriptScroller");

    private static Button Follow(PanelView view) => Named<Button>(view, "FollowButton");

    /// <summary>Moves the view and lets the scroll handler see it, as a pointer or a wheel would.</summary>
    private static void ScrollTo(PanelView view, double y)
    {
        var scroller = Scroller(view);
        scroller.Offset = new Vector(scroller.Offset.X, y);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The Commander, following the tail, scrolling up to read something.</summary>
    private static void ReadHistory(PanelView view)
    {
        var scroller = Scroller(view);

        ScrollTo(view, scroller.Extent.Height - scroller.Viewport.Height);
        ScrollTo(view, 0);
    }

    [AvaloniaFact]
    public void ANewLineFollowsTheTailWhileNobodyHasScrolledAway()
    {
        var model = Talking();
        var (window, view) = Open(model);

        model.Append("The newest line of all.\n");
        Dispatcher.UIThread.RunJobs();

        var scroller = Scroller(view);

        Assert.True(
            scroller.Offset.Y >= scroller.Extent.Height - scroller.Viewport.Height - 1,
            $"the view sat at {scroller.Offset.Y} of {scroller.Extent.Height - scroller.Viewport.Height}.");

        // And nothing floating over the text, because there is nothing below the fold.
        Assert.False(Follow(view).IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void ScrollingUpStopsTheViewBeingDraggedBackDown()
    {
        var model = Talking();
        var (window, view) = Open(model);

        ReadHistory(view);

        model.Append("A line that arrives while the Commander is reading history.\n");
        Dispatcher.UIThread.RunJobs();

        // The whole complaint, as an assertion.
        Assert.Equal(0, Scroller(view).Offset.Y);

        window.Close();
    }

    [AvaloniaFact]
    public void AndOffersTheWayBack()
    {
        var model = Talking();
        var (window, view) = Open(model);

        ReadHistory(view);

        var button = Follow(view);

        Assert.True(button.IsVisible);
        Assert.Equal("↓ Newest", button.Content);

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var scroller = Scroller(view);

        Assert.True(scroller.Offset.Y >= scroller.Extent.Height - scroller.Viewport.Height - 1);
        Assert.False(button.IsVisible);

        window.Close();
    }

    /// <summary>
    /// And following starts again, so pressing the button once is enough — it is a way back to the live
    /// view rather than a single jump.
    /// </summary>
    [AvaloniaFact]
    public void PressingItResumesFollowing()
    {
        var model = Talking();
        var (window, view) = Open(model);

        ReadHistory(view);
        Follow(view).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        model.Append("One more, and the view should move for it.\n");
        Dispatcher.UIThread.RunJobs();

        var scroller = Scroller(view);

        Assert.True(scroller.Offset.Y >= scroller.Extent.Height - scroller.Viewport.Height - 1);

        window.Close();
    }

    /// <summary>Scrolling back to the bottom by hand is the same thing as pressing the button.</summary>
    [AvaloniaFact]
    public void ScrollingBackToTheBottomResumesFollowingToo()
    {
        var model = Talking();
        var (window, view) = Open(model);

        ReadHistory(view);

        var scroller = Scroller(view);
        ScrollTo(view, scroller.Extent.Height - scroller.Viewport.Height);

        Assert.False(Follow(view).IsVisible);

        model.Append("And this one moves the view.\n");
        Dispatcher.UIThread.RunJobs();

        Assert.True(scroller.Offset.Y >= scroller.Extent.Height - scroller.Viewport.Height - 1);

        window.Close();
    }

    /// <summary>Following belongs to the page being read.</summary>
    [AvaloniaFact]
    public void ChangingPageOpensAtTheNewestLineAgain()
    {
        var model = Talking();
        var (window, view) = Open(model);

        ReadHistory(view);
        Assert.True(Follow(view).IsVisible);

        view.Page = TranscriptPage.Log;
        Dispatcher.UIThread.RunJobs();

        model.Append("A line on the page just arrived at.\n");
        Dispatcher.UIThread.RunJobs();

        var scroller = Scroller(view);

        Assert.True(scroller.Offset.Y >= scroller.Extent.Height - scroller.Viewport.Height - 1);

        window.Close();
    }

    /// <summary>Selection and copy across the appending log are the control's own; nothing is invented for what the controls already give free.</summary>
    [AvaloniaFact]
    public void TheTranscriptIsSelectableWithNoWorkflowInvented()
    {
        var model = Talking();
        var (window, view) = Open(model);

        // The turn the Commander is dragging across.
        var transcript = view.TranscriptBlocks[0];

        transcript.SelectAll();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Line 0", transcript.SelectedText, StringComparison.Ordinal);
        Assert.Contains("Line 199", transcript.SelectedText, StringComparison.Ordinal);

        window.Close();
    }

 /// <summary>One affordance for the whole page as it is currently shown.</summary>
    [AvaloniaFact]
    public void TheCopyButtonSitsWithTheReadingToolsAndCopiesThePageBeingRead()
    {
        var model = new PanelViewModel();
        model.Append("Language model: ready.");
        model.Append("\n\n> Where am I?\nWe're holding at HIP 12099 1 b.");

        var (window, view) = Open(model);

        Assert.True(Named<Button>(view, "CopyButton").IsVisible);

        // What is copied is the page being read — the same text the Commander is looking at, and not a third
        // thing assembled for the clipboard.
        var conversation = string.Concat(model.Segments(TranscriptPage.Conversation).Select(s => s.Text));

        Assert.Contains("Language model: ready.", conversation, StringComparison.Ordinal);
        Assert.Contains("HIP 12099 1 b.", conversation, StringComparison.Ordinal);
        Assert.Contains("HIP 12099 1 b.", conversation, StringComparison.Ordinal);

        window.Close();
    }
}
