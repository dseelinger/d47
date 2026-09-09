using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App;
using D47.App.Controls;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>The intro carries the disclosures and the <c>ⓘ</c> carries the reasoning.</summary>
public sealed class TheHelpImproveIntroIsDisclosuresOnlyTests
{
    private const string Destination = "https://donations.example/store";

    // The intro reads the destination and nothing else, so a send delegate would only add a button none of
    // these assertions is about.
    private static HelpImproveWindow Excerpt(string? destination = Destination) =>
        new(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
            _ => "an excerpt",
            destination: destination);

 /// <summary>The history half of the same window, which carries the same list bar one line.</summary>
    private static HelpImproveWindow History(string? destination = Destination) =>
        new(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
            _ => "an excerpt",
            destination: destination,
            read: (_, _, _) => throw new NotSupportedException("No reading is asked for here."),
            write: (_, _, _) => Task.CompletedTask);

    private static TextBlock Intro(Window window) =>
        window.GetVisualDescendants()
            .OfType<TextBlock>()
            .First(block => block.Text is not null && block.Text.StartsWith("Nothing is saved", StringComparison.Ordinal));

    private static string IntroOf(HelpImproveWindow window)
    {
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Only the history construction puts the toggle on screen, and that construction is here to read the
        // history intro — so ticking it where it exists is what makes this the history page rather than a
        // second reading of the excerpt one.
        var toggle = window.GetVisualDescendants()
            .OfType<CheckBox>()
            .SingleOrDefault(found => found.Name == "IncludeHistory" && found.IsVisible);

        if (toggle is not null)
        {
            toggle.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
        }

        var text = Intro(window).Text!;

        window.Close();

        return text;
    }

    /// <summary>Every term of the consent is on the surface.</summary>
    [AvaloniaFact]
    public void TheLedeStillCarriesEveryDisclosure()
    {
        var intro = IntroOf(Excerpt());

        Assert.Contains("Nothing is saved or sent until you press Send it", intro, StringComparison.Ordinal);
        Assert.Contains("no standing consent", intro, StringComparison.Ordinal);
        Assert.Contains("Your name and IDs are replaced", intro, StringComparison.Ordinal);
        Assert.Contains("other people's words removed before anything is sent or saved", intro, StringComparison.Ordinal);
        Assert.Contains("Only the text below is sent or saved", intro, StringComparison.Ordinal);
        Assert.Contains("Sent, it goes to Directive 47", intro, StringComparison.Ordinal);
        Assert.Contains("It is deleted after 30 days, or sooner when you press Forget", intro, StringComparison.Ordinal);
    }

