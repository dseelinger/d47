using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The turn line's figures, moved somewhere they can be read.
/// </summary>
public class SpendDialogTests
{
    private sealed class StoppedClock(DateTimeOffset at) : IWallClock
    {
        public DateTimeOffset UtcNow { get; } = at;
    }

    private static readonly DateTimeOffset Noon = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private static SpendWindow Dialog(out SpendTracker session)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var root = Path.Combine(TempFolders.Create("d47-spend-dialog"), "spend.jsonl");
        var clock = new StoppedClock(Noon);
        var ledger = new SpendLedger(root, clock, NullLogger.Instance);

        var usage = new LlmUsage(1_240, 380, 0, 18_400);
        session = new SpendTracker(ledger);
        session.Record(new TurnCost(usage, 0.0231m, true), coldPrefixExpected: true, "anthropic", "claude-opus-5");

        var speech = new SpeechSpend();

 // Two providers, as the reported window had: the session voice line then reads "$0.0149 -
        // ElevenLabs 298 ($0.0149), Edge Neural 180 (Edge Neural is free)", which is the longest string this
        // window ever renders.
        speech.Record("elevenlabs", 298);
        speech.Record("edge", 180);

        // A charge from earlier in the month, so the running totals are not all the same figure.
        ledger.Append(new SpendEntry
        {
            At = Noon.AddDays(-9),
            Kind = SpendKind.Model,
            ProviderId = "anthropic",
            Model = "claude-opus-5",
            Dollars = 1.4180m,
            Priced = true,
        });

        ledger.Append(new SpendEntry
        {
            At = Noon.AddDays(-2),
            Kind = SpendKind.Voice,
            ProviderId = "elevenlabs",
            Model = "ElevenLabs",
            Dollars = 0.0610m,
            Priced = true,
            Characters = 3_400,
        });

