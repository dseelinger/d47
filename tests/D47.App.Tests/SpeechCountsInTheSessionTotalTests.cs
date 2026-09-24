using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The session figure is model turns plus priced speech, in the PTT row and the Spend window.</summary>
public class SpeechCountsInTheSessionTotalTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private sealed class StoppedClock(DateTimeOffset at) : IWallClock
    {
        public DateTimeOffset UtcNow => at;
    }

    private static readonly D47Settings ElevenAtFiveCents = new()
    {
        Speech = new SpeechSettings
        {
            CharacterPrices = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                [TtsProviderCatalog.ElevenLabsId] = 0.05,
            },
        },
    };

    private static string Money(decimal dollars) => dollars.ToString("C4", CultureInfo.CurrentCulture);

    private static SpendTracker OneTurn()
    {
        var session = new SpendTracker();
        session.Record(new TurnCost(new LlmUsage(1_240, 380, 0, 0), 0.1057m, true), coldPrefixExpected: true);
        return session;
    }

    private static SpendWindow Dialog(SpendTracker session, SpeechSpend speech) =>
        new(
            session.Last,
            session,
            speech,
            new SpendLedger(
                Path.Combine(TempFolders.Create("d47-session-total"), "spend.jsonl"),
                new StoppedClock(Noon),
                NullLogger.Instance),
            ElevenAtFiveCents,
            TimeZoneInfo.Utc);

    private static List<string> Blocks(Window window) =>
        [.. window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static PanelView Panel(SpendTracker session, SpeechSpend speech)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableTurnDetails(
            () => Task.CompletedTask,
            () => SpendWindow.SessionDollars(session, speech, ElevenAtFiveCents),
            () => SpendWindow.SessionDetail(session, speech, ElevenAtFiveCents));
        speech.Recorded += panel.RefreshSessionSpend;

        new Window { Width = 1280, Height = 860, Content = panel }.Show();
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    [AvaloniaFact]
    public void TheSpendWindowHeaderAndSessionRowAddPricedSpeech()
    {
        var speech = new SpeechSpend();
        speech.Record(TtsProviderCatalog.ElevenLabsId, 749);

        var window = Dialog(OneTurn(), speech);
        window.Show();

        // The header figure and the Session row's money cell.
        Assert.Equal(2, Blocks(window).Count(text => text == Money(0.1432m)));

        window.Close();
    }

    [AvaloniaFact]
    public void ThePttRowAddsPricedSpeechAndFollowsEachSpokenLine()
    {
        var speech = new SpeechSpend();
        var panel = Panel(OneTurn(), speech);
        var figure = panel.GetControl<TextBlock>("SessionFigure");

        Assert.Equal(Money(0.1057m), figure.Text);

        speech.Record(TtsProviderCatalog.ElevenLabsId, 749);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Money(0.1432m), figure.Text);
        Assert.Contains("749 characters spoken", ToolTip.GetTip(figure) as string, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void EdgeSpeechLeavesTheModelTotalAlone()
    {
        var speech = new SpeechSpend();
        speech.Record(TtsProviderCatalog.EdgeId, 5_000);

        var panel = Panel(OneTurn(), speech);

        Assert.Equal(Money(0.1057m), panel.GetControl<TextBlock>("SessionFigure").Text);
    }

    [AvaloniaFact]
    public void AResetZeroesThePttRow()
    {
        var session = OneTurn();
        var speech = new SpeechSpend();
        speech.Record(TtsProviderCatalog.ElevenLabsId, 749);
        var panel = Panel(session, speech);

        session.Forget();
        speech.Forget();
        panel.RefreshSessionSpend();

        Assert.Equal(Money(0m), panel.GetControl<TextBlock>("SessionFigure").Text);
    }
}
