using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Conversation;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>Captures of the spend breakdown page in each theme, the headset and its two odd windows, and the SPEND dialog's tile (#2562).</summary>
[Trait("Category", "Integration")]
public class TheSpendBreakdownIsDrawnOnTheKitTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static T Named<T>(Avalonia.Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    /// <summary>A week of every kind of charge: steps, cache and web searches, spoken lines, transcription and charges with no rate.</summary>
    private static SpendLedger Varied(string folder)
    {
        var ledger = SpendBreakdownOpensFromSpendTests.Ledger(folder, Noon);
        var minutes = 0;

        void Charge(SpendEntry entry) => ledger.Append(entry with { At = Noon.AddMinutes(-(minutes += 37)) });

        SpendEntry Model(string purpose, decimal dollars, int input, int output, string? step = null, bool priced = true) => new()
        {
            Kind = SpendKind.Model,
            ProviderId = "anthropic",
            Model = "claude-sonnet-5-5",
            Purpose = purpose,
            Step = step,
            Dollars = dollars,
            Priced = priced,
            InputTokens = input,
            OutputTokens = output,
        };

        Charge(Model(SpendPurposes.Conversation, 0.1923m, 48_210, 9_804) with { CacheReadTokens = 12_100, CacheWriteTokens = 2_480 });
        Charge(Model(SpendPurposes.StoryChapter, 0.4210m, 61_000, 7_400, "fidelity") with { CacheWriteTokens = 46_900 });
        Charge(Model(SpendPurposes.StoryChapter, 0.3015m, 30_500, 12_050, "plain"));
        Charge(Model(SpendPurposes.StoryChapter, 0m, 8_200, 1_100, "heading", priced: false));
        Charge(Model(SpendPurposes.WebLookup, 0.0611m, 3_400, 610) with { WebSearchRequests = 3 });
        Charge(Model(SpendPurposes.NpcChatter, 0.0021m, 1_900, 140));
        Charge(Model(SpendPurposes.Conversation, 0m, 2_000, 300, priced: false) with { ProviderId = "openai", Model = "gpt-6.1-sol" });
        Charge(new SpendEntry { Kind = SpendKind.Voice, ProviderId = "elevenlabs", Model = "ElevenLabs", Purpose = SpendPurposes.VoiceAboard, Dollars = 0.0286m, Priced = true, Characters = 938 });
        Charge(new SpendEntry { Kind = SpendKind.Voice, ProviderId = "elevenlabs", Model = "ElevenLabs", Purpose = SpendPurposes.VoiceStoryCast, Dollars = 0.0122m, Priced = true, Characters = 402 });
        Charge(new SpendEntry { Kind = SpendKind.Transcription, ProviderId = "groq", Model = "whisper-large-v3", Purpose = SpendPurposes.Transcription, Dollars = 0.0001m, Priced = true, AudioSeconds = 3.4 });

        return ledger;
    }

    private static void Capture(SpendLedger ledger, string name, bool headset = false, Action<PanelView>? setUp = null)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();
        Jobs();

        _ = panel.Open(new SpendBreakdownPage(ledger, TimeZoneInfo.Utc));
        Jobs();

        if (setUp is not null)
        {
            setUp(panel);
            Jobs();
        }

        Assert.NotNull(window.SaveFrame(name));

        window.Close();
        Jobs();
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    [InlineData(ThemeCatalog.ElitePaletteId)]
    public void ThePageIsCapturedInEachTheme(string themeId)
    {
        using var look = AppLook.PutForCapture(themeId);

        Capture(SpendBreakdownOpensFromSpendTests.Charged("d47-spend-breakdown-capture", Noon), $"spend-breakdown-{themeId}.png");
    }

    [AvaloniaFact]
    public void ThePageIsCapturedInTheHeadset()
    {
        using var look = AppLook.PutForCapture(ThemeCatalog.Elite);

        Capture(SpendBreakdownOpensFromSpendTests.Charged("d47-spend-breakdown-headset", Noon), "spend-breakdown-headset.png", headset: true);
    }

    private static void EveryCharge(PanelView panel)
    {
        var options = Named<Segment>(panel, "SpendSection").GetVisualDescendants().OfType<RadioButton>().ToList();
        options[SpendBreakdownModel.EveryChargeSection].IsChecked = true;
    }

    private static void Choose(PanelView panel, string name)
    {
        var row = Named<Border>(panel, name);
        row.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = row });
    }

    [AvaloniaFact]
    public void EveryChargeIsCapturedInCostOrder()
    {
        using var look = AppLook.PutForCapture(ThemeCatalog.Elite);

        Capture(Varied("d47-spend-charges-cost"), "spend-charges-cost.png", setUp: EveryCharge);
    }

    [AvaloniaFact]
    public void EveryChargeIsCapturedWithSpokenLinesShown()
    {
        using var look = AppLook.PutForCapture(ThemeCatalog.Elite);

        Capture(Varied("d47-spend-charges-spoken"), "spend-charges-spoken.png", setUp: panel =>
        {
            EveryCharge(panel);
            Jobs();
            Named<CheckBox>(panel, "SpendShowSpoken").IsChecked = true;
        });
    }

    [AvaloniaFact]
    public void EveryChargeIsCapturedFilteredToOnePurposeWithUnpricedRowsLast()
    {
        using var look = AppLook.PutForCapture(ThemeCatalog.Elite);

        Capture(Varied("d47-spend-charges-filtered"), "spend-charges-filtered.png", setUp: panel => Choose(panel, $"SpendPurpose.{SpendPurposes.StoryChapter}.Model"));
    }

    [AvaloniaFact]
    public void EveryChargeIsCapturedInTheHeadset()
    {
        using var look = AppLook.PutForCapture(ThemeCatalog.Elite);

        Capture(Varied("d47-spend-charges-headset"), "spend-charges-headset.png", headset: true, setUp: EveryCharge);
    }

    [AvaloniaFact]
    public void AWindowWhereEveryRowIsNotRecordedIsCaptured()
    {
        using var look = AppLook.PutForCapture(ThemeCatalog.Elite);

        var ledger = SpendBreakdownOpensFromSpendTests.Ledger("d47-spend-breakdown-unrecorded", Noon);

        ledger.Append(new SpendEntry { At = Noon.AddDays(-3), Kind = SpendKind.Model, ProviderId = "anthropic", Model = "claude-opus-5", Dollars = 1.4180m, Priced = true });
        ledger.Append(new SpendEntry { At = Noon.AddDays(-2), Kind = SpendKind.Voice, ProviderId = "elevenlabs", Model = "ElevenLabs", Dollars = 0.0610m, Priced = true, Characters = 3_400 });

        Capture(ledger, "spend-breakdown-not-recorded.png");
    }

    [AvaloniaFact]
    public void AWindowWithNothingChargedIsCaptured()
    {
        using var look = AppLook.PutForCapture(ThemeCatalog.Elite);

        Capture(SpendBreakdownOpensFromSpendTests.Ledger("d47-spend-breakdown-nothing", Noon), "spend-breakdown-nothing.png");
    }

    [AvaloniaFact]
    public void TheSpendDialogIsCapturedWithItsTile()
    {
        using var look = AppLook.PutForCapture(ThemeCatalog.Elite);

        var ledger = SpendBreakdownOpensFromSpendTests.Charged("d47-spend-breakdown-dialog", Noon);
        var dialog = new SpendDialog(
            null,
            new SpendTracker(ledger),
            new D47.Core.Audio.SpeechSpend(),
            ledger,
            TestSurface.Settings().Current,
            TimeZoneInfo.Utc,
            breakdown: () => Task.CompletedTask);

        var window = dialog.Show();
        Jobs();

        Assert.NotNull(window.SaveFrame("spend-dialog-breakdown-tile.png"));

        window.Close();
    }
}
