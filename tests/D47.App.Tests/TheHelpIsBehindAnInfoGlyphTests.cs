using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A row's help is one press away rather than under every row
/// (asked for 2026-09-01 — <i>"That is WAY too much text. Use an info glyph … that when clicked
/// shows the text in a callout-type box"</i>).
/// <para>
/// <b>The words are not cut, they are moved.</b> Push-to-talk's help runs to eleven lines, and
/// eleven lines of grey prose under every row is a page nobody scans — the setting a Commander
/// came for is buried in the explanation of the setting above it.
/// </para>
/// </summary>
public class TheHelpIsBehindAnInfoGlyphTests
{
    private static SettingsHost Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        // Push-to-talk is an Advanced row, and the calm page folds those away. Every claim here is
        // about what a row looks like when it is on screen, not about which rows are.
        settings.Apply(
            D47.Core.Capabilities.Builtin.InterfaceCapability.ShowEverySettingKey,
            "true",
            D47.Core.Configuration.SettingsCaller.Panel);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        return SettingsHost.Open(settings, viewState, paths);
    }

    private static Button Info(SettingsHost host, string key) =>
        host.View.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == SettingsView.RowInfoPrefix + key.Replace('.', '_'));

    /// <summary>
    /// <b>The help is off the page and behind a glyph, with the words intact.</b> Both halves
    /// matter: a row that still printed its eleven lines would not have fixed anything, and a
    /// glyph whose callout was empty would have lost them.
    /// </summary>
    [AvaloniaFact]
    public void TheWordsAreInTheCalloutRatherThanUnderTheRow()
    {
        var host = Open();

        var glyph = Info(host, "listening.pushToTalkKey");

        // A drawn mark, not a character: a font without it draws a box, and a character cannot be
        // sized or coloured with the marks beside it (#69).
        Assert.IsType<Avalonia.Controls.Shapes.Path>(glyph.Content);

        var flyout = Assert.IsType<Flyout>(glyph.Flyout);
        var inside = Assert.IsType<Border>(flyout.Content);

        var words = ((StackPanel)inside.Child!).Children.OfType<TextBlock>().Single();

        Assert.Contains("Held, D47 listens", words.Text, StringComparison.Ordinal);

        // And the same words are not also printed under the row, which is the whole request.
        Assert.DoesNotContain(
            host.View.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsVisible),
            block => (block.Text ?? string.Empty).Contains("Held, D47 listens", StringComparison.Ordinal));

        host.Close();
    }

    /// <summary>
    /// <b>It says what it is to somebody who cannot see it.</b> A Path has no text, so a glyph-only
    /// button is an unnamed button to a screen reader — the same fault, and the same fix, as the
    /// reset glyph beside it.
    /// </summary>
    [AvaloniaFact]
    public void TheGlyphSaysWhatItIs()
    {
        var host = Open();

        var glyph = Info(host, "listening.pushToTalkKey");

        Assert.Equal("About Push-to-talk", AutomationProperties.GetName(glyph));
        Assert.Equal("About Push-to-talk", ToolTip.GetTip(glyph) as string);

        host.Close();
    }

    /// <summary>
    /// <b>The callout carries the way out to the web page.</b> The row already knew its anchor and
    /// nothing drawn had ever offered it — <c>DocsAnchor</c> was read by the documentation gate and
    /// by no control. The short form is in the callout; the long form is one more press.
    /// </summary>
    [AvaloniaFact]
    public void TheCalloutOffersTheHelpPage()
    {
        var host = Open();

        var flyout = (Flyout)Info(host, "listening.pushToTalkKey").Flyout!;
        var inside = (Border)flyout.Content!;

        var link = ((StackPanel)inside.Child!).Children.OfType<Button>().Single();

        Assert.Equal("Help", link.Content);

        host.Close();
    }

    /// <summary>
    /// <b>A search that only the help answers brings the help back out.</b> Matches() has always
    /// tested the help text, and it is no longer on screen — so without this a row survives a
    /// filter with every visible word on it disagreeing with the query, which reads as the filter
    /// being broken rather than as a match the Commander cannot see. The same rule the settings
    /// key already follows.
    /// </summary>
    [AvaloniaFact]
    public void AQueryOnlyTheHelpAnswersShowsTheHelp()
    {
        var host = Open();

        // One in the visual tree — the inline copy, hidden. The callout's own is inside a Flyout,
        // which builds its content when it is opened and not before.
        var inline = Assert.Single(
            host.View.GetVisualDescendants().OfType<TextBlock>(),
            block => (block.Text ?? string.Empty).Contains("Held, D47 listens", StringComparison.Ordinal));

        Assert.False(inline.IsVisible);

        host.View.Filter("Held, D47 listens");

        // The same block, now drawn. Asserted on the block rather than by hunting the tree for its
        // words, because Paint moves a marked caption into Inlines and empties Text — the words
        // are on screen and the property they used to be in is bare.
        Assert.True(inline.IsVisible);

        // And it goes away again when the query does.
        host.View.Filter(string.Empty);

        Assert.False(inline.IsVisible);

        host.Close();
    }

    /// <summary>
    /// <b>A row with nothing to say has no glyph.</b> A control that opens an empty box is worse
    /// than no control: it teaches the Commander that pressing it is not worth it.
    /// </summary>
    [AvaloniaFact]
    public void ARowWithNoHelpHasNoGlyph()
    {
        var host = Open();

        var glyphs = host.View.GetVisualDescendants().OfType<Button>()
            .Count(button => button.Name?.StartsWith(SettingsView.RowInfoPrefix, StringComparison.Ordinal) is true);

        var withHelp = host.View.GetVisualDescendants().OfType<Button>()
            .Count(button => button.Name?.StartsWith(SettingsView.RowInfoPrefix, StringComparison.Ordinal) is true
                             && button.Flyout is Flyout { Content: Border });

        Assert.True(glyphs > 0, "no row offered its help at all");
        Assert.Equal(glyphs, withHelp);

        host.Close();
    }

    /// <summary>
    /// <b>It is chrome, and one predicate says so.</b> Two tests once took the first Button in a
    /// row and got the reset glyph; adding this one broke seven more that had each learned to
    /// exclude the reset one by name. A third mark would have broken them again.
    /// </summary>
    [AvaloniaFact]
    public void TheGlyphIsChromeRatherThanTheControlTheRowIsAbout()
    {
        var host = Open();

        Assert.True(SettingsView.IsRowChrome(Info(host, "listening.pushToTalkKey")));

        Assert.False(SettingsView.IsRowChrome(new Button { Name = "Press_speech_localVoice" }));
        Assert.False(SettingsView.IsRowChrome(new Button()));

        host.Close();
    }
}
