using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Credits and Codex tables take the COLUMNS control and keep what it stores (#2522).</summary>
[Trait("Category", "Integration")]
public sealed class TheCreditsAndCodexTablesAreArrangedTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-10T20:00:00Z", CultureInfo.InvariantCulture);

    private static readonly CodexTable Table = CodexTable.Parse(
        "1\tAlpha tree\tBiological and Geological\tOrganic structures\t18:50\n"
        + "2\tBeta tree\tBiological and Geological\tOrganic structures\t18:30\n"
        + "3\tGamma tree\tBiological and Geological\tGeology\t18:10\n");

    private static readonly string[] CreditLines =
    [
        """{"timestamp":"2026-10-09T09:00:00Z","event":"LoadGame","Credits":1000000}""",
        """{"timestamp":"2026-10-09T10:00:00Z","event":"MarketSell","TotalSale":4812000}""",
        """{"timestamp":"2026-10-09T11:00:00Z","event":"MarketBuy","TotalCost":3120400}""",
        """{"timestamp":"2026-10-09T13:00:00Z","event":"Repair","Cost":5000}""",
    ];

    private static string Codex(string time, long id, string sub) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id
        + ";\",\"Name_Localised\":\"Entry " + id + "\",\"Category\":\"$Codex_Category_X;\",\"Category_Localised\":\"Biological and Geological\""
        + ",\"SubCategory\":\"$Codex_SubCategory_X;\",\"SubCategory_Localised\":\"" + sub + "\",\"Region\":\"$Codex_RegionName_18"
        + ";\",\"System\":\"Sol\",\"SystemAddress\":1,\"BodyID\":5,\"IsNewEntry\":true}";

    private static readonly string[] CodexLines =
    [
        "{\"timestamp\":\"2026-10-01T09:00:00Z\",\"event\":\"LoadGame\"}",
        Codex("2026-10-02T10:00:00Z", 2, "Organic structures"),
        Codex("2026-10-03T10:00:00Z", 3, "Geology"),
        "{\"timestamp\":\"2026-10-04T10:00:00Z\",\"event\":\"SellExplorationData\"}",
    ];

    private sealed record Spec(string Id, string Head, string[] Defaults, string Remove, string Move, string? Add, Func<ColumnArrangementMemory, bool, Control> Make);

    private static readonly Spec[] Specs =
    [
        new(
            CreditsPage.TableId,
            "CreditsHead",
            ["Kind", "In", "Out", "Entries", "Open"],
            "Out",
            "Entries",
            "Events",
            (memory, headset) => new CreditsPage(
                () => Credits(), () => HistoryState.Done, () => Now, new CreditsChoice(), new PanelNavigator(), headset, null, memory)),
        new(
            CreditTransactionsPage.TableId,
            "CreditsTxHead",
            ["Time", "Kind", "Amount", "Balance"],
            "Amount",
            "Balance",
            null,
            (memory, headset) => new CreditTransactionsPage(
                () => Credits(), () => HistoryState.Done, () => Now, new CreditsChoice(), null, headset, null, memory)),
        new(
            CodexPage.TableId,
            "CodexHead",
            ["Region", "New", "Logged", "Entries", "Discoveries", "Open"],
            "New",
            "Discoveries",
            null,
            (memory, headset) => new CodexPage(
                () => Codexed(), () => HistoryState.Done, () => Now, Everything(), new PanelNavigator(), headset, null, memory)),
        new(
            CodexRegionPage.TableId,
            "CodexEntryHead",
            ["Picture", "Entry", "System", "Time", "Status"],
            "System",
            "Status",
            null,
            (memory, headset) => new CodexRegionPage(
                () => Codexed(), () => HistoryState.Done, () => Now, Everything(), 18, headset, null, Table, null, null, memory)),
    ];

    public static TheoryData<string, bool> EveryTable()
    {
        var data = new TheoryData<string, bool>();

        foreach (var headset in new[] { false, true })
        {
            foreach (var spec in Specs)
            {
                data.Add(spec.Id, headset);
            }
        }

        return data;
    }

    private static CommanderGameState Credits() => JournalScript.Commander("F735466", "John Deparagon", CreditLines);

    private static CommanderGameState Codexed() => JournalScript.Commander("F735466", "John Deparagon", CodexLines);

    private static CodexChoice Everything() => new() { Period = (int)CodexPeriod.Everything };

    private static ViewStateStore Views() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static T Named<T>(Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private static void Press(Visual root, string name)
    {
        var button = Named<Button>(root, name);
        Assert.True(button.IsEffectivelyEnabled, $"{name} is disabled.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static string[] Keys(Visual root, string id) =>
        [.. root.GetVisualDescendants().OfType<TableGrid>().First(grid => grid.Layout.Id == id && grid.IsHeader).Layout.Columns.Select(column => column.Key)];

    private static List<string?> Texts(Visual root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text)];

    [AvaloniaTheory]
    [MemberData(nameof(EveryTable))]
    public void WithNothingStoredEachTableDrawsItsOwnColumnsAndOffersTheColumnsTile(string id, bool headset)
    {
        var spec = Specs.Single(known => known.Id == id);
        var (window, page) = PageHost.Open(() => spec.Make(new ColumnArrangementMemory(Views(), headset), headset));

        Assert.Equal(spec.Defaults, Keys(page, id));
        Assert.NotNull(Named<Button>(page, ColumnArranger.NameOf(id, "Tile")));

        window.Close();
    }

    [AvaloniaTheory]
    [MemberData(nameof(EveryTable))]
    public void AnAddedMovedAndRemovedColumnIsKeptWhenThePageOpensAgain(string id, bool headset)
    {
        var views = Views();
        var spec = Specs.Single(known => known.Id == id);
        var (window, page) = PageHost.Open(() => spec.Make(new ColumnArrangementMemory(views, headset), headset));

        Press(page, ColumnArranger.NameOf(id, "Tile"));
        Press(page, $"{spec.Head}.Choose.{spec.Remove}");
        Press(page, ColumnArranger.NameOf(id, "Remove"));
        Press(page, $"{spec.Head}.Choose.{spec.Move}");
        Press(page, ColumnArranger.NameOf(id, "MoveLeft"));

        if (spec.Add is { } add)
        {
            Press(page, ColumnArranger.NameOf(id, "Add"));
            Named<CheckBox>(page, ColumnArranger.NameOf(id, $"Show.{add}")).IsChecked = true;
            Dispatcher.UIThread.RunJobs();
        }

        Press(page, ColumnArranger.NameOf(id, "Done"));

        var arranged = Keys(page, id);
        Assert.DoesNotContain(spec.Remove, arranged);
        Assert.True(Array.IndexOf(arranged, spec.Move) < Array.IndexOf(spec.Defaults, spec.Move));

        if (spec.Add is { } added)
        {
            Assert.Contains(added, arranged);
        }

        window.Close();

        var (againWindow, again) = PageHost.Open(() => spec.Make(new ColumnArrangementMemory(views, headset), headset));
        Assert.Equal(arranged, Keys(again, id));

        Press(again, ColumnArranger.NameOf(id, "Tile"));
        Press(again, ColumnArranger.NameOf(id, "Default"));
        Assert.Equal(spec.Defaults, Keys(again, id));
        againWindow.Close();
    }

    [AvaloniaFact]
    public void ACreditKindsJournalEventsColumnListsTheEventsOfEachKind()
    {
        var spec = Specs[0];
        var (window, page) = PageHost.Open(() => spec.Make(new ColumnArrangementMemory(Views()), false));

        Assert.DoesNotContain(Texts(page), text => text is not null && text.Contains("MarketSell", StringComparison.Ordinal));

        Press(page, ColumnArranger.NameOf(spec.Id, "Tile"));
        Press(page, ColumnArranger.NameOf(spec.Id, "Add"));
        Named<CheckBox>(page, ColumnArranger.NameOf(spec.Id, "Show.Events")).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Press(page, ColumnArranger.NameOf(spec.Id, "Done"));

        Assert.Contains(Texts(page), text => text is not null && text.Contains("MarketSell", StringComparison.Ordinal));

        window.Close();
    }

    [AvaloniaFact]
    public void ASortFromAHeaderFollowsItsColumnWhenItMoves()
    {
        var spec = Specs[0];
        var (window, page) = PageHost.Open(() => spec.Make(new ColumnArrangementMemory(Views()), false));

        List<string?> Order() =>
            [.. page.GetVisualDescendants().OfType<Button>().Where(button => button.Name?.StartsWith("CreditsKind.", StringComparison.Ordinal) == true).Select(button => button.Name)];

        var byDefault = Order();
        Press(page, $"{spec.Head}.Entries");
        var sorted = Order();
        Assert.Contains(Texts(page), text => text is not null && text.StartsWith("ENTRIES", StringComparison.Ordinal) && text.EndsWith('▼'));

        Press(page, ColumnArranger.NameOf(spec.Id, "Tile"));
        Press(page, $"{spec.Head}.Choose.Entries");
        Press(page, ColumnArranger.NameOf(spec.Id, "MoveLeft"));
        Press(page, ColumnArranger.NameOf(spec.Id, "MoveLeft"));
        Press(page, ColumnArranger.NameOf(spec.Id, "Done"));

        Assert.Equal(sorted, Order());
        Assert.Contains(Texts(page), text => text is not null && text.StartsWith("ENTRIES", StringComparison.Ordinal) && text.EndsWith('▼'));
        Assert.Equal("Entries", Keys(page, spec.Id)[1]);
        Assert.Equal(byDefault.Count, sorted.Count);

        window.Close();
    }
}
