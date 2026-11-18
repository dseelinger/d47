using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Catalog;
using D47.Core.Conversation;
using D47.Core.Listening;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>SPEND shows hosted transcription as a provider row in minutes, and counts it in the session (#2554).</summary>
public class TheSpendDialogCountsHostedTranscriptionTests
{
    private sealed class StoppedClock(DateTimeOffset at) : IWallClock
    {
        public DateTimeOffset UtcNow { get; } = at;
    }

    private static readonly DateTimeOffset Noon = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private static SpendDialog Dialog(out TranscriptionSpend transcription, out SpendTracker session)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var root = Path.Combine(TestSurface.MemoryFolder("d47-spend-transcription"), "spend.jsonl");
        var ledger = new SpendLedger(root, new MemoryFileSystem(), new StoppedClock(Noon), NullLogger.Instance);

        session = new SpendTracker(ledger);
        session.Record(
            new TurnCost(new LlmUsage(1_240, 380, 0, 18_400), 0.0231m, true),
            SpendPurposes.Conversation,
            coldPrefixExpected: true,
            "anthropic",
            "claude-opus-5");

        transcription = new TranscriptionSpend(ledger, new ModelCatalogSource(ModelCatalog.Embedded));
        transcription.Record(SttProviderCatalog.Deepgram, TimeSpan.FromSeconds(60));
        transcription.Record(SttProviderCatalog.Deepgram, TimeSpan.FromSeconds(30));

        return new SpendDialog(
            session.Last,
            session,
            new SpeechSpend(),
            ledger,
            TestSurface.Settings().Current,
            TimeZoneInfo.Utc,
            transcription: transcription);
    }

    private static string Words(Control control) =>
        string.Join(
            "\n",
            control.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty));

    [AvaloniaFact]
    public void TheProviderIsARowWithTheMinutesItHeard()
    {
        var dialog = Dialog(out _, out _);
        var window = dialog.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var words = Words(dialog);

        Assert.Contains(SttProviderCatalog.Deepgram.Name, words, StringComparison.Ordinal);
        Assert.Contains($"{1.5:N1} min", words, StringComparison.Ordinal);

        // A transcription provider has no model rows under it, as a voice provider has none.
        Assert.DoesNotContain(SttProviderCatalog.Deepgram.Model!, words, StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public void TheSessionFigureIncludesTranscription()
    {
        var dialog = Dialog(out var transcription, out var session);
        var window = dialog.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var expected = session.RunningTotalDollars + transcription.Dollars;
        var figure = dialog.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "SpendFigure");

        Assert.True(transcription.Dollars > 0m);
        Assert.Equal(expected.ToString("C4"), figure.Text);

        window.Close();
    }

    [AvaloniaFact]
    public void TheDialogWithTranscriptionRendersToACapture()
    {
        var dialog = Dialog(out _, out _);
        var window = dialog.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.SaveCapture("spend-dialog-transcription.png");

        window.Close();
    }
}
