using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A row's help is reached by hovering or focusing its own label, with no separate glyph (#333).
/// </summary>
public class TheRowsHelpLivesOnItsLabelTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

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

    private static TextBlock Label(SettingsHost host, string key) =>
        host.View.LabelFor(key) ?? throw new InvalidOperationException($"{key} has no label");

    /// <summary>The help is on the row's own label rather than printed under it.</summary>
    [AvaloniaFact]
    public void TheWordsAreOnTheLabelsTooltipRatherThanUnderTheRow()
    {
        var host = Open();

        var label = Label(host, "listening.pushToTalkKey");
        var tip = Assert.IsType<Border>(ToolTip.GetTip(label));
        var words = ((StackPanel)tip.Child!).Children.OfType<TextBlock>().Single();

        Assert.Contains("Held, D47 listens", words.Text, StringComparison.Ordinal);

        Assert.DoesNotContain(
            host.View.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsVisible && block != label),
            block => (block.Text ?? string.Empty).Contains("Held, D47 listens", StringComparison.Ordinal));

        host.Close();
    }

    /// <summary>The tooltip carries the way out to the web page too, not just the words.</summary>
    [AvaloniaFact]
    public void TheTooltipOffersTheHelpPage()
    {
        var host = Open();

        var tip = Assert.IsType<Border>(ToolTip.GetTip(Label(host, "listening.pushToTalkKey")));
        var link = ((StackPanel)tip.Child!).Children.OfType<Button>().Single();

        Assert.Equal("Help", link.Content);

        host.Close();
    }

    /// <summary>Tabbing to the label opens the same tooltip the pointer would, and leaving it closes it.</summary>
    [AvaloniaFact]
    public void FocusOpensTheSameTooltipHoverWould()
    {
        var host = Open();

        var label = Label(host, "listening.pushToTalkKey");
        var another = Label(host, "listening.cancelHotkey");

        Assert.False(ToolTip.GetIsOpen(label));

        label.Focus();
        Jobs();
        Assert.True(ToolTip.GetIsOpen(label));

        another.Focus();
        Jobs();
        Assert.False(ToolTip.GetIsOpen(label));
        Assert.True(ToolTip.GetIsOpen(another));

        host.Close();
    }

    /// <summary>A search that only the help answers still brings the help back out.</summary>
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

    /// <summary>
    /// A two-sentence help string stays inside the tooltip rather than running off the window —
    /// measured on the drawn control, not by reading its <c>TextWrapping</c> and <c>MaxWidth</c>
    /// properties back.
    /// </summary>
    [AvaloniaFact]
    public void ATwoSentenceHelpStringWrapsInsideTheTooltip()
    {
        var host = Open();

        var tip = Assert.IsType<Border>(ToolTip.GetTip(Label(host, "listening.pushToTalkKey")));
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
