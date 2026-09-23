using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Conversation page is drawn as a conversation: a turn to a message, every one on the left behind a
/// 2px rule, told apart by the badge in its head rather than by side or colour.
/// </summary>
public class TheConversationLooksLikeOneTests
{
    /// <summary>An exchange with both speakers in it, and the panel's own note about the core.</summary>
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

    private static IReadOnlyList<Border> Messages(PanelView panel) => [.. Turns(panel).OfType<Border>()];

    private static Color? Colour(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    private static Color? Resource(Control near, string key) => Colour((IBrush?)near.FindResource(key));

    /// <summary>The badge in a message's head.</summary>
    private static Border Badge(Border message) =>
        message.GetVisualDescendants().OfType<Border>().First(border => border.Child is TextBlock);

    /// <summary>What one turn says.</summary>
    private static string Said(SelectableTextBlock block) =>
        string.Concat(block.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text));

    [AvaloniaFact]
    public void BothSpeakersSitOnTheLeft()
    {
        var panel = Laid(new PanelView { DataContext = Exchange() });

        var messages = Messages(panel);

        Assert.Equal(3, messages.Count);
        Assert.All(messages, message => Assert.Equal(HorizontalAlignment.Stretch, message.HorizontalAlignment));
        Assert.All(messages, message => Assert.Equal(new Thickness(14, 0, 0, 0), message.Padding));
    }

    /// <summary>A 2px left rule and nothing else: no border, no ground, no clipped corner.</summary>
    [AvaloniaFact]
    public void AMessageIsALeftRuleAndNothingElse()
    {
        Themed();

        var window = new Window();
        var panel = Laid(new PanelView { DataContext = Exchange() }, window);

        Assert.All(Messages(panel), message =>
        {
            Assert.Equal(new Thickness(2, 0, 0, 0), message.BorderThickness);
            Assert.Equal(default, message.CornerRadius);
            Assert.Equal(Resource(window, ThemeManager.BorderKey), Colour(message.BorderBrush));
            Assert.Equal(Colors.Transparent, Colour(message.Background));
        });
    }

    /// <summary>D47's badge is reverse video in Accent; the Commander's is a line-2 fill in ink-2.</summary>
    [AvaloniaFact]
    public void TheBadgeTellsTheSpeakersApart()
    {
        Themed();

        var window = new Window();
        var panel = Laid(new PanelView { DataContext = Exchange() }, window);
        var messages = Messages(panel);

        var ship = Badge(messages[0]);
        var commander = Badge(messages[1]);

        Assert.Equal(Resource(window, ThemeManager.AccentKey), Colour(ship.Background));
        Assert.Equal(Resource(window, ThemeManager.KnockKey), Colour(((TextBlock)ship.Child!).Foreground));
        Assert.IsType<BloomStack>(ship.GetVisualParent());

        Assert.Equal(Resource(window, ThemeManager.BorderKey), Colour(commander.Background));
        Assert.Equal(Resource(window, ThemeManager.TextMutedKey), Colour(((TextBlock)commander.Child!).Foreground));
    }

    /// <summary>The body is ink-2 prose, whoever spoke it.</summary>
    [AvaloniaFact]
    public void EveryBodyIsInkTwoProse()
    {
        Themed();

        var window = new Window();
        var panel = Laid(new PanelView { DataContext = Exchange() }, window);

        var bodies = panel.TranscriptBlocks.Where(block => block.TextAlignment != TextAlignment.Center).ToArray();

        Assert.Equal(3, bodies.Length);
        Assert.All(bodies, body =>
        {
            Assert.Equal(Resource(window, ThemeManager.TextMutedKey), Colour(body.Foreground));
            Assert.Equal(TypeScale.Body, body.FontSize);
            Assert.Contains("Sintony", body.FontFamily.ToString(), StringComparison.Ordinal);
        });
    }

    /// <summary>Hovering lays fill-1 under the row and lifts the rule from line-2 to line.</summary>
    [AvaloniaFact]
    public void HoverLiftsTheRule()
    {
        Themed();

        var window = new Window();
        var panel = Laid(new PanelView { DataContext = Exchange() }, window);
        var message = Messages(panel)[0];

        var middle = message.TranslatePoint(new Point(40, message.Bounds.Height / 2), window)!.Value;
        window.MouseMove(middle, RawInputModifiers.None);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(Resource(window, ThemeManager.FillLowKey), Colour(message.Background));
        Assert.Equal(Resource(window, ThemeManager.RuleKey), Colour(message.BorderBrush));

        window.MouseMove(new Point(1, 1), RawInputModifiers.None);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(Colors.Transparent, Colour(message.Background));
        Assert.Equal(Resource(window, ThemeManager.BorderKey), Colour(message.BorderBrush));
    }

