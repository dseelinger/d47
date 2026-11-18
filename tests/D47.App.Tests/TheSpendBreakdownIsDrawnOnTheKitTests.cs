using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
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

    private static void Capture(SpendLedger ledger, string name, bool headset = false)
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
