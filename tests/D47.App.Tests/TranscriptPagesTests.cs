using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>The transcript has three pages, and which one a surface shows is that surface's business.</summary>
public class TranscriptPagesTests
{
    private static PanelViewModel Said()
    {
        var model = new PanelViewModel();

        model.Append("Language model: ready.");
        model.Append("\n\n> Where am I?\n");
        model.Append("We're holding at HIP 12099 1 b.");

        return model;
    }

    [Fact]
    public void TheTranscriptKeepsEverythingAndKeepsItInOrder()
    {
        var text = Said().TranscriptText;

        Assert.Contains("Language model: ready.", text, StringComparison.Ordinal);
        Assert.Contains("Where am I?", text, StringComparison.Ordinal);
        Assert.Contains("HIP 12099 1 b.", text, StringComparison.Ordinal);

        Assert.True(
            text.IndexOf("Language model", StringComparison.Ordinal)
            < text.IndexOf("Where am I?", StringComparison.Ordinal),
            "A line written before a reply has to stay before it.");
    }

    /// <summary>
    /// A reply arrives one delta at a time, so consecutive writes in one voice have to join rather than
    /// each starting a run.
    /// </summary>
    [Fact]
    public void AStreamedReplyStaysOneRun()
    {
        var model = new PanelViewModel();

        model.Append("We're ");
        model.Append("holding ");
        model.Append("at HIP 12099.");

        Assert.Equal("We're holding at HIP 12099.", model.TranscriptText);
    }

    [Fact]
    public void TheLogPageReportsWhyItIsEmptyRatherThanBeingBlank()
    {
        var model = new PanelViewModel();
        model.RefreshLog();

        Assert.False(string.IsNullOrWhiteSpace(model.LogText));
    }

    [Fact]
    public void ALogThatCannotBeReadSaysSo()
    {
        var model = new PanelViewModel { LogSource = () => throw new IOException("it is locked") };

        model.RefreshLog();

        Assert.Contains("it is locked", model.LogText, StringComparison.Ordinal);
    }

    /// <summary>
    /// The page is a property of the surface, like <see cref="PanelMode"/> and for the same reason:
    /// both surfaces bind one model, so a page held there would send the headset to the log file the
    /// moment the window went to it.
    /// </summary>
    [AvaloniaFact]
    public void TwoSurfacesCanShowDifferentPagesOfTheSameTranscript()
    {
        var model = Said();

        var window = Laid(new PanelView { DataContext = model, Page = TranscriptPage.Log });
        var headset = Laid(new PanelView { DataContext = model, Page = TranscriptPage.Conversation });

        // One surface is on the log file and the other on the conversation, and neither can see the other's
        // page.
        Assert.DoesNotContain("HIP 12099 1 b.", Shown(window), StringComparison.Ordinal);
        Assert.Contains("HIP 12099 1 b.", Shown(headset), StringComparison.Ordinal);
    }

    /// <summary>Clicking a mode moves the page, so the control and the property stay one thing.</summary>
    [AvaloniaFact]
    public void CheckingATabMovesThePage()
    {
        var panel = Laid(new PanelView { DataContext = Said() });

        Assert.Equal(TranscriptPage.Conversation, panel.Page);

        PanelModes.Choose(panel, PanelView.LogRoot);

        Assert.Equal(TranscriptPage.Log, panel.Page);
    }

    /// <summary>
    /// Mini is "the transcript's tail and the provenance line" and nothing else, so the tabs go with
    /// the rest of the chrome rather than spending a 640x280 surface on three selectors.
    /// </summary>
    [AvaloniaFact]
    public void MiniShowsNoTabs()
    {
        var full = Laid(new PanelView { DataContext = Said(), Mode = PanelMode.Full });
        var mini = Laid(new PanelView { DataContext = Said(), Mode = PanelMode.Mini });

        // The whole strip, tabs and search box together: a surface with 640x280 to spend does not spend it on
        // four page selectors and a text field.
        Assert.True(full.GetControl<DockPanel>("TabStrip").IsVisible);
        Assert.False(mini.GetControl<DockPanel>("TabStrip").IsVisible);
    }

    /// <summary>Both surfaces open with a tab highlighted.</summary>
    [AvaloniaFact]
    public void TwoSurfacesEachKeepTheirOwnCheckedTab()
    {
        var model = Said();

        // Built in the order the app builds them: the window, then the headset overlay.
        var window = Laid(new PanelView { DataContext = model });
        var headset = Laid(new PanelView { DataContext = model });

        // Each surface's own control says In Ship, which is what "each keeps its own" means when one model
        // serves both.
        Assert.Equal("In Ship", PanelModes.Showing(window));
        Assert.Equal("In Ship", PanelModes.Showing(headset));
    }

    /// <summary>
    /// And moving one surface's page leaves the other where it was, which is the same independence seen
    /// from the other side.
    /// </summary>
    [AvaloniaFact]
    public void MovingOneSurfacesPageLeavesTheOtherAlone()
    {
        var model = Said();

        var window = Laid(new PanelView { DataContext = model });
        var headset = Laid(new PanelView { DataContext = model });

        PanelModes.Choose(window, PanelView.LogRoot);

        Assert.Equal(TranscriptPage.Log, window.Page);
        Assert.Equal(TranscriptPage.Conversation, headset.Page);
        Assert.Equal("In Ship", PanelModes.Showing(headset));
    }

    private static string Shown(PanelView panel) => panel.TranscriptShown;

    private static PanelView Laid(PanelView panel)
    {
        var window = new Window { Width = 900, Height = 560, Content = panel };
        window.Show();

        var bounds = new Rect(0, 0, 900, 560);
        window.Measure(bounds.Size);
        window.Arrange(bounds);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return panel;
    }
}
