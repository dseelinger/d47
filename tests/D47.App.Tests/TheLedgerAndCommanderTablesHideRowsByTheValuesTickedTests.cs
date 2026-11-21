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
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>The ledger and Commander tables list the values of their columns in the FILTER bar, and unticking one hides its rows (#2633).</summary>
[Trait("Category", "Integration")]
public class TheLedgerAndCommanderTablesHideRowsByTheValuesTickedTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-10T20:00:00Z", CultureInfo.InvariantCulture);

    private static readonly CodexTable Small = CodexTable.Parse(
        "101\tAlpha One\tBiological\tOrganic structures\tAlpha\t18:5\n"
        + "102\tAlpha Two\tBiological\tOrganic structures\tAlpha\t3:4\n");

    private static readonly CodexTree SmallTree = CodexTree.Of(Small);

    private static readonly string[] KillLines =
    [
        """{"timestamp":"2026-10-10T09:00:00Z","event":"FSDJump","StarSystem":"Sol","SystemAddress":10}""",
        """{"timestamp":"2026-10-10T10:00:00Z","event":"Bounty","Target":"eagle","TotalReward":1000,"VictimFaction":"Pirates","Rewards":[{"Faction":"Alpha","Reward":1000}]}""",
        """{"timestamp":"2026-10-10T10:01:00Z","event":"Bounty","Target":"anaconda","TotalReward":1240000,"VictimFaction":"Raiders","Rewards":[{"Faction":"Beta","Reward":1240000}]}""",
        """{"timestamp":"2026-10-10T10:02:00Z","event":"FactionKillBond","Reward":5000,"AwardingFaction":"Holder","VictimFaction":"Losers"}""",
    ];

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

    private static void Untick(Visual root, string table, string key, string value)
    {
        Named<CheckBox>(root, ColumnFilter.ValueName(table, key, 0, value)).IsChecked = false;
        Dispatcher.UIThread.RunJobs();
    }

    private static bool Shows(Visual root, string text) =>
        root.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == text);

    private static string[] KindKeys(Visual page) =>
        [.. page.GetVisualDescendants().OfType<Button>().Where(button => button.Name?.StartsWith("CodexKind.", StringComparison.Ordinal) == true)
            .Select(button => button.Name!["CodexKind.".Length..])];

    [AvaloniaFact]
    public void ACodexKindsStatusListsOnRecordAndMissingAndUntickingMissingHidesThoseKinds()
    {
        var state = JournalScript.Commander(
            "F735466",
            "John Deparagon",
            """{"timestamp":"2026-11-01T10:00:00Z","event":"CodexEntry","EntryID":101,"Name_Localised":"Alpha One","SubCategory_Localised":"Organic structures","Region":"$Codex_RegionName_18;","System":"Jump","IsNewEntry":true}""");
        var table = CodexCataloguePage.KindsTableId;

        var (window, page) = PageHost.Open(() =>
            new CodexCataloguePage(() => state, () => HistoryState.Done, new CodexCatalogueChoice(), SmallTree.Node("organic_structures")!, false, tree: SmallTree));

        Assert.Equal(["101", "102"], KindKeys(page).Order(StringComparer.Ordinal));

        Press(page, ColumnFilter.NameOf(table, "Tile"));
        Press(page, "CodexKindHead.Filter.Status");
        Named<CheckBox>(page, ColumnFilter.ValueName(table, "Status", 0, "On record"));
        Untick(page, table, "Status", "Missing");

        Assert.Equal(["101"], KindKeys(page));
        window.Close();
    }

    [AvaloniaFact]
    public void ACombatTablesNameListsShipOrOnFootAndUntickingShipHidesTheShips()
    {
        var state = JournalScript.Commander("F735466", "John Deparagon", KillLines);
        var table = CombatStatsPage.TableId;

        var (window, page) = PageHost.Open(() => new CombatStatsPage(() => state, () => HistoryState.Done, () => Now));

        Assert.True(Shows(page, "Eagle"));
        Assert.True(Shows(page, "Anaconda"));
        Assert.True(Shows(page, CombatStatsPage.BondText));

        Press(page, ColumnFilter.NameOf(table, "Tile"));
        Press(page, "CombatStatsHead.Filter.Name");
        Untick(page, table, "Name", "Ship");

        Assert.False(Shows(page, "Eagle"));
        Assert.False(Shows(page, "Anaconda"));
        Assert.True(Shows(page, CombatStatsPage.BondText));
        window.Close();
    }

    [AvaloniaFact]
    public void AnEngineersStatusListsItsWordsAndTheFilterIsClearedWhenThePageOpensAgain()
    {
        var state = EngineersTabTests.State();
        var root = TestSurface.MemoryFolder("d47-engineers-filter-tests");
        var unlocks = PlanBench.Over(root, () => state).Unlocks;
        var source = new EngineerSource(unlocks.Report);
        var table = EngineerDirectoryPage.TableId;

        var (window, page) = PageHost.Open(() => new EngineerDirectoryPage(source, new PanelNavigator()));

        bool Lists(string name) =>
            page.GetVisualDescendants().OfType<Button>().Any(button => AutomationProperties.GetName(button) == name);

        Assert.True(Lists("Liz Ryder"));

        Press(page, ColumnFilter.NameOf(table, "Tile"));
        Press(page, EngineerDirectoryPage.HeadName + ".Filter.Status");
        Untick(page, table, "Status", "Unlocked at grade 5");

        Assert.False(Lists("Liz Ryder"));
        Assert.True(Lists("Felicity Farseer"));

        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        window.Content = page;
        Dispatcher.UIThread.RunJobs();

        Assert.True(Lists("Liz Ryder"));
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), button => button.Name == ColumnFilter.NameOf(table, "HeadClear"));
        window.Close();
    }
}
