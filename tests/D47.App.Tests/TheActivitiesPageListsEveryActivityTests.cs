using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Activities;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Activities page off the Checklist: every activity, its date, and the Suggest box (#587).</summary>
public sealed class TheActivitiesPageListsEveryActivityTests
{
    private const string Fid = "F1";

    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string at, string kind, string extra = "")
    {
        Assert.True(JournalEvent.TryParse(
            $"{{\"timestamp\":\"{at}\",\"event\":\"{kind}\"{extra}}}",
            NullLogger.Instance,
            out var parsed));

        return parsed!;
    }

    private static ActivityLedger Ledger()
    {
        var ledger = new ActivityLedger(
            Path.Combine(TempFolders.Create("d47-activities-page"), "activities.json"),
            NullLogger.Instance);

        ledger.Apply(
        [
            Event("2026-04-01T08:00:00Z", "Commander", ",\"FID\":\"F1\",\"Name\":\"Jameson\""),
            Event("2026-09-23T10:00:00Z", "MiningRefined"),
            Event("2026-08-18T10:00:00Z", "SAAScanComplete"),
            Event("2025-02-02T10:00:00Z", "Bounty"),
            Event("2026-10-05T10:00:00Z", "MarketBuy"),
        ],
            Fid);

        return ledger;
    }

    private static ChecklistService Checklists()
    {
        var store = new GameStateStore();
        Assert.True(JournalEvent.TryParse(
            """{ "timestamp":"2026-10-01T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
            NullLogger.Instance,
            out var parsed));
        store.Apply(parsed!);

        var paths = new AppPaths(TempFolders.Create("d47-activities-checklist"));
        paths.EnsureCreated();

        return new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => store.Active);
    }

    private static Window Open(ActivityLedger ledger, double width)
    {
        var page = new ActivitiesPage(ledger, () => Fid, () => Now);
        var window = new Window { Content = page, Width = width, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static IReadOnlyList<Border> Rows(Window window) =>
        [.. window.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains(ListRow.Class))];

    private static string RowName(Border row) =>
        row.GetVisualDescendants().OfType<TextBlock>().First().Text!;

    private static CheckBox BoxOf(Border row) => row.GetVisualDescendants().OfType<CheckBox>().Single();

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(TestSurface.CaptureDirectory, name), new PngBitmapEncoderOptions());
    }

    [AvaloniaFact]
    public void EveryActivityHasARowOldestFirstAndUndatedLast()
    {
        using var look = AppLook.Put();
        var window = Open(Ledger(), 1280);
        var rows = Rows(window);

        Assert.Equal(ActivityCatalogue.All.Count, rows.Count);

        var names = rows.Select(RowName).ToList();

        Assert.Equal(["BOUNTY HUNTING", "EXPLORATION", "MINING", "TRADING"], names.Take(4));
        Assert.Equal("COMBAT ZONES", names[4]);

        var words = window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToList();

        Assert.Contains("2 Feb 3311", words);
        Assert.Contains("yesterday", words);
        Assert.Contains("1 year 8 months ago", words);
        Assert.Contains("From your journals since 2 Feb 3311.", words);
        Assert.Contains("Not in your journals since 2 Feb 3311", words);

        window.Close();
    }

    [AvaloniaFact]
    public void UntickingSuggestKeepsTheRowAndItsDate()
    {
        using var look = AppLook.Put();
        var ledger = Ledger();
        var window = Open(ledger, 1280);

        var mining = Rows(window).Single(row => RowName(row) == "MINING");

        BoxOf(mining).IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.False(ledger.IsSuggested("mining", Fid));
        Assert.DoesNotContain(ledger.Stalest(20, Fid), date => date.Key == "mining");

        var again = Rows(window).Single(row => RowName(row) == "MINING");

        Assert.Same(mining, again);
        Assert.Contains(
            again.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "23 Sep 3312" || block.Text == "13 days ago");

        BoxOf(again).IsChecked = true;
        Assert.Contains(ledger.Stalest(20, Fid), date => date.Key == "mining");

        window.Close();
    }

    [AvaloniaFact]
    public void ThePageDrawsAtTheDesignsWidthsAndTheNarrowRowIsTaller()
    {
        using var look = AppLook.Put();
        var wide = Open(Ledger(), 1280);
        var wideRow = Rows(wide)[0];

        Assert.True(wideRow.Bounds.Height >= 44);
        Save(wide, "activities-1280.png");
        wide.Close();

        var narrow = Open(Ledger(), 640);
        var narrowRow = Rows(narrow)[0];

        Assert.True(narrowRow.Bounds.Height >= 52);
        Save(narrow, "activities-640.png");
        narrow.Close();
    }

    [AvaloniaFact]
    public void TheChecklistsActivitiesButtonOpensThePageWithABreadcrumbBack()
    {
        using var look = AppLook.Put();

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableChecklist(Checklists(), activities: Ledger());

        var window = new Window { Content = panel, Width = 1280, Height = 900 };
        window.Show();
        panel.Tab = PanelTab.Commander;
        Dispatcher.UIThread.RunJobs();

        var button = panel.GetVisualDescendants().OfType<Button>()
            .Single(found => found.IsEffectivelyVisible && found.Content as string == "Activities");

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            ActivityCatalogue.All.Count,
            panel.GetVisualDescendants().OfType<Border>().Count(border => border.Classes.Contains(ListRow.Class)));

        Assert.Contains(
            panel.GetVisualDescendants().OfType<Control>(),
            control => control.IsEffectivelyVisible && AutomationProperties.GetName(control)?.StartsWith("Mining, ", StringComparison.Ordinal) == true);

        window.Close();
    }
}
