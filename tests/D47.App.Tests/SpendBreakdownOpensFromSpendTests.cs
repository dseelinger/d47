using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>SPEND BREAKDOWN in the SPEND dialog's footer closes the dialog and opens the page on the panel (#2562).</summary>
public class SpendBreakdownOpensFromSpendTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private sealed class StoppedClock(DateTimeOffset at) : IWallClock
    {
        public DateTimeOffset UtcNow { get; } = at;
    }

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static T Named<T>(Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private static List<string> Words(Visual root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Empty)];

    internal static SpendLedger Ledger(string folder, DateTimeOffset now)
    {
        var ledger = new SpendLedger(
            Path.Combine(TestSurface.MemoryFolder(folder), "spend.jsonl"), new MemoryFileSystem(), new StoppedClock(now), NullLogger.Instance);

        return ledger;
    }

    /// <summary>A month of charges: priced, partly priced, unpriced, a voice slot, one with no purpose, one free and one too old.</summary>
    internal static SpendLedger Charged(string folder, DateTimeOffset now)
    {
        var ledger = Ledger(folder, now);

        void Charge(int daysAgo, SpendKind kind, string? purpose, decimal dollars, bool priced = true) =>
            ledger.Append(new SpendEntry
            {
                At = now.AddDays(-daysAgo),
                Kind = kind,
                ProviderId = kind == SpendKind.Voice ? "elevenlabs" : "anthropic",
                Model = kind == SpendKind.Voice ? "ElevenLabs" : "claude-sonnet-5-5",
                Purpose = purpose,
                Dollars = dollars,
                Priced = priced,
                Characters = kind == SpendKind.Voice ? 900 : 0,
            });

        Charge(0, SpendKind.Model, SpendPurposes.Conversation, 0.4000m);
        Charge(1, SpendKind.Model, SpendPurposes.Conversation, 0.4000m);
        Charge(2, SpendKind.Model, SpendPurposes.Conversation, 0.4000m);
        Charge(3, SpendKind.Model, SpendPurposes.StoryChapter, 1.0000m);
        Charge(3, SpendKind.Model, SpendPurposes.StoryChapter, 1.1000m);
        Charge(4, SpendKind.Model, SpendPurposes.StoryChapter, 0m, priced: false);
        Charge(5, SpendKind.Voice, SpendPurposes.VoiceAboard, 0.0610m);
        Charge(12, SpendKind.Model, null, 0.4000m);
        Charge(6, SpendKind.Model, SpendPurposes.WebLookup, 0m, priced: false);
        Charge(7, SpendKind.Voice, SpendPurposes.VoiceNpcs, 0m);
        Charge(45, SpendKind.Model, SpendPurposes.Debrief, 9.0000m);

        return ledger;
    }

    private static (Window Window, PanelView Panel, SpendLedger Ledger) Panel(SpendLedger ledger)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableTurnDetails(
            (owner, open) => new SpendDialog(
                null,
                new SpendTracker(ledger),
                new D47.Core.Audio.SpeechSpend(),
                ledger,
                TestSurface.Settings().Current,
                TimeZoneInfo.Utc,
                breakdown: () => open(new SpendBreakdownPage(ledger, TimeZoneInfo.Utc))).Over(owner),
            () => 0m);

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();
        Jobs();

        return (window, panel, ledger);
    }

    [AvaloniaFact]
    public void PressingTheTileClosesTheDialogAndPutsThePageOnThePanelsTrail()
    {
        var (window, panel, _) = Panel(Charged("d47-spend-breakdown-tile", Noon));

        Press(Named<Button>(panel, "TurnDetails"));
        Jobs();

        var dialog = window.GetVisualDescendants().OfType<SpendDialog>().Single();
        var tile = Named<Button>(dialog, "SpendBreakdown");

        Assert.True(tile.IsEffectivelyVisible);

        Press(tile);
        Jobs();

        Assert.True(dialog.Left.IsCompleted, "the dialog closed");
        Assert.DoesNotContain(dialog, window.GetVisualDescendants());
        Assert.Equal("Spend breakdown", panel.Nav.Trail[^1].Word);

        var page = panel.GetVisualDescendants().OfType<SpendBreakdownPage>().Single();
        Assert.True(page.IsEffectivelyVisible);
        Assert.Equal(SpendBreakdownModel.Summary, Named<TextBlock>(panel, "PageSummary").Text);

        Assert.True(panel.GoBack());
        Jobs();

        Assert.DoesNotContain(page, panel.GetVisualDescendants());

        window.Close();
    }

    [AvaloniaFact]
    public void TheTileComesAfterClose()
    {
        var (window, panel, _) = Panel(Ledger("d47-spend-breakdown-footer", Noon));

        Press(Named<Button>(panel, "TurnDetails"));
        Jobs();

        var dialog = window.GetVisualDescendants().OfType<SpendDialog>().Single();
        var close = dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Close"));
        var tile = Named<Button>(dialog, "SpendBreakdown");

        Assert.Same(close.Parent, tile.Parent);

        var row = (StackPanel)tile.Parent!;
        Assert.Equal(row.Children.IndexOf(close) + 1, row.Children.IndexOf(tile));
        Assert.True(tile.Bounds.Height >= TypeScale.MinimumTarget);

        window.Close();
    }

    [AvaloniaFact]
    public void ADialogWithNoPanelToOpenItOnHasNoTile()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var ledger = Ledger("d47-spend-breakdown-none", Noon);
        var dialog = new SpendDialog(null, new SpendTracker(ledger), new D47.Core.Audio.SpeechSpend(), ledger, TestSurface.Settings().Current, TimeZoneInfo.Utc);
        var window = dialog.Show();
        Jobs();

        Assert.DoesNotContain(dialog.GetVisualDescendants().OfType<Button>(), button => button.Name == "SpendBreakdown");

        window.Close();
    }

    [AvaloniaFact]
    public void ThePageOpensOnTheLastThirtyDaysWithEachPurposeMostCostlyFirst()
    {
        var (window, panel, ledger) = Panel(Charged("d47-spend-breakdown-rows", Noon));

        _ = panel.Open(new SpendBreakdownPage(ledger, TimeZoneInfo.Utc));
        Jobs();

        Assert.Equal(SpendBreakdownModel.OpeningWindow, Named<Stepper>(panel, "SpendWindow").SelectedItem);
        Assert.Equal("18 Jul 12:00 – 17 Aug 12:00", Named<TextBlock>(panel, "SpendRange").Text);
        Assert.Equal("9 charges", Named<TextBlock>(panel, "SpendCharges").Text);
        Assert.Equal("≥ " + 3.7610m.ToString("C4", System.Globalization.CultureInfo.CurrentCulture), Named<TextBlock>(panel, "SpendTotal").Text);

        var rows = Named<StackPanel>(panel, "SpendPurposeRows").Children.Select(row => row.Name).ToList();

        Assert.Equal(
            [
                $"SpendPurpose.{SpendPurposes.StoryChapter}.Model",
                $"SpendPurpose.{SpendPurposes.Conversation}.Model",
                "SpendPurpose.none.Model",
                $"SpendPurpose.{SpendPurposes.VoiceAboard}.Voice",
                $"SpendPurpose.{SpendPurposes.WebLookup}.Model",
            ],
            rows);

        var words = Words(panel);
        Assert.Contains("1 with no rate", words);
        Assert.Contains(SpendBreakdownModel.NoRate, words);
        Assert.Contains(SpendBreakdownModel.NotRecordedLine, words);
        Assert.Contains("Aboard", words);

        var options = Named<Segment>(panel, "SpendSection").GetVisualDescendants().OfType<RadioButton>().ToList();
        Assert.Equal(2, options.Count);
        Assert.True(options[0].IsChecked);
        Assert.True(options[1].IsEnabled);
        Assert.Contains(SpendBreakdownModel.PressHint, words);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingCostReversesTheOrderAndKeepsNoRateLast()
    {
        var (window, panel, ledger) = Panel(Charged("d47-spend-breakdown-sort", Noon));

        _ = panel.Open(new SpendBreakdownPage(ledger, TimeZoneInfo.Utc));
        Jobs();

        // The first press chooses Cost, most first, which is the default order; the second reverses it.
        Press(Named<Button>(panel, $"{SpendBreakdownPage.HeadName}.Cost"));
        Jobs();
        Press(Named<Button>(panel, $"{SpendBreakdownPage.HeadName}.Cost"));
        Jobs();

        var rows = Named<StackPanel>(panel, "SpendPurposeRows").Children.Select(row => row.Name).ToList();

        Assert.Equal($"SpendPurpose.{SpendPurposes.VoiceAboard}.Voice", rows[0]);
        Assert.Equal($"SpendPurpose.{SpendPurposes.WebLookup}.Model", rows[^1]);

        window.Close();
    }

    [AvaloniaFact]
    public void SteppingTheWindowRedrawsItsFigures()
    {
        var (window, panel, ledger) = Panel(Charged("d47-spend-breakdown-step", Noon));

        _ = panel.Open(new SpendBreakdownPage(ledger, TimeZoneInfo.Utc));
        Jobs();

        var stepper = Named<Stepper>(panel, "SpendWindow");
        var previous = stepper.GetVisualDescendants().OfType<RepeatButton>().First();

        // Today is the first window and Last 30 days the last.
        for (var i = 0; i < 4; i++)
        {
            Press(previous);
            Jobs();
        }

        Assert.Equal("Today", stepper.SelectedItem);
        Assert.Same(stepper, Named<Stepper>(panel, "SpendWindow"));
        Assert.Equal("1 charge", Named<TextBlock>(panel, "SpendCharges").Text);
        Assert.Equal(0.4m.ToString("C4", System.Globalization.CultureInfo.CurrentCulture), Named<TextBlock>(panel, "SpendTotal").Text);
        Assert.Equal([$"SpendPurpose.{SpendPurposes.Conversation}.Model"], Named<StackPanel>(panel, "SpendPurposeRows").Children.Select(row => row.Name));

        window.Close();
    }

    private static List<string?> ChargeRows(Visual root) =>
        [.. Named<StackPanel>(root, "SpendChargeRows").Children.Select(Cost)];

    /// <summary>A charge row's cost cell, its last.</summary>
    private static string? Cost(Control row) => ((TextBlock)((Grid)((Border)row).Child!).Children[^1]).Text;

    private static void Open(PanelView panel, SpendLedger ledger)
    {
        _ = panel.Open(new SpendBreakdownPage(ledger, TimeZoneInfo.Utc));
        Jobs();
    }

    private static void EveryCharge(Visual root)
    {
        var options = Named<Segment>(root, "SpendSection").GetVisualDescendants().OfType<RadioButton>().ToList();
        options[SpendBreakdownModel.EveryChargeSection].IsChecked = true;
        Jobs();
    }

    /// <summary>Presses a By purpose row from the keyboard, as a row that opens takes Enter.</summary>
    private static void Choose(Visual root, string name)
    {
        var row = Named<Border>(root, name);
        row.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter, Source = row });
        Jobs();
    }

    private static string Money(decimal dollars) => dollars.ToString("C4", System.Globalization.CultureInfo.CurrentCulture);

    [AvaloniaFact]
    public void EveryChargeListsTheWindowsChargesMostCostlyFirstWithoutSpokenLines()
    {
        var (window, panel, ledger) = Panel(Charged("d47-spend-breakdown-every", Noon));

        Open(panel, ledger);
        EveryCharge(panel);

        Assert.Equal(SpendBreakdownModel.EveryChargeSection, Named<Segment>(panel, "SpendSection").SelectedIndex);
        Assert.Equal(
            [Money(1.1m), Money(1.0m), Money(0.4m), Money(0.4m), Money(0.4m), Money(0.4m), SpendBreakdownModel.NoRate, SpendBreakdownModel.NoRate],
            ChargeRows(panel));
        Assert.Equal("8 charges", Named<TextBlock>(panel, "SpendResultsCount").Text);
        Assert.Equal($"Spoken lines are left out: 1 charge, {Money(0.061m)}.", Named<TextBlock>(panel, "SpendResultsHint").Text);
        Assert.Equal("1–8 of 8", Named<TextBlock>(panel, "SpendChargesReadout").Text);
        Assert.False(Named<Button>(panel, SpendBreakdownPage.NextName).IsEnabled);

        Named<CheckBox>(panel, "SpendShowSpoken").IsChecked = true;
        Jobs();

        Assert.Equal(9, ChargeRows(panel).Count);
        Assert.Contains(Money(0.061m), ChargeRows(panel));
        Assert.True(Named<CheckBox>(panel, "SpendShowSpoken").IsChecked);
        Assert.StartsWith("Spoken lines are shown", Named<TextBlock>(panel, "SpendResultsHint").Text, StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingAPurposeListsItsChargesUntilEveryPurposeIsShownAgain()
    {
        var (window, panel, ledger) = Panel(Charged("d47-spend-breakdown-filter", Noon));

        Open(panel, ledger);
        Choose(panel, $"SpendPurpose.{SpendPurposes.StoryChapter}.Model");

        Assert.Equal(SpendBreakdownModel.EveryChargeSection, Named<Segment>(panel, "SpendSection").SelectedIndex);
        Assert.Equal([Money(1.1m), Money(1.0m), SpendBreakdownModel.NoRate], ChargeRows(panel));
        Assert.Equal("3 charges for Story chapters", Named<TextBlock>(panel, "SpendResultsCount").Text);
        Assert.Equal("≥ " + Money(2.1m), Named<TextBlock>(panel, "SpendResultsCost").Text);
        Assert.Contains("1 with no rate", Words(panel));
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<CheckBox>(), box => box.Name == "SpendShowSpoken");

        Press(Named<Button>(panel, "SpendShowEveryPurpose"));
        Jobs();

        Assert.Equal(8, ChargeRows(panel).Count);
        Assert.Equal("8 charges", Named<TextBlock>(panel, "SpendResultsCount").Text);

        window.Close();
    }

    [AvaloniaFact]
    public void AVoicePurposesChargesAreListedWithSpokenLinesUnticked()
    {
        var (window, panel, ledger) = Panel(Charged("d47-spend-breakdown-voice", Noon));

        Open(panel, ledger);
        Choose(panel, $"SpendPurpose.{SpendPurposes.VoiceAboard}.Voice");

        Assert.Equal([Money(0.061m)], ChargeRows(panel));
        Assert.Contains("VOICE", Words(panel));
        Assert.Contains("900 characters", Words(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void OnlyAPageOfAThousandChargesBecomesRows()
    {
        var ledger = Ledger("d47-spend-breakdown-pages", Noon);

        for (var i = 0; i < 1_000; i++)
        {
            ledger.Append(new SpendEntry
            {
                At = Noon.AddMinutes(-i),
                Kind = SpendKind.Model,
                ProviderId = "anthropic",
                Model = "claude-sonnet-5-5",
                Purpose = SpendPurposes.NpcChatter,
                Dollars = 0.0001m * (i + 1),
                Priced = true,
            });
        }

        var (window, panel, _) = Panel(ledger);

        Open(panel, ledger);
        EveryCharge(panel);

        Assert.Equal(100, ChargeRows(panel).Count);
        Assert.Equal(Money(0.1m), ChargeRows(panel)[0]);
        Assert.Equal($"1–100 of {1_000:N0}", Named<TextBlock>(panel, "SpendChargesReadout").Text);
        Assert.False(Named<Button>(panel, SpendBreakdownPage.PreviousName).IsEnabled);

        Press(Named<Button>(panel, SpendBreakdownPage.NextName));
        Jobs();

        Assert.Equal($"101–200 of {1_000:N0}", Named<TextBlock>(panel, "SpendChargesReadout").Text);
        Assert.Equal(Money(0.09m), ChargeRows(panel)[0]);
        Assert.True(Named<Button>(panel, SpendBreakdownPage.PreviousName).IsEnabled);

        // A new sort starts again at the first page; Time's first press is newest first.
        Press(Named<Button>(panel, $"{SpendBreakdownPage.ChargeHeadName}.Time"));
        Jobs();

        Assert.Equal($"1–100 of {1_000:N0}", Named<TextBlock>(panel, "SpendChargesReadout").Text);
        Assert.Equal(Money(0.0001m), ChargeRows(panel)[0]);

        window.Close();
    }

    [AvaloniaFact]
    public void ThePageSaysASubscriptionCanMakeTheRealCostDifferent()
    {
        var (window, panel, ledger) = Panel(Ledger("d47-spend-breakdown-hedge", Noon));
        ledger.Append(new SpendEntry { At = Noon.AddMinutes(-5), Kind = SpendKind.Voice, ProviderId = "elevenlabs", Model = "ElevenLabs", Purpose = SpendPurposes.VoiceAboard, Dollars = 0.03m, Priced = true, Characters = 900 });

        _ = panel.Open(new SpendBreakdownPage(ledger, TimeZoneInfo.Utc));
        Jobs();

        Assert.Contains(SpendBreakdownModel.Hint, Words(panel));
        Assert.Contains(
            "not what your account is billed. A subscription with bundled credits can make the real cost anything from higher to nothing at all.",
            SpendBreakdownModel.Hint,
            StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public void AWindowWithNothingChargedSaysSoInPlaceOfTheTable()
    {
        var (window, panel, ledger) = Panel(Ledger("d47-spend-breakdown-empty", Noon));

        _ = panel.Open(new SpendBreakdownPage(ledger, TimeZoneInfo.Utc));
        Jobs();

        Assert.Equal("Nothing has been charged in the last 30 days.", Named<TextBlock>(panel, "SpendNothing").Text);
        Assert.Equal("0 charges", Named<TextBlock>(panel, "SpendCharges").Text);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Segment>(), segment => segment.Name == "SpendSection");
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<StackPanel>(), stack => stack.Name == "SpendPurposeRows");
        Assert.Contains(SpendBreakdownModel.NothingHint, Words(panel));

        window.Close();
    }
}
