using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
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
/// The turn line's figures, moved somewhere they can be read
/// (docs/plans/change-requests.md item 2).
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
        speech.Record("elevenlabs", 412);

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

    /// <summary>
    /// All four windows are named, whether or not anything was spent in them. A window that is
    /// silent because it is empty and one that is silent because it was not computed look the
    /// same, and only one of those is acceptable.
    /// </summary>
    [AvaloniaFact]
    public void AllFourRunningTotalsAreListed()
    {
        var window = Dialog(out _);
        window.Show();

        var text = Words(window);

        Assert.Contains("Last 7 days", text, StringComparison.Ordinal);
        Assert.Contains("Last 30 days", text, StringComparison.Ordinal);
        Assert.Contains("This week", text, StringComparison.Ordinal);
        Assert.Contains("This month", text, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    /// The figures the line stopped carrying are all here — including the token counts, which
    /// were the bulk of what made it a wall.
    /// </summary>
    [AvaloniaFact]
    public void TheTurnsFiguresSurvivedTheMove()
    {
        var window = Dialog(out _);
        window.Show();

        var text = Words(window);

        Assert.Contains("Input", text, StringComparison.Ordinal);
        Assert.Contains("Output", text, StringComparison.Ordinal);
        Assert.Contains("cached", text, StringComparison.Ordinal);
        Assert.Contains("Turns", text, StringComparison.Ordinal);

        // Voice is reported beside the model rather than on a surface of its own, so "what has
        // this cost" keeps having one answer.
        Assert.Contains("Voice", text, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    /// The link is a property of the surface, like the settings tab and the search box. The
    /// headset's copy is handed nothing and therefore structurally cannot open a desktop dialog,
    /// rather than merely being unlikely to be clicked in mid-air.
    /// </summary>
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
