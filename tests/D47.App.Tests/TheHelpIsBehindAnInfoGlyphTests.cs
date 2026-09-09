using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A row's help is one press away rather than under every row (asked for 2026-09-01 — "That is WAY too
/// much text.
/// </summary>
public class TheHelpIsBehindAnInfoGlyphTests
{
    private static SettingsHost Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        // Push-to-talk is an Advanced row, and the calm page folds those away.
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

    /// <summary>The help is off the page and behind a glyph, with the words intact.</summary>
    [AvaloniaFact]
    public void TheWordsAreInTheCalloutRatherThanUnderTheRow()
    {
        var host = Open();

        var glyph = Info(host, "listening.pushToTalkKey");

        // A drawn mark, not a character: a font without it draws a box, and a character cannot be sized or
 // coloured with the marks beside it.
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

    /// <summary>It says what it is to somebody who cannot see it.</summary>
    [AvaloniaFact]
    public void TheGlyphSaysWhatItIs()
    {
        var host = Open();

        var glyph = Info(host, "listening.pushToTalkKey");

        Assert.Equal("About Push-to-talk", AutomationProperties.GetName(glyph));

        host.Close();
    }

    [AvaloniaFact]
    public void TheHoverShowsTheSameWordsAsTheClick()
    {
        var host = Open();

        var glyph = Info(host, "listening.pushToTalkKey");

        var flyoutWords = ((StackPanel)((Border)((Flyout)glyph.Flyout!).Content!).Child!)
            .Children.OfType<TextBlock>().Single().Text;

        var tip = Assert.IsType<Border>(ToolTip.GetTip(glyph));
        var tipWords = ((StackPanel)tip.Child!).Children.OfType<TextBlock>().Single();

        Assert.Equal(flyoutWords, tipWords.Text);
        Assert.Contains("Held, D47 listens", tipWords.Text, StringComparison.Ordinal);
        Assert.Equal(TextWrapping.Wrap, tipWords.TextWrapping);

        // The tooltip carries the same way out to the web page too, not just the words.
        var tipLink = ((StackPanel)tip.Child!).Children.OfType<Button>().Single();
        Assert.Equal("Help", tipLink.Content);

        host.Close();
    }

    /// <summary>The callout carries the way out to the web page.</summary>
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

    /// <summary>A search that only the help answers brings the help back out.</summary>
    [AvaloniaFact]
    public void AQueryOnlyTheHelpAnswersShowsTheHelp()
    {
        var host = Open();

        // One in the visual tree — the inline copy, hidden.
        var inline = Assert.Single(
            host.View.GetVisualDescendants().OfType<TextBlock>(),
            block => (block.Text ?? string.Empty).Contains("Held, D47 listens", StringComparison.Ordinal));

        Assert.False(inline.IsVisible);

        host.View.Filter("Held, D47 listens");

        // The same block, now drawn.
        Assert.True(inline.IsVisible);

        // And it goes away again when the query does.
        host.View.Filter(string.Empty);

        Assert.False(inline.IsVisible);

        host.Close();
    }

    /// <summary>A row with nothing to say has no glyph.</summary>
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

    /// <summary>It is chrome, and one predicate says so.</summary>
    [AvaloniaFact]
    public void TheGlyphIsChromeRatherThanTheControlTheRowIsAbout()
    {
        var host = Open();

        Assert.True(SettingsView.IsRowChrome(Info(host, "listening.pushToTalkKey")));

        Assert.False(SettingsView.IsRowChrome(new Button { Name = "Press_speech_localVoice" }));
        Assert.False(SettingsView.IsRowChrome(new Button()));

        host.Close();
    }

    /// <summary>
    /// The template the whole issue is about: a tooltip that is the row's own name read back (<c>"About
    /// {label}"</c>) or a click described rather than shown (<c>"What {label} does"</c>).
    /// </summary>
    private static readonly Regex EchoesTheLabelInstead = new(
        @"^(About .+|What .+ does)$", RegexOptions.Compiled);

 /// <summary>No info or help mark says the row's name back instead of its help.</summary>
    [AvaloniaFact]
    public void NoInfoGlyphsHoverEchoesTheRowsOwnName()
    {
        var host = Open();

        var offenders = host.View.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.Name?.StartsWith(
                SettingsView.RowInfoPrefix, StringComparison.Ordinal) is true)
            .Where(glyph => ToolTip.GetTip(glyph) is string tip && EchoesTheLabelInstead.IsMatch(tip))
            .Select(glyph => glyph.Name)
            .ToArray();

        Assert.True(offenders.Length == 0, "still templated: " + string.Join(", ", offenders));

        host.Close();
    }

    /// <summary>
    /// A two-sentence help string stays inside the tooltip rather than running off the window —
    /// measured on the drawn control, not by reading its <c>TextWrapping</c> and <c>MaxWidth</c>
    /// properties back.
    /// </summary>
    [AvaloniaFact]
    public void ATwoSentenceHelpStringWrapsInsideTheTooltip()
    {
        var host = Open();

        var glyph = Info(host, "listening.pushToTalkKey");
        var tip = Assert.IsType<Border>(ToolTip.GetTip(glyph));
        var words = ((StackPanel)tip.Child!).Children.OfType<TextBlock>().Single();

        // One line of the same text, at the same size, unwrapped — the yardstick a wrapped multi-line block
        // is measured against.
        var oneLine = new TextBlock
        {
            Text = "One short line.",
            FontSize = words.FontSize,
            TextWrapping = TextWrapping.NoWrap,
        };

        words.Measure(Size.Infinity);
        oneLine.Measure(Size.Infinity);

        Assert.True(
            words.DesiredSize.Width <= words.MaxWidth + 0.5,
            $"the tooltip measured {words.DesiredSize.Width}px wide against a {words.MaxWidth}px cap");

        Assert.True(
            words.DesiredSize.Height > oneLine.DesiredSize.Height * 1.5,
            "the help did not wrap onto more than one line");

        host.Close();
    }
}
