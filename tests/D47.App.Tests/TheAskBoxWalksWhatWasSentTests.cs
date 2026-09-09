using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Up and down walk what has already been sent from the conversation box.</summary>
public class TheAskBoxWalksWhatWasSentTests
{
    private static (Window Window, PanelView View, PanelViewModel Model) Open()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var model = new PanelViewModel();
        var view = new PanelView { DataContext = model };
        var window = new Window { Content = view, Width = 900, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, view, model);
    }

    private static TextBox Box(PanelView view) => (TextBox)view.FindControl<Control>("AskBox")!;

    /// <summary>Through the control, so the handler under test is the one the Commander reaches.</summary>
    private static void Press(PanelView view, Key key)
    {
        Box(view).RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
        });

        Dispatcher.UIThread.RunJobs();
    }

    private static void Send(PanelView view, PanelViewModel model, string text)
    {
        model.AskText = text;
        Dispatcher.UIThread.RunJobs();

        Press(view, Key.Enter);
    }

    [AvaloniaFact]
    public void UpWalksBackThroughWhatWasSentAndStopsAtTheOldest()
    {
        var (window, view, model) = Open();

        Send(view, model, "where am I");
        Send(view, model, "what is my fuel");

        Press(view, Key.Up);
        Assert.Equal("what is my fuel", model.AskText);

        Press(view, Key.Up);
        Assert.Equal("where am I", model.AskText);

        // **Stops rather than wrapping.** A long history that silently returned to the newest reads as the key
        // having missed.
        Press(view, Key.Up);
        Assert.Equal("where am I", model.AskText);

        window.Close();
    }

    /// <summary>The detail most implementations miss.</summary>
    [AvaloniaFact]
    public void DownPastTheNewestGivesTheInterruptedDraftBack()
    {
        var (window, view, model) = Open();

        Send(view, model, "where am I");
        Send(view, model, "what is my fuel");

        model.AskText = "half a thought";
        Dispatcher.UIThread.RunJobs();

        Press(view, Key.Up);
        Press(view, Key.Up);
        Assert.Equal("where am I", model.AskText);

        Press(view, Key.Down);
        Assert.Equal("what is my fuel", model.AskText);

        Press(view, Key.Down);
        Assert.Equal("half a thought", model.AskText);

        // And the walk is over: Down again is not this feature's key any more, so the box is left exactly as
        // the Commander has it.
        Press(view, Key.Down);
        Assert.Equal("half a thought", model.AskText);

        window.Close();
    }

    /// <summary>
    /// Enter sends what is in the box, not what is remembered — the one key that has to stay
    /// unambiguous.
    /// </summary>
    [AvaloniaFact]
    public void EnterMidWalkSendsWhatIsInTheBox()
    {
        var (window, view, model) = Open();

        var sent = new List<string>();
        model.AskRequested += () => sent.Add(model.AskText);

        Send(view, model, "where am I");
        Send(view, model, "what is my fuel");

        Press(view, Key.Up);
        Press(view, Key.Up);

        model.AskText = "where am I really";
        Dispatcher.UIThread.RunJobs();

        Press(view, Key.Enter);

        Assert.Equal(["where am I", "what is my fuel", "where am I really"], sent);

        // Sending ended the walk, so Up starts again from the newest — which is the line just sent, and not
        // the one the walk had reached.
        Press(view, Key.Up);
        Assert.Equal("where am I really", model.AskText);

        window.Close();
    }

    /// <summary>Editing a recalled line does not rewrite history.</summary>
    [AvaloniaFact]
    public void EditingARecalledLineLeavesTheEntryAsItWasSent()
    {
        var (window, view, model) = Open();

        Send(view, model, "where am I");
        Send(view, model, "what is my fuel");

        Press(view, Key.Up);
        Press(view, Key.Up);

        model.AskText = "where am I now";
        Dispatcher.UIThread.RunJobs();

        // Down to the end drops the edit and gives back the draft the walk started from — the edit was never
        // an entry.
        Press(view, Key.Down);
        Press(view, Key.Down);

        Press(view, Key.Up);
        Press(view, Key.Up);
        Assert.Equal("where am I", model.AskText);

        window.Close();
    }

    /// <summary>Consecutive duplicates collapse, and nothing empty is ever remembered.</summary>
    [AvaloniaFact]
    public void DuplicatesCollapseAndBlanksAreNeverRemembered()
    {
        var (window, view, model) = Open();

        Send(view, model, "where am I");
        Send(view, model, "where am I");
        Send(view, model, "   ");
        Send(view, model, string.Empty);
        Send(view, model, "what is my fuel");

        Press(view, Key.Up);
        Assert.Equal("what is my fuel", model.AskText);

        Press(view, Key.Up);
        Assert.Equal("where am I", model.AskText);

        Press(view, Key.Up);
        Assert.Equal("where am I", model.AskText);

        window.Close();
    }

    /// <summary>
    /// With nothing sent, the arrows are somebody else's keys: an Up in an empty box must not swallow
    /// itself into a feature that has nothing to offer.
    /// </summary>
    [AvaloniaFact]
    public void WithNothingSentTheArrowsDoNothing()
    {
        var (window, view, model) = Open();

        model.AskText = "half a thought";
        Dispatcher.UIThread.RunJobs();

        Press(view, Key.Up);
        Press(view, Key.Down);

        Assert.Equal("half a thought", model.AskText);

        window.Close();
    }

    /// <summary>
    /// The caret follows a recalled line to its end, because a recalled line is one the Commander is
    /// about to edit or send.
    /// </summary>
    [AvaloniaFact]
    public void TheCaretLandsAtTheEndOfARecalledLine()
    {
        var (window, view, model) = Open();

        Send(view, model, "where am I");

        Box(view).CaretIndex = 0;
        Press(view, Key.Up);

        Assert.Equal("where am I".Length, Box(view).CaretIndex);

        window.Close();
    }
}