        return new SpendWindow(
            session.Last,
            session,
            speech,
            ledger,
            TestSurface.Settings().Current,
            TimeZoneInfo.Utc);
    }

    private static string Words(Window window) =>
        string.Join(
            "\n",
            window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty));

    [AvaloniaFact]
    public void NothingInTheWindowScrollsSideways()
    {
        var window = Dialog(out _);
        window.Show();

        var scroller = Assert.Single(window.GetVisualDescendants().OfType<ScrollViewer>());

        // The window sizes to its content's height, so lay it out before measuring anything.
        window.UpdateLayout();

        Assert.Equal(ScrollBarVisibility.Disabled, scroller.HorizontalScrollBarVisibility);

        Assert.True(
            scroller.Extent.Width <= scroller.Viewport.Width + 0.5,
            $"The content is {scroller.Extent.Width:0} wide in a {scroller.Viewport.Width:0} viewport, so it "
            + "scrolls sideways. Every row wraps, so this means the scroller measured them against "
            + "an unbounded width - see #87.");

        window.Close();
    }

 /// <summary>Said once, above everything, rather than as a suffix on each figure.</summary>
    [AvaloniaFact]
    public void TheWindowSaysOnceThatTheFiguresAreEstimates()
    {
        var window = Dialog(out _);
        window.Show();

        var blocks = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        var estimates = blocks.FindIndex(text => text.StartsWith("Estimates.", StringComparison.Ordinal));
        var firstGroup = blocks.FindIndex(text => text == "Now");

        Assert.True(estimates >= 0, "The spend dialog says nothing about the figures being estimates.");
        Assert.True(
            estimates < firstGroup,
            "The estimates line must come before the first figure — a Commander who reads the first "
            + "number and closes the window never reaches a footnote.");

        // The reason, not just the disclaimer.
        Assert.Contains("published rates", blocks[estimates], StringComparison.Ordinal);

        window.Close();
    }

 /// <summary>The money column holds an amount and nothing else.</summary>
    [AvaloniaFact]
    public void TheMoneyColumnCannotBeClipped()
    {
        var window = Dialog(out _);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var amounts = window.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => Grid.GetColumn(block) == 1 && block.TextWrapping == TextWrapping.NoWrap)
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0)
            .ToList();

        Assert.NotEmpty(amounts);

        foreach (var amount in amounts)
        {
            Assert.DoesNotContain("—", amount, StringComparison.Ordinal);
            Assert.DoesNotContain("model", amount, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("voice", amount, StringComparison.OrdinalIgnoreCase);

            // What is left is a figure, optionally marked as a floor for a window holding a model d47 has no
            // rate for.
            Assert.Matches(@"^(≥ )?[^A-Za-z]*[\d][^A-Za-z]*$", amount);
        }

        window.Close();
    }

    [AvaloniaFact]
    public void NoIndividualFigureCarriesAnEstimateSuffix()
    {
        var window = Dialog(out _);
        window.Show();

        Assert.DoesNotContain("(est.)", Words(window), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("est.)", Words(window), StringComparison.OrdinalIgnoreCase);

        window.Close();
    }

    /// <summary>All five windows are named, whether or not anything was spent in them.</summary>
    [AvaloniaFact]
    public void AllFiveRunningTotalsAreListedInTheirTwoGroups()
    {
        var window = Dialog(out _);
        window.Show();

        var blocks = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        foreach (var name in new[] { "Today", "This week", "Last 7 days", "This month", "Last 30 days" })
        {
            Assert.Contains(name, blocks);
        }

        // Today is in the first group with the turn and the session, and every other window is below the
        // second heading.
        var now = blocks.IndexOf("Now");
        var running = blocks.IndexOf("Running totals");

        Assert.True(now >= 0 && running > now);
        Assert.InRange(blocks.IndexOf("Turn"), now, running);
        Assert.InRange(blocks.IndexOf("Session"), now, running);
        Assert.InRange(blocks.IndexOf("Today"), now, running);

        // The pairs, in order, and adjacent — which is the ask.
        Assert.Equal(
            ["This week", "Last 7 days", "This month", "Last 30 days"],
            blocks.Skip(running).Where(text =>
                text is "This week" or "Last 7 days" or "This month" or "Last 30 days"));

        window.Close();
    }

    /// <summary>
    /// The figures the line stopped carrying are all here — including the token counts, which were the
    /// bulk of what made it a wall.
    /// </summary>
    [AvaloniaFact]
    public void TheTurnsFiguresSurvivedTheMove()
    {
        var window = Dialog(out _);
        window.Show();

        var text = Words(window);

        Assert.Contains(" in,", text, StringComparison.Ordinal);
        Assert.Contains(" out,", text, StringComparison.Ordinal);
        Assert.Contains("cached", text, StringComparison.Ordinal);
        Assert.Contains("turn", text, StringComparison.OrdinalIgnoreCase);

        // Voice is reported beside the model rather than on a surface of its own, so "what has this cost"
        // keeps having one answer.
        Assert.Contains("characters spoken", text, StringComparison.Ordinal);
        Assert.Contains("ElevenLabs", text, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>The link is a property of the surface, like the settings tab and the search box.</summary>
    [AvaloniaFact]
    public void OnlyASurfaceThatWasGivenSomewhereToShowThemOffersTheLink()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var headset = new D47.App.Panel.PanelView { DataContext = new D47.App.Panel.PanelViewModel() };
        var window = new Window { Content = headset, Width = 900, Height = 700 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(headset.FindControl<Control>("TurnDetails")!.IsVisible);

        var asked = 0;
        headset.EnableTurnDetails(() => asked++);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var link = headset.FindControl<Control>("TurnDetails")!;
        Assert.True(link.IsVisible);

        link.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, asked);

        window.Close();
    }

    /// <summary>The short line and its link, for a human to look at.</summary>
    [AvaloniaFact]
    public void TheShortTurnLineRendersToACapture()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var model = new D47.App.Panel.PanelViewModel { TurnLine = "Answered via Model, effort Medium — $0.0231" };
        var view = new D47.App.Panel.PanelView { DataContext = model };

        view.EnableTurnDetails(() => { });

        var window = new Window { Content = view, Width = 900, Height = 700 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "turn-line-short.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }

    [AvaloniaFact]
    public void TheDialogRendersToACapture()
    {
        var window = Dialog(out _);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "spend-dialog.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }
}
