using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The transcript has three pages, and which one a surface shows is that surface's business.
/// <para>
/// The split rests on the caller saying what kind of line it is writing, because it is not
/// recoverable afterwards: a version banner and a reply are both text, and separating them by
/// pattern is guessing at somebody's prose.
/// </para>
/// </summary>
public class TranscriptPagesTests
{
    private static PanelViewModel Said()
    {
        var model = new PanelViewModel();

        model.Append("Directive 47 0.5.8\nLanguage model: ready.", TranscriptKind.Technical);
        model.Append("\n\n> Where am I?\n");
        model.Append("We're holding at HIP 12099 1 b.");

        return model;
    }

    [Fact]
    public void TheConversationPageLeavesTheDiagnosticsOut()
    {
        var model = Said();

        Assert.DoesNotContain("Language model: ready.", model.ConversationText, StringComparison.Ordinal);
        Assert.Contains("Where am I?", model.ConversationText, StringComparison.Ordinal);
        Assert.Contains("HIP 12099 1 b.", model.ConversationText, StringComparison.Ordinal);
    }

    /// <summary>The technical page is the transcript as it always was — everything, in order.</summary>
    [Fact]
    public void TheTechnicalPageKeepsEverythingAndKeepsItInOrder()
    {
        var text = Said().TranscriptText;

        Assert.Contains("Language model: ready.", text, StringComparison.Ordinal);
        Assert.Contains("HIP 12099 1 b.", text, StringComparison.Ordinal);

        Assert.True(
            text.IndexOf("Language model", StringComparison.Ordinal)
            < text.IndexOf("Where am I?", StringComparison.Ordinal),
            "A technical line written before a reply has to stay before it.");
    }

    /// <summary>
    /// A reply arrives one delta at a time, so consecutive writes of one kind have to join
    /// rather than each starting a run. Otherwise a sentence is split into a run per token and
    /// the ordering machinery is doing work per character.
    /// </summary>
    [Fact]
    public void AStreamedReplyStaysOneRunOfConversation()
    {
        var model = new PanelViewModel();

        model.Append("We're ");
        model.Append("holding ");
        model.Append("at HIP 12099.");

        Assert.Equal("We're holding at HIP 12099.", model.ConversationText);
        Assert.Equal("We're holding at HIP 12099.", model.TranscriptText);
    }

    /// <summary>The default is unchanged, so nothing that streams a reply had to be touched.</summary>
    [Fact]
    public void AppendWithoutAKindIsConversation()
    {
        var model = new PanelViewModel();
        model.Append("Receiving you clearly, Commander.");

        Assert.Equal("Receiving you clearly, Commander.", model.ConversationText);
    }

    [Fact]
    public void TheLogPageReportsWhyItIsEmptyRatherThanBeingBlank()
    {
        var model = new PanelViewModel();
        model.RefreshLog();

        Assert.False(string.IsNullOrWhiteSpace(model.LogText));
    }

    /// <summary>
    /// A log that cannot be read says so. It is a diagnostic; failing to show it must not be a
    /// second fault to chase.
    /// </summary>
    [Fact]
    public void ALogThatCannotBeReadSaysSo()
    {
        var model = new PanelViewModel { LogSource = () => throw new IOException("it is locked") };

        model.RefreshLog();

        Assert.Contains("it is locked", model.LogText, StringComparison.Ordinal);
    }

    /// <summary>
    /// The page is a property of the surface, like <see cref="PanelMode"/> and for the same
    /// reason: both surfaces bind one model, so a page held there would send the headset to the
    /// log file the moment the window went to it.
    /// </summary>
    [AvaloniaFact]
    public void TwoSurfacesCanShowDifferentPagesOfTheSameTranscript()
    {
        var model = Said();

        var window = Laid(new PanelView { DataContext = model, Page = TranscriptPage.Technical });
        var headset = Laid(new PanelView { DataContext = model, Page = TranscriptPage.Conversation });

        Assert.Contains("Language model: ready.", Shown(window), StringComparison.Ordinal);
        Assert.DoesNotContain("Language model: ready.", Shown(headset), StringComparison.Ordinal);

        // Same transcript underneath, which is the half that must not become two.
        Assert.Contains("HIP 12099 1 b.", Shown(window), StringComparison.Ordinal);
        Assert.Contains("HIP 12099 1 b.", Shown(headset), StringComparison.Ordinal);
    }

    /// <summary>Clicking a tab moves the page, so the strip and the property stay one thing.</summary>
    [AvaloniaFact]
    public void CheckingATabMovesThePage()
    {
        var panel = Laid(new PanelView { DataContext = Said() });

        Assert.Equal(TranscriptPage.Conversation, panel.Page);

        panel.GetControl<RadioButton>("LogTab").IsChecked = true;

        Assert.Equal(TranscriptPage.Log, panel.Page);
    }

    /// <summary>
    /// Mini is "the transcript's tail and the provenance line" and nothing else, so the tabs go
    /// with the rest of the chrome rather than spending a 640x280 surface on three selectors.
    /// </summary>
    [AvaloniaFact]
    public void MiniShowsNoTabs()
    {
        var full = Laid(new PanelView { DataContext = Said(), Mode = PanelMode.Full });
        var mini = Laid(new PanelView { DataContext = Said(), Mode = PanelMode.Mini });

        // The whole strip, tabs and search box together: a surface with 640x280 to spend does
        // not spend it on four page selectors and a text field.
        Assert.True(full.GetControl<DockPanel>("TabStrip").IsVisible);
        Assert.False(mini.GetControl<DockPanel>("TabStrip").IsVisible);
    }

    /// <summary>
    /// Both surfaces open with a tab highlighted.
    /// <para>
    /// Reported from a running build: the window opened with no tab marked at all, and only
    /// showed one once it was clicked. Avalonia groups named radio buttons across the whole
    /// application rather than within a tree, and this view is instantiated twice - once by the
    /// window and once by the headset overlay - so the second strip built unchecked the first.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TwoSurfacesEachKeepTheirOwnCheckedTab()
    {
        var model = Said();

        // Built in the order the app builds them: the window, then the headset overlay.
        var window = Laid(new PanelView { DataContext = model });
        var headset = Laid(new PanelView { DataContext = model });

        Assert.Equal(
            true,
            window.GetControl<RadioButton>("ConversationTab").IsChecked);

        Assert.Equal(
            true,
            headset.GetControl<RadioButton>("ConversationTab").IsChecked);
    }

    /// <summary>
    /// And moving one surface's page leaves the other where it was, which is the same
    /// independence seen from the other side.
    /// </summary>
    [AvaloniaFact]
    public void MovingOneSurfacesPageLeavesTheOtherAlone()
    {
        var model = Said();

        var window = Laid(new PanelView { DataContext = model });
        var headset = Laid(new PanelView { DataContext = model });

        window.GetControl<RadioButton>("LogTab").IsChecked = true;

        Assert.Equal(TranscriptPage.Log, window.Page);
        Assert.Equal(TranscriptPage.Conversation, headset.Page);
        Assert.Equal(true, headset.GetControl<RadioButton>("ConversationTab").IsChecked);
    }

    private static string Shown(PanelView panel) =>
        PanelParityTests.Shown(panel.GetControl<SelectableTextBlock>("Transcript"));

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
