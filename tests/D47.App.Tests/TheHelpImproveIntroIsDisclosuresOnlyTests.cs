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

/// <summary>
/// The one-sentence lede and the three consent lines carry the disclosures; the intro's hover carries
/// the reasoning (#338).
/// </summary>
public sealed class TheHelpImproveIntroIsDisclosuresOnlyTests
{
    private const string Destination = "https://donations.example/store";

    // The consent reads the destination and nothing else, so a send delegate would only add a button none of
    // these assertions is about.
    private static HelpImproveWindow Excerpt(string? destination = Destination) =>
        new(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
            TestSurface.Excerpt("an excerpt"),
            destination: destination);

    /// <summary>The history half of the same window, which carries the same consent lines.</summary>
    private static HelpImproveWindow History(string? destination = Destination) =>
        new(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
            TestSurface.Excerpt("an excerpt"),
            destination: destination,
            read: (_, _, _) => throw new NotSupportedException("No reading is asked for here."),
            write: (_, _, _) => Task.CompletedTask);

    /// <summary>Every TextBlock on screen, once the window (and the history toggle, where it exists) has
    /// settled — the disclosures are split across several lines now, not one paragraph (#338).</summary>
    private static IReadOnlyList<string> TextsOf(HelpImproveWindow window)
    {
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Only the history construction puts the toggle on screen, and that construction is here to read the
        // history consent — so ticking it where it exists is what makes this the history page rather than a
        // second reading of the excerpt one.
        var toggle = window.GetVisualDescendants()
            .OfType<CheckBox>()
            .SingleOrDefault(found => found.Name == "IncludeHistory" && found.IsEffectivelyVisible);

        if (toggle is not null)
        {
            toggle.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
        }

        var texts = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        window.Close();

        return texts;
    }

    private static bool Has(IReadOnlyList<string> texts, string substring) =>
        texts.Any(text => text.Contains(substring, StringComparison.Ordinal));

    /// <summary>Every term of the consent is on the surface, somewhere.</summary>
    [AvaloniaFact]
    public void EveryDisclosureIsOnTheSurface()
    {
        var texts = TextsOf(Excerpt());

        Assert.True(Has(texts, "Send the developer a slice of what just happened"));
        Assert.True(Has(texts, "Nothing is saved or sent until you press Send it"));
        Assert.True(Has(texts, "no standing consent"));
        Assert.True(Has(texts, "Names and IDs are replaced with stand-ins"));
        Assert.True(Has(texts, "other people's words are stripped"));
        Assert.True(Has(texts, "Sent, it goes to Directive 47"));
        Assert.True(Has(texts, "It is deleted after 30 days, or sooner when you press Forget"));
    }

    /// <summary>And none of the machinery.</summary>
    [AvaloniaFact]
    public void NothingNamesNoAddressNoPathAndNoHash()
    {
        foreach (var texts in new[] { TextsOf(Excerpt()), TextsOf(History()) })
        {
            Assert.False(Has(texts, Destination));
            Assert.False(Has(texts, "http"));
            Assert.False(Has(texts, "data\\"));
            Assert.False(Has(texts, "hash"));
            Assert.False(Has(texts, "donor-token"));
        }
    }

    /// <summary>One set of consent lines, two pages — differing only on the retention clause.</summary>
    [AvaloniaFact]
    public void WithAnAddressTheTwoPagesDifferOnlyOnRetention()
    {
        var excerpt = TextsOf(Excerpt()).Single(text => text.Contains("Sent, it goes to Directive 47", StringComparison.Ordinal));
        var history = TextsOf(History()).Single(text => text.Contains("Sent, it goes to Directive 47", StringComparison.Ordinal));

        Assert.Equal(
            "Sent, it goes to Directive 47. It is deleted after 30 days, or sooner when you press Forget.",
            excerpt);
        Assert.Equal("Sent, it goes to Directive 47. It is kept until you press Forget.", history);
    }

    /// <summary>And none of the arguments for pressing.</summary>
    [AvaloniaFact]
    public void NothingCarriesTheReasoning()
    {
        var texts = TextsOf(Excerpt());

        Assert.False(Has(texts, "most useful thing"));
        Assert.False(Has(texts, "how defects get found"));
        Assert.False(Has(texts, "hundreds of megabytes"));
        Assert.DoesNotContain(texts, text => text.Contains("replay", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The reverse guard, and the one that matters most: the hover holds no term of the consent.
    /// </summary>
    [Fact]
    public void TheHoverHoldsNoDisclosure()
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

    /// <summary>The reasoning is the intro's hover, and no separate glyph carries it (#360).</summary>
    [AvaloniaFact]
    public void TheReasoningIsTheIntrosHover()
    {
        var window = Excerpt();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            window.GetVisualDescendants().OfType<TextBlock>(),
            block => Equals(ToolTip.GetTip(block), HelpImproveWindow.Reasoning));
        Assert.DoesNotContain(
            window.GetVisualDescendants().OfType<Button>(),
            button => button.Name == "HelpImproveInfo");

        window.Close();
    }

    /// <summary>
    /// With no address there is nothing to name a destination for, and the consent line says so rather
    /// than leaving it out — the warning about a file posted publicly is a fact about where it can end
    /// up, so it stays on the surface too.
    /// </summary>
    [AvaloniaFact]
    public void WithNoAddressTheConsentSaysSoAndKeepsTheWarning()
    {
        var texts = TextsOf(Excerpt(destination: null));

        Assert.True(Has(texts, "No send address is set"));
        Assert.True(Has(texts, "archived beyond anyone's reach"));
        Assert.False(Has(texts, "donor-token"));
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
            TestSurface.Excerpt("an excerpt"),
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
