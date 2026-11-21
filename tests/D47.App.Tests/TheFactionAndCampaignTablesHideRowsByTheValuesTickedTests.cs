using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Campaigns;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.App.Tests.UiWait;

namespace D47.App.Tests;

/// <summary>The faction and campaign tables list the values of their columns in the FILTER bar, and unticking one hides its rows (#2635).</summary>
[Trait("Category", "Integration")]
public sealed class TheFactionAndCampaignTablesHideRowsByTheValuesTickedTests
{
    private const string Ours = "Kestrel Reach Compact";
    private const string Frontier = "F2635";
    private const string Rival = "Lave Purple Gang";

    private static readonly DateTimeOffset Fetched = DateTimeOffset.UtcNow.AddMinutes(-4);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-09T18:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset BgsTick = DateTimeOffset.Parse("2026-11-09T14:12:00Z", CultureInfo.InvariantCulture);

    private sealed class Systems : StubStarSystemService
    {
        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(null);

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private static FactionReport Report(int systems)
    {
        var rows = Enumerable.Range(0, systems)
            .Select(i => new FactionSystem
            {
                Name = $"Hollin {i:000}",
                SystemAddress = 1000 + i,
                Influence = 0.9 - i * 0.001,
                ActiveStates = i % 2 == 0 ? ["Expansion"] : [],
                ReportedAt = Fetched.AddHours(-i),
            })
            .ToList();

        return new FactionReport(Ours, "Confederacy", "Independent", rows, [])
        {
            FetchedAt = Fetched,
        };
    }

    private static (PanelView Panel, Window Window) OpenFaction(bool headset, int systems)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableStarSystem(new StarSystemSurface(new Systems(), () => new JournalLocation("Hollin 002", null, false, null))
        {
            Searches = new GalaxySearchSurface(CapabilityRegistry.Build([]), new GalaxySearchBoard(), null, () => string.Empty, () => [])
            {
                Faction = (_, _, _, _) => Task.FromResult(Report(systems)),
                SquadronFaction = () => Ours,
            },
        });

        var window = headset
            ? new Window { Content = panel, Width = 1024, Height = 640 }
            : new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(FactionPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        var page = panel.GetVisualDescendants().OfType<FactionPage>().Single();
        Until(() => page.View == FactionView.Answered);
        page.ShowSection(1);
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    private sealed class Fixture
    {
        public CampaignStore Store { get; } = new("campaigns.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance);

        public CommanderGameState State { get; }

        private CampaignFrontsSnapshot Snapshot { get; }

        public Fixture()
        {
            State = JournalScript.Commander(
                Frontier,
                "Kade Orrin",
                """{ "timestamp":"2026-11-09T17:30:00Z", "event":"FSDJump", "StarSystem":"Arque", "SystemAddress":129, "StarPos":[0,0,0], "Factions":[{ "Name":"Arque Purple Cartel", "Influence":0.57 },{ "Name":"Kestrel Reach Compact", "Influence":0.245 }] }""");

            var report = new FactionReport(Ours, "Cooperative", "Independent",
            [
                Place("Gliese 868", 0.728, true, ["Boom"], rival: 0.147),
                Place("Haoria", 0.526, true, ["War", "Outbreak"], rival: 0.312),
                Place("Arque", 0.245, true, rival: 0.572),
                Place("LHS 1914", 0.041, false, pending: ["Retreat"], rival: 0.351),
            ], []);
            Snapshot = new CampaignFrontsSnapshot(
                Frontier, Ours, report, BgsTick.AddMinutes(5), CampaignFronts.Of(report, State.Standings, Store.For(Frontier), Now), BgsTick.AddMinutes(6), null, null);
        }

        public CampaignSources Sources => new(
            () => State,
            Store,
            () => Snapshot,
            () => Ours,
            () => BgsTick,
            CapabilityRegistry.Build([CampaignCapability.Create(Store, () => State)]))
        {
            Now = () => TheFactionAndCampaignTablesHideRowsByTheValuesTickedTests.Now,
        };
    }

    private static FactionSystem Place(string name, double influence, bool controls, string[]? active = null, string[]? pending = null, double rival = 0.10) => new()
    {
        Name = name,
        Influence = influence,
        Controls = controls,
        ActiveStates = active ?? [],
        PendingStates = pending ?? [],
        Others = [new FactionNeighbour(Rival, rival)],
        ReportedAt = Now.AddDays(-1),
        Population = 1_000_000,
    };

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

    private static bool Shows(Visual root, string text) =>
        root.GetVisualDescendants().OfType<TextBlock>().Any(block => block.IsEffectivelyVisible && string.Equals(block.Text, text, StringComparison.Ordinal));

    private static int RowsOf(Visual root, string id) =>
        root.GetVisualDescendants().OfType<TableGrid>().Count(grid => grid.Layout.Id == id && !grid.IsHeader);

    private static void Untick(Visual root, string table, string key, string value)
    {
        Named<CheckBox>(root, ColumnFilter.ValueName(table, key, 0, value)).IsChecked = false;
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void AFactionSystemsActiveStateIsListedAndUntickingItHidesTheSystemsWithOnlyThatState()
    {
        var (panel, window) = OpenFaction(false, 8);
        var table = FactionPage.TableId;

        Assert.True(Shows(panel, "Hollin 000"));
        Assert.True(Shows(panel, "Hollin 001"));

        Press(panel, ColumnFilter.NameOf(table, "Tile"));
        Press(panel, FactionPage.HeadName + ".Filter.Active");
        Untick(panel, table, "Active", "Expansion");

        Assert.False(Shows(panel, "Hollin 000"));
        Assert.True(Shows(panel, "Hollin 001"));
        Assert.Equal(4, RowsOf(panel, table));
        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetPagerCountsTheRowsShownAndReturnsToTheFirstPageWhenTheFilterChanges()
    {
        var (panel, window) = OpenFaction(true, 60);
        var table = FactionPage.TableId;

        Assert.True(Shows(panel, "1–25 of 60"));

        Press(panel, FactionPage.NextName);
        Assert.True(Shows(panel, "26–50 of 60"));

        Press(panel, ColumnFilter.NameOf(table, "Tile"));
        Press(panel, FactionPage.HeadName + ".Filter.Active");
        Untick(panel, table, "Active", "Expansion");
        Press(panel, ColumnFilter.NameOf(table, "Tile"));

        Assert.True(Shows(panel, "1–25 of 30"));
        window.Close();
    }

    [AvaloniaFact]
    public void ACampaignFrontsKindIsListedAndUntickingItHidesTheFrontsOfThatKind()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var page = new CampaignPage(new Fixture().Sources, headset: false, copy: _ => Task.FromResult(true));
        var window = new Window
        {
            Content = page,
            Width = 1280,
            Height = 1300,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        page.GetVisualDescendants().OfType<Segment>().First(segment => segment.Name == CampaignPage.SectionsName).PressOption(CampaignModel.FrontsSection);
        Dispatcher.UIThread.RunJobs();

        var table = CampaignPage.FrontsTableId;
        var before = RowsOf(page, table);
        Assert.True(before > 1);

        Press(page, ColumnFilter.NameOf(table, "Tile"));
        Press(page, CampaignPage.FrontHeadName + ".Filter.Kind");

        var kind = page.GetVisualDescendants().OfType<CheckBox>()
            .First(box => box.Name?.StartsWith(ColumnFilter.NameOf(table, "Value.Kind.0."), StringComparison.Ordinal) == true);
        kind.IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.InRange(RowsOf(page, table), 0, before - 1);
        window.Close();
    }
}
