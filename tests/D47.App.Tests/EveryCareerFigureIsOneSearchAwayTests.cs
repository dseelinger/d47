using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Statistics: a sidebar of the sixteen sections, a grid of one, and a search across all (#553).</summary>
public class EveryCareerFigureIsOneSearchAwayTests
{
    private const string Statistics =
        """
        {"event":"Statistics",
         "Bank_Account":{"Current_Wealth":1234567,"Spent_On_Ships":500000},
         "Combat":{"Bounties_Claimed":1842,"Bounty_Hunting_Profit":2104880400,"Combat_Bonds":612},
         "Crime":{"Notoriety":0,"Fines":3},
         "Smuggling":{"Black_Markets_Traded_With":4,"Black_Markets_Profits":41880000},
         "Trading":{"Markets_Traded_With":12,"Market_Profits":902440000},
         "Mining":{"Mining_Profits":1380220000},
         "Exploration":{"Systems_Visited":4120,"Exploration_Profits":4120880000,"Greatest_Distance_From_Start":21345.67},
         "Passengers":{"Passengers_Missions_Delivered":5},
         "Search_And_Rescue":{"SearchRescue_Traded":9,"SearchRescue_Profit":88120000},
         "Squadron":{"Squadron_Bank_Credits_Deposited":0},
         "Crafting":{"Count_Of_Used_Engineers":11},
         "Crew":{"NpcCrew_Hired":2},
         "Multicrew":{"Multicrew_Time_Total":7260},
         "Material_Trader_Stats":{"Trades_Completed":31},
         "FLEETCARRIER":{"FLEETCARRIER_TRADEPROFIT_TOTAL":88000000},
         "Exobiology":{"Organic_Data_Profits":3412880000,"Organic_Species":120}}
        """;

    private static readonly string[] Sections =
    [
        "Bank_Account", "Combat", "Crime", "Smuggling", "Trading", "Mining", "Exploration", "Passengers",
        "Search_And_Rescue", "Squadron", "Crafting", "Crew", "Multicrew", "Material_Trader_Stats",
        "FLEETCARRIER", "Exobiology",
    ];

    private static JournalEvent Event(string json)
    {
        var root = JsonDocument.Parse(json).RootElement;

        return new JournalEvent(DateTimeOffset.UtcNow, root.GetProperty("event").GetString()!, root);
    }

    private static (Window Window, PanelView Panel, CommanderGameState State) Open(params string[] events)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var root = TestSurface.MemoryFolder("d47-statistics-page-tests");
        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));

        foreach (var json in events)
        {
            state.Apply(Event(json));
        }

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableCommanderName(() => state.Identity.Name);
        panel.EnableStanding(() => state);
        panel.EnableStatistics(() => state);
        panel.EnableChecklist(checklists);
        panel.EnableSearch();

        var window = new Window
        {
            Content = panel,
            Width = 1280,
            Height = 1050,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();

        panel.Tab = PanelTab.Commander;
        panel.Nav.SelectRoot(PanelTab.Commander, StatisticsPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return (window, panel, state);
    }

    private static StatisticsPage Page(PanelView panel) =>
        panel.GetVisualDescendants().OfType<StatisticsPage>().Single();

    private static TextBox Search(PanelView panel) => panel.FindControl<TextBox>("SearchInput")!;

    private static void Type(PanelView panel, string text)
    {
        Search(panel).Text = text;
        Dispatcher.UIThread.RunJobs();
    }

    private static bool Shows(Control within, string text) =>
        within.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == text);

    [AvaloniaFact]
    public void StatisticsIsTheCommanderTabsSecondRoot()
    {
        var (window, panel, _) = Open(Statistics);

        Assert.Equal(StatisticsPage.RootKey, panel.Nav.Roots(PanelTab.Commander)[1].Key);

        window.Close();
    }

    [AvaloniaFact]
    public void TheSidebarListsEverySectionInTheGamesOrderWithItsCount()
    {
        var (window, panel, _) = Open(Statistics);

        var sidebar = Page(panel).GetVisualDescendants().OfType<Sidebar>().Single();
        var labels = sidebar.Items
            .Select(row => row.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToArray())
            .ToList();

        Assert.Equal(16, labels.Count);
        Assert.Equal(
            Sections.Select(name => CareerStatistics.Label(name).ToUpperInvariant()),
            labels.Select(row => row[0]));
        Assert.Equal("3", labels[1][1]);
        Assert.Equal("MATERIAL TRADER", labels[13][0]);

        window.Close();
    }

    [AvaloniaFact]
    public void ChoosingASectionShowsEveryFigureItHasInThreeColumns()
    {
        var (window, panel, _) = Open(Statistics);
        var page = Page(panel);

        Assert.Equal("Bank_Account", page.Section);

        page.Open("Combat");
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(page, "COMBAT"));
        Assert.True(Shows(page, "BOUNTIES CLAIMED"));
        Assert.True(Shows(page, "1,842"));
        Assert.True(Shows(page, "2,104,880,400 CR"));
        Assert.True(Shows(page, "COMBAT BONDS"));
        Assert.False(Shows(page, "CURRENT WEALTH"));

        var grid = page.GetVisualDescendants().OfType<Grid>()
            .First(g => g.Children.OfType<Border>().Any(tile => Shows(tile, "BOUNTIES CLAIMED")));
        Assert.Equal(3, grid.ColumnDefinitions.Count(column => column.Width.IsStar));

        window.Close();
    }

    [AvaloniaFact]
    public void TheSearchFindsFiguresAcrossSectionsAndMatchesSectionNames()
    {
        var (window, panel, _) = Open(Statistics);
        var page = Page(panel);

        Assert.True(Search(panel).IsVisible);
        Assert.Equal("Find a figure in any section", Search(panel).PlaceholderText);

        Type(panel, "profit");

        Assert.True(Shows(page, "FIGURES MATCHING “PROFIT”"));
        Assert.True(Shows(page, "Bounty Hunting Profit"));
        Assert.True(Shows(page, "Market Profits"));
        Assert.True(Shows(page, "Organic Data Profits"));
        Assert.True(Shows(page, "EXOBIOLOGY"));
        Assert.False(Shows(page, "Bounties Claimed"));

        Type(panel, "crew");

        Assert.True(Shows(page, "Npc Crew Hired"));
        Assert.True(Shows(page, "Multicrew Time Total"));
        Assert.True(Shows(page, "2H 1M"));

        window.Close();
    }

    [AvaloniaFact]
    public void ClickingAResultOpensItsSectionAndClearsTheSearch()
    {
        var (window, panel, _) = Open(Statistics);
        var page = Page(panel);

        Type(panel, "market profits");

        var label = page.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == "Market Profits");
        var centre = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), window)!.Value;
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Trading", page.Section);
        Assert.Equal(string.Empty, Search(panel).Text);
        Assert.True(Shows(page, "MARKETS TRADED WITH"));

        window.Close();
    }

    [AvaloniaFact]
    public void ASearchThatFindsNothingSaysSo()
    {
        var (window, panel, _) = Open(Statistics);

        Type(panel, "thargoid");

        Assert.True(Shows(Page(panel), StatisticsPage.NoMatch));

        window.Close();
    }

    [AvaloniaFact]
    public void ANewStatisticsEventRedrawsThePageOnTheTick()
    {
        var (window, panel, state) = Open(Statistics);
        var page = Page(panel);
        page.Open("Combat");

        state.Apply(Event(Statistics.Replace("1842", "1900", StringComparison.Ordinal)));

        Assert.True(panel.TickCommander());
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shows(page, "1,900"));
        Assert.Equal("Combat", page.Section);
        Assert.False(panel.TickCommander());

        window.Close();
    }

    [AvaloniaFact]
    public void BeforeTheGameWritesThemThePageSaysSo()
    {
        var (window, panel, _) = Open();

        Assert.True(Shows(Page(panel), "The game has not reported your career statistics yet. It writes them when you log in."));
        Assert.False(Search(panel).IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void TheFooterCarriesAPhraseGetCommanderStatisticsAnswers()
    {
        var (window, panel, _) = Open(Statistics);

        Assert.True(Shows(panel, $"Say: “{StatisticsPage.Phrase}”"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheStatisticsScreenIsCaptured()
    {
        var (window, panel, _) = Open(Statistics);
        Page(panel).Open("Combat");
        Dispatcher.UIThread.RunJobs();

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("commander-statistics.png");
        }

        Type(panel, "profit");

        using (var frame = window.CaptureRenderedFrame()!)
        {
            var path = "commander-statistics-search.png";
            frame.SaveCapture(path);
        }

        window.Close();
    }
}
