using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Activities;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Commander tab's tables draw through a TableLayout: header and rows share column edges and content columns fit (#1614).</summary>
public class TheCommanderTabTablesFitTheirColumnsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-10T20:00:00Z", CultureInfo.InvariantCulture);

    private static readonly string[] CreditLines =
    [
        """{"timestamp":"2026-10-09T09:00:00Z","event":"LoadGame","Credits":1000000}""",
        """{"timestamp":"2026-10-09T10:00:00Z","event":"MarketSell","TotalSale":4812000}""",
        """{"timestamp":"2026-10-09T11:00:00Z","event":"MarketBuy","TotalCost":3120400}""",
        """{"timestamp":"2026-10-09T13:00:00Z","event":"Repair","Cost":5000}""",
    ];

    private static readonly string[] CombatLines =
    [
        """{"timestamp":"2026-10-10T09:00:00Z","event":"FSDJump","StarSystem":"Sol","SystemAddress":10}""",
        """{"timestamp":"2026-10-10T10:00:00Z","event":"Bounty","Target":"eagle","TotalReward":1000,"VictimFaction":"Pirates","Rewards":[{"Faction":"Alpha","Reward":1000}]}""",
        """{"timestamp":"2026-10-10T10:01:00Z","event":"Bounty","Target":"anaconda","TotalReward":1240000,"VictimFaction":"Raiders","Rewards":[{"Faction":"Beta","Reward":1240000}]}""",
    ];

    private static readonly string[] BgsLines =
    [
        """{"timestamp":"2026-10-10T17:00:00Z","event":"Docked","StationName":"Hutton Orbital","MarketID":100,"StarSystem":"Alpha","SystemAddress":1,"StationFaction":{"Name":"Alpha Mining"}}""",
        """{"timestamp":"2026-10-10T17:05:00Z","event":"RedeemVoucher","Type":"bounty","Factions":[{"Faction":"Gamma Pirates","Amount":3000}]}""",
        """{"timestamp":"2026-10-10T17:08:00Z","event":"SellExplorationData","TotalEarnings":123456}""",
    ];

    private static readonly string[] CodexLines =
    [
        """{"timestamp":"2026-10-01T09:00:00Z","event":"LoadGame"}""",
        CodexEntry("2026-10-02T10:00:00Z", 1, "Sol"),
        CodexEntry("2026-10-04T10:00:00Z", 2, "Alpha Centauri"),
        CodexEntry("2026-10-05T10:00:00Z", 3, "Beta"),
    ];

    private static readonly CodexTable Table = CodexTable.Parse(
        "11\tAlpha tree\tBiological and Geological\tOrganic structures\t18:50\n"
        + "12\tBeta tree\tBiological and Geological\tOrganic structures\t18:30\n"
        + "13\tGamma tree\tBiological and Geological\tOrganic structures\t18:10\n");

    private static string CodexEntry(string time, long id, string system) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id
        + ";\",\"Name_Localised\":\"Entry " + id + "\",\"Category\":\"$Codex_Category_X;\",\"Category_Localised\":\"Biological and Geological\""
        + ",\"SubCategory\":\"$Codex_SubCategory_Organic_Structures;\",\"SubCategory_Localised\":\"Organic structures\""
        + ",\"Region\":\"$Codex_RegionName_18;\",\"System\":\"" + system + "\",\"SystemAddress\":1,\"BodyID\":5,\"IsNewEntry\":true}";

    private static CommanderGameState State(params string[] lines)
    {
        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state.Apply(parsed!);
        }

        return state;
    }

    private static Window Show(Control page)
    {
        var look = AppLook.Put();
        var window = new Window
        {
            Content = page,
            Width = 1000,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static double Natural(Control child)
    {
        var supplied = TableGrid.GetNaturalWidth(child);

        return double.IsNaN(supplied) ? child.DesiredSize.Width : Math.Max(supplied, child.DesiredSize.Width);
    }

    private static void Fits(Window window, string id, int rowsAtLeast = 1)
    {
        var grids = window.GetVisualDescendants().OfType<TableGrid>().Where(grid => grid.Layout.Id == id).ToList();
        var header = Assert.Single(grids, grid => grid.IsHeader);
        var rows = grids.Where(grid => !grid.IsHeader).ToList();

        Assert.True(rows.Count >= rowsAtLeast, $"{id}: {rows.Count} rows drawn");

        var edges = header.ColumnDefinitions.Select(definition => definition.ActualWidth).ToList();
        var spacing = header.Layout.Spacing;

        foreach (var row in rows)
        {
            Assert.Equal(edges, row.ColumnDefinitions.Select(definition => definition.ActualWidth));
        }

        var columns = header.Layout.Columns;

        for (var column = 0; column < columns.Count; column++)
        {
            if (columns[column].Fit != ColumnFit.Content)
            {
                continue;
            }

            var widest = grids
                .SelectMany(grid => grid.Children.Where(child => Grid.GetColumn(child) == column && Grid.GetColumnSpan(child) == 1 && child is not ColumnGrip))
                .Select(Natural).DefaultIfEmpty(0).Max();

            Assert.True(edges[column] - spacing <= widest + 1, $"{id}.{columns[column].Key} is {edges[column]} wide; its widest cell is {widest}");
        }
    }

    [AvaloniaFact]
    public void TheCreditKindsFitTheirColumns()
    {
        var state = State(CreditLines);
        var window = Show(new CreditsPage(() => state, () => HistoryState.Done, () => Now, new CreditsChoice(), new PanelNavigator()));

        Fits(window, CreditsPage.TableId, 2);
        window.Close();
    }

    [AvaloniaFact]
    public void TheCreditTransactionsFitTheirColumns()
    {
        var state = State(CreditLines);
        var window = Show(new CreditTransactionsPage(() => state, () => HistoryState.Done, () => Now, new CreditsChoice(), null));

        Fits(window, CreditTransactionsPage.TableId, 2);
        window.Close();
    }

    [AvaloniaFact]
    public void TheCodexRegionsFitTheirColumns()
    {
        var state = State(CodexLines);
        var window = Show(new CodexPage(() => state, () => HistoryState.Done, () => Now, new CodexChoice { Period = (int)CodexPeriod.Everything }, new PanelNavigator()));

        Fits(window, CodexPage.TableId);
        window.Close();
    }

    [AvaloniaFact]
    public void TheCodexEntriesFitTheirColumns()
    {
        var state = State(CodexLines);
        var window = Show(new CodexRegionPage(() => state, () => HistoryState.Done, () => Now, new CodexChoice { Period = (int)CodexPeriod.Everything }, 18));

        Fits(window, CodexRegionPage.TableId, 2);
        window.Close();
    }

    [AvaloniaFact]
    public void TheCodexDiscoveriesFitTheirColumns()
    {
        var state = State(CodexLines);
        var window = Show(new CodexDiscoveriesPage(() => state, () => HistoryState.Done, 18, Table));

        Fits(window, CodexDiscoveriesPage.TableId);
        window.Close();
    }

    [AvaloniaFact]
    public void TheCombatStatsFitTheirColumns()
    {
        var state = State(CombatLines);
        var window = Show(new CombatStatsPage(() => state, () => HistoryState.Done, () => Now));

        Fits(window, CombatStatsPage.TableId, 2);
        window.Close();
    }

    [AvaloniaFact]
    public void TheBgsWorkFitsItsColumns()
    {
        var state = State(BgsLines);
        var window = Show(new BgsWorkPage(() => state, () => Now.AddHours(-4), () => Now));

        Fits(window, BgsWorkPage.TableId);
        window.Close();
    }

    [AvaloniaFact]
    public void TheActivitiesFitTheirColumns()
    {
        var ledger = new ActivityLedger(Path.Combine(@"C:\d47-test", "activities.json"), new MemoryFileSystem(), NullLogger.Instance);
        var window = Show(new ActivitiesPage(ledger, () => "F1", () => Now));

        Fits(window, ActivitiesPage.TableId, 2);
        window.Close();
    }

    [AvaloniaFact]
    public void TheCommandersFitTheirColumns()
    {
        var found = new Dictionary<string, CommanderSighting>
        {
            ["F1"] = new("F1", "John Deparagon", Now, "Sol", "Anaconda"),
            ["F2"] = new("F2", "Jameson", Now.AddDays(-3), "Alpha Centauri", "Sidewinder"),
        };
        var roster = new CommanderRoster(() => found, () => 2, () => "F1", () => false, _ => { });
        var window = Show(new CommandersPage(roster));

        Fits(window, CommandersPage.TableId, 2);
        window.Close();
    }

    [AvaloniaFact]
    public void ADraggedCreditKindWidthHoldsWhenThePageIsReopened()
    {
        var memory = new ColumnWidthMemory(new ViewStateStore(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance));
        memory.Remember(CreditsPage.TableId, "In", 300);

        var state = State(CreditLines);
        var window = Show(new CreditsPage(() => state, () => HistoryState.Done, () => Now, new CreditsChoice(), new PanelNavigator(), columnWidths: memory));
        var header = window.GetVisualDescendants().OfType<TableGrid>().Single(grid => grid.IsHeader);

        Assert.Equal(300 + header.Layout.Spacing, header.ColumnDefinitions[1].ActualWidth, 0.5);
        Assert.NotEmpty(header.Children.OfType<ColumnGrip>());
        window.Close();
    }
}