 /// <summary>And none of the machinery.</summary>
    [AvaloniaFact]
    public void TheLedeNamesNoAddressNoPathAndNoHash()
    {
        foreach (var intro in new[] { IntroOf(Excerpt()), IntroOf(History()) })
        {
            Assert.DoesNotContain(Destination, intro, StringComparison.Ordinal);
            Assert.DoesNotContain("http", intro, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("data\\", intro, StringComparison.Ordinal);
            Assert.DoesNotContain("hash", intro, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("donor-token", intro, StringComparison.Ordinal);
        }
    }

 /// <summary>One list, two pages.</summary>
    [AvaloniaFact]
    public void WithAnAddressTheTwoPagesDifferOnlyOnTheRetentionLine()
    {
        var excerpt = IntroOf(Excerpt()).Split('\n');
        var history = IntroOf(History()).Split('\n');

        Assert.Equal(excerpt.Length, history.Length);

        var differing = Enumerable.Range(0, excerpt.Length)
            .Where(line => excerpt[line] != history[line])
            .ToList();

        Assert.Equal([excerpt.Length - 1], differing);

        Assert.Equal("  •  It is deleted after 30 days, or sooner when you press Forget.", excerpt[^1]);
        Assert.Equal("  •  It is kept until you press Forget.", history[^1]);
    }

    /// <summary>And none of the arguments for pressing.</summary>
    [AvaloniaFact]
    public void TheLedeCarriesNoneOfTheReasoning()
    {
        var intro = IntroOf(Excerpt());

        Assert.DoesNotContain("most useful thing", intro, StringComparison.Ordinal);
        Assert.DoesNotContain("how defects get found", intro, StringComparison.Ordinal);
        Assert.DoesNotContain("hundreds of megabytes", intro, StringComparison.Ordinal);
        Assert.DoesNotContain("replay", intro, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The reverse guard, and the one that matters most: the glyph holds no term of the consent.
    /// </summary>
    [Fact]
    public void TheGlyphHoldsNoDisclosure()
    {
        var reasoning = HelpImproveWindow.Reasoning;

        Assert.DoesNotContain("donor-token", reasoning, StringComparison.Ordinal);
        Assert.DoesNotContain("data\\donations", reasoning, StringComparison.Ordinal);
        Assert.DoesNotContain("thirty days", reasoning, StringComparison.Ordinal);
        Assert.DoesNotContain("Privacy and egress", reasoning, StringComparison.Ordinal);
        Assert.DoesNotContain("until you press", reasoning, StringComparison.Ordinal);

        // And it does hold the arguments, or the assertions above pass on an empty string.
        Assert.Contains("Why real journals", reasoning, StringComparison.Ordinal);
        Assert.Contains("What the scrub does", reasoning, StringComparison.Ordinal);
        Assert.DoesNotContain("Why a history is shown as a report", reasoning, StringComparison.Ordinal);

 // And in a Commander's words rather than the project's.
        Assert.DoesNotContain("defect", reasoning, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("parser", reasoning, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
 /// The chooser says what it decides, at the size and colour of the control it names — a
    /// caption drawn muted and a step smaller read as a footnote beside its own combo box.
    /// </summary>
    [AvaloniaFact]
    public void TheChooserLabelReadsIncludeAtTheControlsOwnScale()
    {
        var window = Excerpt();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var label = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(block => block.Text == "Include");

        Assert.Equal(TypeScale.Body, label.FontSize);
        Assert.DoesNotContain(
            window.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "Scale");

        window.Close();
    }

    /// <summary>
    /// The glyph is on the window, beside the mark, and its way out goes to the same page — one
    /// authored copy, on the site.
    /// </summary>
    [AvaloniaFact]
    public void TheGlyphSitsBesideTheMarkAndOpensTheSamePage()
    {
        var window = Excerpt();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var buttons = window.GetVisualDescendants().OfType<Button>().ToList();
        var info = buttons.Single(button => button.Name == "HelpImproveInfo");
        var mark = buttons.Single(button => button.Name == "HelpImproveHelp");

        Assert.Equal("ⓘ", info.Content);

        // Opened, because a flyout that is merely attached proves nothing: the reasoning and the way out to
        // the site both have to be on screen once it is showing.
        var flyout = Assert.IsType<Flyout>(info.Flyout);
        flyout.ShowAt(info);
        Dispatcher.UIThread.RunJobs();

        var shown = Assert.IsType<StackPanel>(flyout.Content);
        var body = shown.Children.OfType<TextBlock>().Single();
        var more = shown.Children.OfType<Button>().Single();

        Assert.Equal(HelpImproveWindow.Reasoning, body.Text);
        Assert.Equal("Read the full page on the website", more.Content);
        Assert.Equal(DocsSite.Page(HelpImproveWindow.HelpPage), ToolTip.GetTip(more));

        flyout.Hide();

        // Same parent, and the ⓘ first: the glyphs read left to right in the order they deepen.
        var row = Assert.IsType<StackPanel>(info.Parent);
        Assert.Same(row, mark.Parent);
        Assert.Equal(0, row.Children.IndexOf(info));
        Assert.Equal(1, row.Children.IndexOf(mark));

        window.Close();
    }

    /// <summary>
    /// With no address there is nothing to name a destination for, and the intro says so rather than
    /// leaving the bullet out — the warning about a file posted publicly is a fact about where it can
    /// end up, so it stays on the surface too.
    /// </summary>
    [AvaloniaFact]
    public void WithNoAddressTheLedeSaysSoAndKeepsTheWarning()
    {
        var intro = IntroOf(Excerpt(destination: null));

        Assert.Contains("No send address is set", intro, StringComparison.Ordinal);
        Assert.Contains("archived beyond anyone's reach", intro, StringComparison.Ordinal);
        Assert.DoesNotContain("donor-token", intro, StringComparison.Ordinal);
    }

    /// <summary>
 /// Forget sits on both pages and makes the call — the same one the <c>Privacy and egress</c>
    /// row makes, with its sentence going to the line each page already talks on.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForgetIsOnBothPagesAndSaysWhatHappened(bool history)
    {
        var presses = 0;

        var window = new HelpImproveWindow(
            new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
            _ => "an excerpt",
            destination: Destination,
            read: history ? (_, _, _) => throw new NotSupportedException() : null,
            write: history ? (_, _, _) => Task.CompletedTask : null,
            forget: _ =>
            {
                presses++;
                return Task.FromResult("Everything you sent has been deleted.");
            });

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Both pages means both, and the page opens on the excerpt now — so the history case has to ask for
        // the history half, or this theory would press Forget on the same page twice.
        if (history)
        {
            window.GetVisualDescendants()
                .OfType<CheckBox>()
                .Single(found => found.Name == "IncludeHistory")
                .IsChecked = true;

            Dispatcher.UIThread.RunJobs();
        }

        var button = window.GetVisualDescendants()
            .OfType<Button>()
            .Single(candidate => candidate.Name == "ForgetDonations");

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, presses);

        Assert.Contains(
            window.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "Everything you sent has been deleted." && block.IsVisible);

        window.Close();
    }

    /// <summary>
    /// Eyes on the drawn window, because "shorter" is a claim about a rendered page and every assertion
    /// above is a claim about a string.
    /// </summary>
    [AvaloniaFact]
    public void TheTrimmedWindowRendersToACapture()
    {
        var window = Excerpt();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "help-improve-trimmed.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }
}
