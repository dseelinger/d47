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
/// The Conversation page is drawn as a conversation: a turn to a message, the ship's on the left behind an A
/// bar and the Commander's on the right behind a cyan bar on a cyan ground (#398).
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

    /// <summary>The speaker's name at the head of a message.</summary>
    private static TextBlock Name(Border message) =>
        (TextBlock)((WrapPanel)((DockPanel)((StackPanel)message.Child!).Children[0]).Children[1]).Children[0];

    /// <summary>What one turn says.</summary>
    private static string Said(SelectableTextBlock block) =>
        string.Concat(block.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text));

    [AvaloniaFact]
    public void TheShipSitsLeftAndTheCommanderRight()
    {
        var panel = Laid(new PanelView { DataContext = Exchange() });

        var messages = Messages(panel);

        Assert.Equal(3, messages.Count);
        Assert.Equal(HorizontalAlignment.Left, messages[0].HorizontalAlignment);
        Assert.Equal(HorizontalAlignment.Right, messages[1].HorizontalAlignment);
        Assert.Equal(HorizontalAlignment.Left, messages[2].HorizontalAlignment);
        Assert.Equal(new Thickness(3, 0, 0, 0), messages[0].BorderThickness);
        Assert.Equal(new Thickness(0, 0, 3, 0), messages[1].BorderThickness);

        var list = panel.GetControl<StackPanel>("Bubbles").Bounds;

        Assert.True(messages[1].Bounds.Right >= list.Width - 1, "the Commander's turn does not reach the right edge");
    }

    /// <summary>The ship's bar is A over no ground; the Commander's is cyan over cyan mixed onto bg. No corner.</summary>
    [AvaloniaFact]
    public void TheBarAndGroundFollowTheSpeaker()
    {
        Themed();

        var window = new Window();
        var panel = Laid(new PanelView { DataContext = Exchange() }, window);
        var messages = Messages(panel);

        Assert.All(messages, message => Assert.Equal(default, message.CornerRadius));

        Assert.Equal(Resource(window, ThemeManager.AKey), Colour(messages[0].BorderBrush));
        Assert.Equal(Colors.Transparent, Colour(messages[0].Background));

        Assert.Equal(Resource(window, ThemeManager.CyanKey), Colour(messages[1].BorderBrush));
        Assert.Equal(Resource(window, ThemeManager.CyanGroundKey), Colour(messages[1].Background));
    }

    /// <summary>The name is uppercase text in the bar's colour, and no name glows.</summary>
    [AvaloniaFact]
    public void TheNameIsTheBarsColourAndNothingGlows()
    {
        Themed();

        var window = new Window();
        var panel = Laid(new PanelView { DataContext = Exchange() }, window);
        var messages = Messages(panel);

        Assert.Equal("D47", Name(messages[0]).Text);
        Assert.Equal(Resource(window, ThemeManager.AKey), Colour(Name(messages[0]).Foreground));

        Assert.Equal("CMDR", Name(messages[1]).Text);
        Assert.Equal(Resource(window, ThemeManager.CyanKey), Colour(Name(messages[1]).Foreground));

        Assert.Empty(panel.GetControl<StackPanel>("Bubbles").GetVisualDescendants().OfType<BloomStack>());
    }

    /// <summary>The body is white, left-aligned prose, whoever spoke it.</summary>
    [AvaloniaFact]
    public void EveryBodyIsWhiteProseAlignedLeft()
    {
        Themed();

        var window = new Window();
        var panel = Laid(new PanelView { DataContext = Exchange() }, window);

        var bodies = panel.TranscriptBlocks.Where(block => block.TextAlignment != TextAlignment.Center).ToArray();

        Assert.Equal(3, bodies.Length);
        Assert.All(bodies, body =>
        {
            Assert.Equal(Resource(window, ThemeManager.WhiteKey), Colour(body.Foreground));
            Assert.Equal(TextAlignment.Left, body.TextAlignment);
            Assert.Equal(TypeScale.Body, body.FontSize);
            Assert.Contains("Sintony", body.FontFamily.ToString(), StringComparison.Ordinal);
        });
    }

    /// <summary>Hovering lays slab under a turn, and leaving puts back the ground it had.</summary>
    [AvaloniaFact]
    public void HoverLaysSlabUnderATurn()
    {
        Themed();

        var window = new Window();
        var panel = Laid(new PanelView { DataContext = Exchange() }, window);

        foreach (var (message, rest) in new[]
                 {
                     (Messages(panel)[0], (Color?)Colors.Transparent),
                     (Messages(panel)[1], Resource(window, ThemeManager.CyanGroundKey)),
                 })
        {
            var middle = message.TranslatePoint(new Point(40, message.Bounds.Height / 2), window)!.Value;
            window.MouseMove(middle, RawInputModifiers.None);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(Resource(window, ThemeManager.SlabKey), Colour(message.Background));

            window.MouseMove(new Point(1, 1), RawInputModifiers.None);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(rest, Colour(message.Background));
        }
    }

    /// <summary>No turn is wider than 80% of the list, and the cap follows the list when it is resized.</summary>
    [AvaloniaTheory]
    [InlineData(1600)]
    [InlineData(700)]
    public void NoTurnIsWiderThanItsShareOfTheList(double resizedTo)
    {
        var model = Exchange();

        model.Append(
            string.Join(' ', Enumerable.Repeat("where is the nearest scoopable star", 12)),
            voice: TranscriptVoice.Commander);
        model.Append(string.Join(' ', Enumerable.Repeat("The beacon is quiet and the turn is done.", 12)));

        var window = new Window();
        var panel = Laid(new PanelView { DataContext = model }, window, width: 1100);

        Laid(panel, window, width: resizedTo);

        var list = panel.GetControl<StackPanel>("Bubbles").Bounds.Width;

        Assert.All(Messages(panel), message =>
            Assert.True(
                message.Bounds.Width <= (list * PanelView.TurnShare) + 1,
                $"a turn is {message.Bounds.Width} wide in a list {list} wide"));
    }

    /// <summary>A delivery tag is drawn in the head and taken out of the body.</summary>
    [AvaloniaFact]
    public void ADeliveryTagIsInTheHeadAndNotTheBody()
    {
        var model = new PanelViewModel();
        model.Append("[calm] Functioning within tolerance, Commander.");

        var panel = Laid(new PanelView { DataContext = model });
        var message = Messages(panel).Single();

        var body = Said(panel.TranscriptBlocks.Single());
        var head = ((StackPanel)message.Child!).Children[0]
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(label => label.Text ?? string.Empty);

        Assert.DoesNotContain('[', body);
        Assert.Equal("Functioning within tolerance, Commander.", body);
        Assert.Contains(head, text => text.Contains("calm", StringComparison.Ordinal));
    }

    /// <summary>
    /// A long turn of either voice reaches its share of the list, before and after a resize; a short one stays
    /// narrower, flush against its own side.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(1600)]
    [InlineData(1100)]
    public void ALongTurnReachesItsShareAndAShortOneStaysNarrow(double resizedTo)
    {
        var model = new PanelViewModel();

        model.Append(string.Join(' ', Enumerable.Repeat("The beacon is quiet and the turn is done.", 12)));
        model.Append(
            string.Join(' ', Enumerable.Repeat("where is the nearest scoopable star", 12)),
            voice: TranscriptVoice.Commander);
        model.Append("Holding.");
        model.Append("Where am I?", voice: TranscriptVoice.Commander);

        var window = new Window();
        var panel = Laid(new PanelView { DataContext = model }, window, width: 1600);

        Laid(panel, window, width: resizedTo);

        var list = panel.GetControl<StackPanel>("Bubbles").Bounds.Width;
        var share = Math.Floor(list * PanelView.TurnShare);
        var messages = Messages(panel);

        Assert.Equal(4, messages.Count);
        Assert.All(messages.Take(2), message =>
            Assert.True(
                Math.Abs(message.Bounds.Width - share) <= 1,
                $"a long turn is {message.Bounds.Width} wide against a share of {share}"));

        Assert.All(messages.Skip(2), message =>
            Assert.True(message.Bounds.Width < share, $"a short turn is {message.Bounds.Width} wide"));

        Assert.True(messages[0].Bounds.Left <= 1, "the ship's long turn does not start at the left edge");
        Assert.True(messages[1].Bounds.Right >= list - 1, "the Commander's long turn does not reach the right edge");
        Assert.True(messages[2].Bounds.Left <= 1, "the ship's short turn does not start at the left edge");
        Assert.True(messages[3].Bounds.Right >= list - 1, "the Commander's short turn does not reach the right edge");
    }

    /// <summary>Long and short turns of both voices, drawn in the app's look at 1600 px and saved for review.</summary>
    [AvaloniaFact]
    public void LongAndShortTurnsAreCaptured()
    {
        var model = new PanelViewModel();

        model.Append(string.Join(' ', Enumerable.Repeat("The beacon is quiet and the turn is done.", 8)));
        model.Append(
            string.Join(' ', Enumerable.Repeat("where is the nearest scoopable star", 8)),
            voice: TranscriptVoice.Commander);
        model.Append("Holding at Fixture Anchorage.");
        model.Append("Where am I?", voice: TranscriptVoice.Commander);

        var path = AppLook.Capture(new PanelView { DataContext = model }, "transcript-turns.png", width: 1600);

        Assert.True(File.Exists(path));
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
        Assert.All(Messages(mini), message => Assert.NotEqual(default, message.BorderThickness));
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
        Assert.Equal("CMDR", Name(Messages(panel)[^1]).Text);
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
