using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App;
using D47.App.Controls;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The lede carries the disclosures and the <c>ⓘ</c> carries the reasoning
/// (<a href="https://github.com/dseelinger/d47/issues/269">#269</a>).
/// <para>
/// <b>What is asserted is the split, in both directions</b>, because both halves rot the same way
/// and only one of them is visible. A disclosure drifting behind the glyph puts a term of the
/// consent one press away, which is the thing two trims have now been careful not to do; an
/// argument drifting back into the lede rebuilds the wall of text a press at a time.
/// </para>
/// </summary>
public sealed class TheHelpImproveLedeIsDisclosuresOnlyTests
{
    private const string Destination = "https://donations.example/store";

    // The lede reads the destination and nothing else, so a send delegate would only add a button
    // none of these assertions is about.
    private static HelpImproveWindow Excerpt(string? destination = Destination) =>
        new(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
            _ => "an excerpt",
            destination: destination);

    private static TextBlock Lede(Window window) =>
        window.GetVisualDescendants()
            .OfType<TextBlock>()
            .First(block => block.Text is not null && block.Text.StartsWith("Nothing is read", StringComparison.Ordinal));

    /// <summary>
    /// Every term of the consent is on the surface. These are the statements a Commander who
    /// presses neither glyph must still have read: what happens to the payload, where it goes,
    /// how long it lasts, what stays here, and what travels with it.
    /// </summary>
    [AvaloniaFact]
    public void TheLedeStillCarriesEveryDisclosure()
    {
        var window = Excerpt();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var lede = Lede(window).Text!;

        Assert.Contains("Nothing is read, written or sent until you press", lede, StringComparison.Ordinal);
        Assert.Contains("no standing consent", lede, StringComparison.Ordinal);
        Assert.Contains("your name and IDs replaced", lede, StringComparison.Ordinal);
        Assert.Contains("other people's words", lede, StringComparison.Ordinal);
        Assert.Contains(Destination, lede, StringComparison.Ordinal);
        Assert.Contains("thirty days", lede, StringComparison.Ordinal);
        Assert.Contains("Privacy and egress", lede, StringComparison.Ordinal);
        Assert.Contains("data\\donations", lede, StringComparison.Ordinal);
        Assert.Contains("data\\donor-token.txt", lede, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    /// And none of the arguments for pressing. Each of these was a sentence in the lede before
    /// #269; each is an argument rather than a fact, so each is behind the glyph now.
    /// </summary>
    [AvaloniaFact]
    public void TheLedeCarriesNoneOfTheReasoning()
    {
        var window = Excerpt();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var lede = Lede(window).Text!;

        Assert.DoesNotContain("most useful thing", lede, StringComparison.Ordinal);
        Assert.DoesNotContain("how defects get found", lede, StringComparison.Ordinal);
        Assert.DoesNotContain("hundreds of megabytes", lede, StringComparison.Ordinal);
        Assert.DoesNotContain("replay", lede, StringComparison.OrdinalIgnoreCase);

        window.Close();
    }

    /// <summary>
    /// The reverse guard, and the one that matters most: the glyph holds no term of the consent.
    /// A disclosure moved here to shorten the lede would be invisible to the Commander who never
    /// presses it, which is exactly the trade both trims refused.
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
        Assert.Contains("Why a history is shown as a report", reasoning, StringComparison.Ordinal);
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

        // Opened, because a flyout that is merely attached proves nothing: the reasoning and the
        // way out to the site both have to be on screen once it is showing.
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
    /// With no address there is nothing to name a destination for, and the lede says so rather
    /// than leaving the bullet out — the warning about a file posted publicly is a fact about
    /// where it can end up, so it stays on the surface too.
    /// <para>
    /// <b>The token line stays as well, and that is the shape this window already had</b> rather
    /// than a call #269 made. It reads oddly beside "nothing can be sent from here" and it is
    /// asserted here so the oddity is a recorded state rather than something a later trim silently
    /// resolves in either direction.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void WithNoAddressTheLedeSaysSoAndKeepsTheWarning()
    {
        var window = Excerpt(destination: null);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var lede = Lede(window).Text!;

        Assert.Contains("No send address is set", lede, StringComparison.Ordinal);
        Assert.Contains("archived beyond anyone's reach", lede, StringComparison.Ordinal);
        Assert.Contains("donor-token.txt", lede, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    /// Eyes on the drawn window, because "shorter" is a claim about a rendered page and every
    /// assertion above is a claim about a string.
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