    /// <summary>A long body wraps at 76 characters' width rather than running the width of the window.</summary>
    [AvaloniaFact]
    public void ALongBodyStopsAtItsMeasure()
    {
        var model = new PanelViewModel();
        model.Append(string.Join(' ', Enumerable.Repeat("The beacon is quiet and the turn is done.", 12)));

        var window = new Window();
        var panel = Laid(new PanelView { DataContext = model }, window, width: 1600);

        var body = panel.TranscriptBlocks.Single();

        Assert.True(body.Bounds.Width <= PanelView.BodyMaxWidth, $"the body is {body.Bounds.Width} wide");
    }

    /// <summary>
    /// The panel's own note is not a turn of the conversation, so it gets no badge and sits across the
    /// middle.
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
    public void TheMessagesCarryWhatWasSaidAndNoneOfTheFraming()
    {
        var model = Exchange();
        var conversation = Laid(new PanelView { DataContext = model });

        Assert.Equal(
            ["Standing by, Commander.", "[Switched to Sentinel]", "where am I", "Holding at Fixture Anchorage."],
            conversation.TranscriptBlocks.Select(Said));

        // The buffer is untouched, mark and all.
        Assert.Contains("\n\n> where am I\n", model.TranscriptText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheLogFileIsStillOneFlatBlock()
    {
        var model = Exchange();
        model.LogSource = () => "12:04 something happened";

        var panel = Laid(new PanelView { DataContext = model, Page = TranscriptPage.Log });

        Assert.Empty(Turns(panel));
        Assert.True(panel.GetControl<SelectableTextBlock>("Transcript").IsVisible);
    }

    /// <summary>Mini draws the same messages with the same builder.</summary>
    [AvaloniaFact]
    public void MiniIsTheSameConversation()
    {
        var model = Exchange();
        var full = Laid(new PanelView { DataContext = model, Mode = PanelMode.Full });
        var mini = Laid(new PanelView { DataContext = model, Mode = PanelMode.Mini });

        Assert.Equal(Messages(full).Count, Messages(mini).Count);
        Assert.All(Messages(mini), message => Assert.Equal(new Thickness(2, 0, 0, 0), message.BorderThickness));
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

    /// <summary>A new voice is a new message rather than more of the last one.</summary>
    [AvaloniaFact]
    public void TheOtherSpeakerStartsANewMessage()
    {
        var model = Exchange();
        var panel = Laid(new PanelView { DataContext = model });

        var before = Turns(panel).Count;

        model.Append("what's my fuel", voice: TranscriptVoice.Commander);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(before + 1, Turns(panel).Count);
        Assert.Equal("CMDR", ((TextBlock)Badge(Messages(panel)[^1]).Child!).Text);
    }

    /// <summary>Before anything is said, one centred line stands in for the list.</summary>
    [AvaloniaFact]
    public void AnEmptyConversationSaysSo()
    {
        var model = new PanelViewModel();
        var panel = Laid(new PanelView { DataContext = model });

        var empty = panel.GetControl<TextBlock>("EmptyConversation");

        Assert.True(empty.IsVisible);
        Assert.Equal(HorizontalAlignment.Center, empty.HorizontalAlignment);

        model.Append("Standing by, Commander.");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(empty.IsVisible);
    }

    /// <summary>Searching still reaches every turn.</summary>
    [AvaloniaFact]
    public void AQueryHighlightsInsideWhicheverMessagesItMatched()
    {
        Themed();

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

        var marked = panel.TranscriptRuns
            .Where(run => run.Background is not null)
            .Select(run => run.Text);

        Assert.All(marked, text => Assert.Equal("an", text, ignoreCase: true));
    }

    private static void Themed() =>
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(themeId: null);

    private static PanelView Laid(PanelView panel, Window? into = null, double width = 900)
    {
        var window = into ?? new Window();

        window.Width = width;
        window.Height = 560;
        window.Content = panel;
        window.Show();

        var bounds = new Rect(0, 0, width, 560);
        window.Measure(bounds.Size);
        window.Arrange(bounds);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return panel;
    }
}
