using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Activities;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Combat stats and Activities tables take the COLUMNS control and keep what it stores (#2523).</summary>
[Trait("Category", "Integration")]
public sealed class TheCombatStatsAndActivitiesTablesAreArrangedTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-10T20:00:00Z", CultureInfo.InvariantCulture);

    private static readonly string[] KillLines =
    [
        """{"timestamp":"2026-10-10T09:00:00Z","event":"FSDJump","StarSystem":"Sol","SystemAddress":10}""",
        """{"timestamp":"2026-10-10T10:00:00Z","event":"Bounty","Target":"eagle","TotalReward":1000,"VictimFaction":"Pirates","Rewards":[{"Faction":"Alpha","Reward":1000}]}""",
        """{"timestamp":"2026-10-10T10:01:00Z","event":"Bounty","Target":"anaconda","TotalReward":1240000,"VictimFaction":"Raiders","Rewards":[{"Faction":"Beta","Reward":1240000}]}""",
        """{"timestamp":"2026-10-10T10:02:00Z","event":"FactionKillBond","Reward":5000,"AwardingFaction":"Holder","VictimFaction":"Losers"}""",
    ];

    private sealed record Spec(string Id, string Head, string[] Defaults, string? Remove, string Move, Func<ColumnArrangementMemory, bool, Control> Make);

    private static readonly Spec[] Specs =
    [
        new(
            CombatStatsPage.TableId,
            "CombatStatsHead",
            ["Name", "Kills", "Credits"],
            "Kills",
            "Credits",
            (memory, headset) => Combat(memory, headset, grouping: 0)),
        new(
            CombatStatsPage.PaidTableId,
            "CombatStatsPaidHead",
            ["Name", "Credits"],
            null,
            "Credits",
            (memory, headset) => Combat(memory, headset, grouping: 3)),
        new(
            ActivitiesPage.TableId,
            "ActivitiesHead",
            ["Activity", "LastDone", "Age"],
            null,
            "LastDone",
            (memory, _) => new ActivitiesPage(Ledger(), () => "F1", () => Now, null, false, memory)),
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

    private static CombatStatsPage Combat(ColumnArrangementMemory memory, bool headset, int grouping)
    {
        var state = JournalScript.Commander("F735466", "John Deparagon", KillLines);
        var page = new CombatStatsPage(() => state, () => HistoryState.Done, () => Now, headset, null, null, memory);
        page.Tag = grouping;

        return page;
    }

    private static void Group(Control page, int grouping)
    {
        page.GetVisualDescendants().OfType<Segment>().Single(segment => segment.Name == "CombatGrouping")
            .GetVisualDescendants().OfType<RadioButton>().ElementAt(grouping).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private static ActivityLedger Ledger()
    {
        var ledger = new ActivityLedger(new MemoryFileSystem(), NullLogger.Instance);

        ledger.Apply(
        [
            Event("2026-04-01T08:00:00Z", "Commander", ",\"FID\":\"F1\",\"Name\":\"Jameson\""),
            Event("2026-09-23T10:00:00Z", "MiningRefined"),
            Event("2026-08-18T10:00:00Z", "SAAScanComplete"),
            Event("2026-10-05T10:00:00Z", "MarketBuy"),
        ],
            "F1");

        return ledger;
    }

    private static JournalEvent Event(string at, string kind, string extra = "")
    {
        Assert.True(JournalEvent.TryParse($"{{\"timestamp\":\"{at}\",\"event\":\"{kind}\"{extra}}}", NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static ViewStateStore Views() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static (Window Window, Control Page) Open(Spec spec, ColumnArrangementMemory memory, bool headset, double width = 1280)
    {
        var (window, page) = PageHost.Open(() => spec.Make(memory, headset), width);

        if (page is CombatStatsPage { Tag: int grouping and > 0 })
        {
            Group(page, grouping);
            Dispatcher.UIThread.RunJobs();
        }

        return (window, page);
    }

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

    [AvaloniaTheory]
    [MemberData(nameof(EveryTable))]
    public void WithNothingStoredEachTableDrawsItsOwnColumnsAndOffersTheColumnsTile(string id, bool headset)
    {
        var spec = Specs.Single(known => known.Id == id);
        var (window, page) = Open(spec, new ColumnArrangementMemory(Views(), headset), headset);

        Assert.Equal(spec.Defaults, Keys(page, id));
        Assert.NotNull(Named<Button>(page, ColumnArranger.NameOf(id, "Tile")));

        window.Close();
    }

    [AvaloniaTheory]
    [MemberData(nameof(EveryTable))]
    public void AMovedAndRemovedColumnIsKeptWhenThePageOpensAgain(string id, bool headset)
    {
        var views = Views();
        var spec = Specs.Single(known => known.Id == id);
        var (window, page) = Open(spec, new ColumnArrangementMemory(views, headset), headset);

        Press(page, ColumnArranger.NameOf(id, "Tile"));

        if (spec.Remove is { } remove)
        {
            Press(page, $"{spec.Head}.Choose.{remove}");
            Press(page, ColumnArranger.NameOf(id, "Remove"));
        }

        Press(page, $"{spec.Head}.Choose.{spec.Move}");
        Press(page, ColumnArranger.NameOf(id, "MoveLeft"));
        Press(page, ColumnArranger.NameOf(id, "Done"));

        var arranged = Keys(page, id);
        Assert.NotEqual(spec.Defaults, arranged);

        if (spec.Remove is { } removed)
        {
            Assert.DoesNotContain(removed, arranged);
        }

        Assert.True(Array.IndexOf(arranged, spec.Move) < Array.IndexOf(spec.Defaults, spec.Move));
        window.Close();

        var (againWindow, again) = Open(spec, new ColumnArrangementMemory(views, headset), headset);
        Assert.Equal(arranged, Keys(again, id));

        Press(again, ColumnArranger.NameOf(id, "Tile"));
        Press(again, ColumnArranger.NameOf(id, "Default"));
        Assert.Equal(spec.Defaults, Keys(again, id));
        againWindow.Close();
    }

    [AvaloniaFact]
    public void AnEnemyShipSortFollowsItsColumnWhenItMoves()
    {
        var spec = Specs[0];
        var (window, page) = Open(spec, new ColumnArrangementMemory(Views()), false);

        List<string?> Names() =>
            [.. page.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Text is "Eagle" or "Anaconda" or CombatStatsPage.BondText).Select(block => block.Text)];

        var byDefault = Names();
        Press(page, $"{spec.Head}.Enemyship");
        var sorted = Names();
        Assert.NotEqual(byDefault, sorted);

        Press(page, ColumnArranger.NameOf(spec.Id, "Tile"));
        Press(page, $"{spec.Head}.Choose.Name");
        Press(page, ColumnArranger.NameOf(spec.Id, "MoveRight"));
        Press(page, ColumnArranger.NameOf(spec.Id, "MoveRight"));
        Press(page, ColumnArranger.NameOf(spec.Id, "Done"));

        Assert.Equal("Name", Keys(page, spec.Id)[^1]);
        Assert.Equal(sorted, Names());

        window.Close();
    }

    [AvaloniaFact]
    public void TheFirstCombatColumnIsNamedForTheGrouping()
    {
        var (window, page) = Open(Specs[0], new ColumnArrangementMemory(Views()), false);

        Assert.Contains(page.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "Enemy ship ▲" || block.Text == "ENEMY SHIP");
        Assert.Equal("Name", Keys(page, CombatStatsPage.TableId)[0]);

        Group(page, 2);

        Assert.Equal("Name", Keys(page, CombatStatsPage.TableId)[0]);
        Assert.NotNull(Named<Button>(page, "CombatStatsHead.System"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheActivitiesNeverDoneCellSpansLastDoneAndAgeWhereverTheyAre()
    {
        var spec = Specs[2];
        var (window, page) = Open(spec, new ColumnArrangementMemory(Views()), false);

        Press(page, ColumnArranger.NameOf(spec.Id, "Tile"));
        Press(page, $"{spec.Head}.Choose.LastDone");
        Press(page, ColumnArranger.NameOf(spec.Id, "MoveLeft"));
        Press(page, ColumnArranger.NameOf(spec.Id, "Done"));

        Assert.Equal(["LastDone", "Age", "Activity"], Keys(page, spec.Id));
        Assert.Contains(
            page.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text?.StartsWith("Not in your journals", StringComparison.Ordinal) == true && Grid.GetColumnSpan(block) == 2);

        window.Close();
    }

    [AvaloniaFact]
    public void ASortFromAnActivitiesHeaderFollowsItsColumnWhenItMoves()
    {
        var spec = Specs[2];
        var (window, page) = Open(spec, new ColumnArrangementMemory(Views()), false);

        List<string?> Order() =>
            [.. page.GetVisualDescendants().OfType<Border>().Select(row => AutomationProperties.GetName(row)).Where(name => !string.IsNullOrEmpty(name))];

        var byDefault = Order();
        Press(page, $"{spec.Head}.Activity");
        var sorted = Order();
        Assert.NotEqual(byDefault, sorted);

        Press(page, ColumnArranger.NameOf(spec.Id, "Tile"));
        Press(page, $"{spec.Head}.Choose.Activity");
        Press(page, ColumnArranger.NameOf(spec.Id, "MoveRight"));
        Press(page, ColumnArranger.NameOf(spec.Id, "Done"));

        Assert.Equal("Activity", Keys(page, spec.Id)[^1]);
        Assert.Equal(sorted, Order());

        window.Close();
    }

    [AvaloniaFact]
    public void TheNarrowActivitiesLayoutDrawsOneColumnAndNoColumnsTile()
    {
        var spec = Specs[2];
        var (window, page) = Open(spec, new ColumnArrangementMemory(Views()), false, width: 600);

        Assert.Equal(["Activity"], Keys(page, spec.Id));
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), button => button.Name == ColumnArranger.NameOf(spec.Id, "Tile"));

        window.Close();
    }
}
