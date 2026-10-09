using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Missions: the live board, in the order and bands the spoken board uses.</summary>
public class TheMissionsPaneRanksAsTheBoardSpeaksTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 18, 0, 0, TimeSpan.Zero);

    private const long Wing = 101;
    private const long Courier = 102;
    private const long Source = 103;
    private const long Massacre = 104;
    private const long Tourists = 105;
    private const long Salvage = 106;

    private static string At(TimeSpan offset) => (Now + offset).ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static readonly (TimeSpan At, string Json)[] Board =
    [
        (TimeSpan.FromHours(-7), $$"""{"event":"Missions","Active":[{"MissionID":{{Salvage}},"Name":"Mission_Salvage_Planet","PassengerMission":false,"Expires":{{(long)(TimeSpan.FromHours(7) + new TimeSpan(4, 9, 0, 0)).TotalSeconds}}}],"Failed":[],"Complete":[]}"""),
        (TimeSpan.FromHours(-6) + TimeSpan.FromMinutes(6), $$"""{"event":"MissionAccepted","MissionID":{{Wing}},"Name":"Mission_DeliveryWing","LocalisedName":"Wing delivery: Bertrandite","Faction":"Diaguandri Interstellar","DestinationSystem":"Kremainn","DestinationStation":"Mackenzie Relay","Commodity":"$Bertrandite_Name;","Commodity_Localised":"Bertrandite","Count":120,"Reward":4812000,"Expiry":"{{At(TimeSpan.FromMinutes(38))}}"}"""),
        (TimeSpan.FromHours(-5), $$"""{"event":"MissionAccepted","MissionID":{{Courier}},"Name":"Mission_Courier","LocalisedName":"Courier: data to Ray Gateway","Faction":"Diaguandri Interstellar","DestinationSystem":"Diaguandri","DestinationStation":"Ray Gateway","Reward":412500,"Expiry":"{{At(new TimeSpan(3, 12, 0))}}"}"""),
        (TimeSpan.FromHours(-5), $$"""{"event":"MissionAccepted","MissionID":{{Source}},"Name":"Mission_Collect","LocalisedName":"Source 18 Progenitor Cells","Faction":"Diaguandri Interstellar","DestinationSystem":"Diaguandri","DestinationStation":"Ray Gateway","Commodity":"$ProgenitorCells_Name;","Commodity_Localised":"Progenitor Cells","Count":18,"Reward":1986000,"Expiry":"{{At(new TimeSpan(1, 4, 0, 0))}}"}"""),
        (TimeSpan.FromHours(-4), $$"""{"event":"MissionAccepted","MissionID":{{Massacre}},"Name":"Mission_Massacre","LocalisedName":"Massacre: Kremainn Crimson Gang","Faction":"Diaguandri Interstellar","TargetFaction":"Kremainn Crimson Gang","DestinationSystem":"Kremainn","DestinationStation":"Lee Hub","Reward":9240000,"Expiry":"{{At(new TimeSpan(2, 11, 0, 0))}}"}"""),
        (TimeSpan.FromHours(-3), $$"""{"event":"MissionAccepted","MissionID":{{Tourists}},"Name":"Mission_Sightseeing","LocalisedName":"Transport 4 tourists to Sothis","Faction":"Diaguandri Interstellar","DestinationSystem":"Sothis","DestinationStation":"Newholm Station","PassengerCount":4,"Reward":3100000,"Expiry":"{{At(new TimeSpan(5, 2, 0, 0))}}"}"""),
        (TimeSpan.FromHours(-2), $$"""{"event":"MissionRedirected","MissionID":{{Massacre}},"NewDestinationSystem":"Kremainn","NewDestinationStation":"Hahn Gateway"}"""),
        (TimeSpan.FromHours(-1), $$"""{"event":"CargoDepot","MissionID":{{Wing}},"UpdateType":"Deliver","CargoType":"Bertrandite","ItemsCollected":120,"ItemsDelivered":72,"TotalItemsToDeliver":120}"""),
        (TimeSpan.FromMinutes(-10), """{"event":"Location","StarSystem":"Diaguandri","Docked":true,"StationName":"Ray Gateway"}"""),
    ];

    private sealed class Plots
    {
        public List<string> Systems { get; } = [];
    }

    private static JournalEvent Event(TimeSpan at, string json)
    {
        var root = JsonDocument.Parse(json).RootElement;

        return new JournalEvent(Now + at, root.GetProperty("event").GetString()!, root);
    }

    private static CommanderGameState State(params (TimeSpan At, string Json)[] events)
    {
        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));

        foreach (var (at, json) in events)
        {
            state.Apply(Event(at, json));
        }

        return state;
    }

    private static CapabilityRegistry Registry(Plots plots) => CapabilityRegistry.Build(
    [
        new CapabilityDescriptor
        {
            Id = "navigation",
            Group = "Ship",
            Name = "Navigation",
            Summary = "Plots.",
            Tools =
            [
                new ToolDefinition
                {
                    Name = MissionsPage.PlotTool,
                    Description = "Plot a course.",
                    Protected = true,
                    Parameters =
                    [
                        new ToolParameter { Name = "system", Type = ToolParameterType.String, Description = "System.", Required = true },
                    ],
                    Handler = (arguments, _) =>
                    {
                        plots.Systems.Add(arguments.TryGetString("system", out var system) ? system : string.Empty);
                        return Task.FromResult(ToolResult.Ok("Plotted."));
                    },
                },
            ],
        },
    ]);

    private static (Window Window, PanelView Panel) Open(
        CommanderGameState state,
        CapabilityRegistry? registry = null,
        bool headset = false,
        string theme = ThemeCatalog.Elite,
        double height = 900)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(theme);

        var root = TempFolders.Create("d47-missions-page-tests");
        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableCommanderName(() => state.Identity.Name);
        panel.EnableChecklist(checklists);
        panel.EnableMissions(() => state, () => Now, registry, _ => Task.FromResult(true));
        panel.EnableStanding(() => state);
        panel.EnableStatistics(() => state);
        panel.EnableSession(() => state);

        var window = new Window
        {
            Content = panel,
            Width = headset ? 1024 : 1280,
            Height = headset ? 640 : height,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();

        panel.Tab = PanelTab.Commander;
        panel.Nav.SelectRoot(PanelTab.Commander, MissionsPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static MissionsPage Page(PanelView panel) => panel.GetVisualDescendants().OfType<MissionsPage>().Single();

    private static List<Button> Rows(PanelView panel) =>
        [.. Page(panel).GetVisualDescendants().OfType<Button>().Where(button => button.Tag is long)];

    private static bool Shows(Visual root, string text) =>
        root.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == text);

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Capture(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame()!;
        var path = name;
        frame.SaveCapture(path);
    }

    [AvaloniaFact]
    public void MissionsIsTheCommanderTabsSecondRoot()
    {
        var (window, panel) = Open(State(Board));

        string[] keys = [.. panel.Nav.Roots(PanelTab.Commander).Select(root => root.Key)];
        Assert.Equal(["checklist", MissionsPage.RootKey, StandingPage.RootKey, StatisticsPage.RootKey, SessionPage.RootKey], keys);
        Assert.Equal("Missions", panel.Nav.Roots(PanelTab.Commander)[1].Word);

        window.Close();
    }

    [AvaloniaFact]
    public void TheRowsTopToBottomAreTheSpokenBoardsRanking()
    {
        var state = State(Board);
        var (window, panel) = Open(state);

        var ranked = state.Missions.Ranked(Now, state.Location);

        Assert.Equal(
            [MissionBand.ExpiringSoon, MissionBand.HandInHere, MissionBand.Rest],
            ranked.Select(mission => MissionBoard.BandOf(mission, Now, state.Location)).Distinct());
        Assert.Equal(ranked.Select(mission => mission.Id), Rows(panel).Select(row => (long)row.Tag!));

        var page = Page(panel);
        Assert.True(Shows(page, "EXPIRING WITHIN THE HOUR"));
        Assert.True(Shows(page, "HAND IN HERE"));
        Assert.True(Shows(page, "EVERYTHING ELSE"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheSummaryCountsWhatHandsInHereAndTheRewardsAtLeast()
    {
        var (window, panel) = Open(State(Board));

        Assert.Equal("6 missions. 2 hand in here. At least 19,550,500 credits in rewards.", Page(panel).Summary);

        window.Close();
    }

    [AvaloniaFact]
    public void AMissionWithNoCargoReportDrawsNoDeliverySection()
    {
        var (window, panel) = Open(State(Board));

        Assert.True(Shows(Page(panel), "DELIVERY"));

        Press(Rows(panel).Single(row => (long)row.Tag! == Courier));

        Assert.False(Shows(Page(panel), "DELIVERY"));
        Assert.True(Shows(Page(panel), "HAND IN HERE"));

        window.Close();
    }

    [AvaloniaFact]
    public void PlotToHandInPlotsTheHandInSystemAsTheCommander()
    {
        var plots = new Plots();
        var registry = Registry(plots);
        var (window, panel) = Open(State(Board), registry);

        var plot = Page(panel).GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Plot to hand-in"));
        Press(plot);

        Assert.Equal(["Kremainn"], plots.Systems);

        // The tool is protected, so the page reaching it at all means it called as the Commander.
        var refused = registry.InvokeAsync(
            MissionsPage.PlotTool,
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = "Sol" }),
            TestContext.Current.CancellationToken,
            ToolCaller.Model).GetAwaiter().GetResult();
        Assert.True(refused.IsError);
        Assert.Equal(["Kremainn"], plots.Systems);

        window.Close();
    }

    [AvaloniaFact]
    public void AHandInHereMissionHasNoPlotTile()
    {
        var (window, panel) = Open(State(Board), Registry(new Plots()));

        Press(Rows(panel).Single(row => (long)row.Tag! == Source));

        Assert.DoesNotContain(
            Page(panel).GetVisualDescendants().OfType<Button>(),
            button => Equals(button.Content, "Plot to hand-in"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheSelectionFollowsTheMissionAndFallsBackToTheFirstRow()
    {
        var state = State(Board);
        var (window, panel) = Open(state);

        Press(Rows(panel).Single(row => (long)row.Tag! == Tourists));
        Assert.Contains(ListRow.SelectedClass, Rows(panel).Single(row => (long)row.Tag! == Tourists).Classes);

        state.Apply(Event(TimeSpan.FromMinutes(1), $$"""{"event":"MissionCompleted","MissionID":{{Tourists}}}"""));
        Assert.True(panel.TickCommander());
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(Rows(panel), row => (long)row.Tag! == Tourists);
        Assert.Contains(ListRow.SelectedClass, Rows(panel)[0].Classes);

        window.Close();
    }

    [AvaloniaFact]
    public void SearchKeepsOnlyMatchingRowsAndDropsEmptyGroups()
    {
        var (window, panel) = Open(State(Board));

        Page(panel).Filter("sothis");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([Tourists], Rows(panel).Select(row => (long)row.Tag!));
        Assert.False(Shows(Page(panel), "EXPIRING WITHIN THE HOUR"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheCountdownRedrawsWhenTheMinuteTurns()
    {
        var now = Now;
        var state = State(Board);
        var page = new MissionsPage(() => state, () => now);

        Assert.False(page.Tick());

        now += TimeSpan.FromSeconds(30);
        Assert.False(page.Tick());

        now += TimeSpan.FromSeconds(30);
        Assert.True(page.Tick());
    }

    [AvaloniaFact]
    public void ANoDetailMissionShowsOnlyTimeLeftAndTheExplanation()
    {
        var (window, panel) = Open(State(Board));

        Press(Rows(panel).Single(row => (long)row.Tag! == Salvage));

        var page = Page(panel);
        Assert.True(Shows(page, MissionsPage.NoDetail));
        Assert.True(Shows(page, "TIME LEFT"));
        Assert.False(Shows(page, "REWARD"));
        Assert.False(Shows(page, "GIVEN BY"));

        window.Close();
    }

    [AvaloniaFact]
    public void WithNoMissionsThePaneSaysSo()
    {
        using var look = AppLook.Put();
        var (window, panel) = Open(State(), height: 560);

        var page = Page(panel);
        Assert.Equal("No missions accepted.", page.Summary);
        Assert.True(Shows(page, MissionsPage.Nothing));
        Assert.True(Shows(page, MissionsPage.NothingHint));
        Assert.True(Shows(page, $"Say: “{MissionsPage.Phrase}”"));
        Assert.Empty(Rows(panel));

        Capture(window, "commander-missions-empty.png");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    public void TheMissionsPaneIsCaptured(string theme)
    {
        using var look = AppLook.Put(theme);
        var (window, panel) = Open(State(Board), Registry(new Plots()), theme: theme);
        Capture(window, $"commander-missions-{theme}.png");

        Press(Rows(panel).Single(row => (long)row.Tag! == Massacre));
        Capture(window, $"commander-missions-redirected-{theme}.png");

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetMissionsPaneIsCaptured()
    {
        using var look = AppLook.Put();
        var (window, panel) = Open(State(Board), Registry(new Plots()), headset: true);

        Assert.Equal(2, Page(panel).GetVisualDescendants().OfType<ScrollViewer>().Count());
        Capture(window, "commander-missions-headset.png");

        window.Close();
    }
}
