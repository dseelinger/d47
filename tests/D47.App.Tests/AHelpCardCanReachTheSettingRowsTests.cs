using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Help;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary> The help mark on the Transcript page, and the three cards at the foot of what it opens. </summary>
public class AHelpCardCanReachTheSettingRowsTests
{
    private static Button Press(Control page, string label) =>
        page.GetVisualDescendants().OfType<Button>()
            .First(button => button.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == label));

    private static void Click(Button button) =>
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    /// <summary>The default reading's mark opens the page about the page.</summary>
    [AvaloniaFact]
    public void TheConversationReadingAsksForThePageAboutThePage()
    {
        var view = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = view };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelView.InShipHelp, view.Nav.Help);
        Assert.Equal("general-in-ship", PanelView.InShipHelp);

        var article = HelpLibrary.For(view.Nav.Help!);

        Assert.NotNull(article);
        Assert.Equal("In Ship", article.Title);

        window.Close();
    }

    /// <summary>
    /// End to end, on a surface that has settings: the mark, then a card, and the panel is on the
    /// Settings tab with that section named.
    /// </summary>
    [AvaloniaFact]
    public void PressingListeningLeavesHelpAndNamesThatSection()
    {
        var revealed = new List<string>();

        var view = new PanelView { DataContext = new PanelViewModel() };
        view.EnableSettings(() => new TextBlock { Text = "settings" }, revealed.Add);

        var window = new Window { Content = view, Width = 1100, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var mark = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "HelpButton");
        Click(mark);
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.Nav.Modal, "help took the panel");

        Click(Press(view, "Listening"));
        Dispatcher.UIThread.RunJobs();

        Assert.False(view.Nav.Modal, "help was dismissed");
        Assert.Equal(PanelTab.Settings, view.Tab);
        Assert.Equal(["listening"], revealed);

        window.Close();
    }

    /// <summary>
    /// The headset's copy is handed no settings at all, so the same card is a drill into the band about
    /// the same subject — which is why <c>listening.md</c> had to grow one before this could ship.
    /// </summary>
    [AvaloniaFact]
    public void WithNoSettingsTabTheSameCardDrillsIntoTheBandInstead()
    {
        var view = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = view, Width = 1100, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var mark = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "HelpButton");
        Click(mark);
        Dispatcher.UIThread.RunJobs();

        Click(Press(view, "Listening"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("help:listening", view.Nav.Trail[^1].Key);
        Assert.NotEqual(PanelTab.Settings, view.Tab);

        window.Close();
    }
}
