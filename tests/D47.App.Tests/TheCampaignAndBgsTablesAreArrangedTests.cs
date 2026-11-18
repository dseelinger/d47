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
using D47.Core.Campaigns;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Campaign page's Systems, Fronts and Chronicle tables and the WORK table take the COLUMNS control and keep what it stores (#2521).</summary>
[Trait("Category", "Integration")]
public sealed class TheCampaignAndBgsTablesAreArrangedTests
{
    private const string Frontier = "F2521";
    private const string Us = "Kestrel Reach Compact";
    private const string Rival = "Lave Purple Gang";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-09T18:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset BgsTick = DateTimeOffset.Parse("2026-11-09T14:12:00Z", CultureInfo.InvariantCulture);

    private static readonly string[] SystemNames = ["Gliese 868", "Kanati", "Haoria", "Wolf 1301", "Arque", "LHS 1914"];

    private sealed record Table(string Id, int Section, string Head, string[] Defaults, string[] HeadsetDefaults, string Remove, string? Add, string Move);

    private static readonly Table[] Tables =
    [
        new(
            CampaignPage.TableId,
            CampaignModel.SystemsSection,
            CampaignPage.HeadName,
            ["System", "Distance", "Influence", "Lead", "Your work", "State", "Intent"],
            ["System", "Distance", "Influence", "Lead", "State", "Intent"],
            "State",
            "Updated",
            "Lead"),
        new(
            CampaignPage.FrontsTableId,
            CampaignModel.FrontsSection,
            CampaignPage.FrontHeadName,
            ["Rank", "Kind", "System", "Distance", "Reason", "Data age", "Take"],
            ["Rank", "Kind", "System", "Distance", "Reason", "Data age", "Take"],
            "Reason",
            "Controls",
            "Data age"),
        new(
            CampaignPage.ChronicleTableId,
            CampaignModel.ChronicleSection,
            CampaignPage.ChronicleHeadName,
            ["Ended", "Kind", "System", "Outcome", "Ticks", "Your work"],
            ["Ended", "Kind", "System", "Outcome", "Ticks", "Your work"],
            "Kind",
            null,
            "Ticks"),
        new(
            BgsWorkTable.TableId,
            CampaignModel.WorkSection,
            "BgsWorkHead",
            ["System", "Missions", "Combat", "Trade", "Data", "Influence"],
            ["System", "Missions", "Combat", "Trade", "Data", "Influence"],
            "Trade",
            null,
            "Data"),
    ];

    public static TheoryData<string, bool> EveryTable()
    {
        var data = new TheoryData<string, bool>();

        foreach (var headset in new[] { false, true })
        {
            foreach (var table in Tables)
            {
                data.Add(table.Id, headset);
            }
        }

        return data;
    }

    private sealed class Fixture
    {
        public CampaignStore Store { get; } = new("campaigns.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance);

        public CommanderGameState State { get; }

        public DateTimeOffset? Tick { get; set; } = BgsTick;

        private CampaignFrontsSnapshot Snapshot { get; set; } = null!;

        public Fixture()
        {
            State = JournalScript.Commander(
                Frontier,
                "Kade Orrin",
                """{ "timestamp":"2026-11-09T17:30:00Z", "event":"FSDJump", "StarSystem":"Arque", "SystemAddress":129, "StarPos":[0,0,0], "Factions":[{ "Name":"Arque Purple Cartel", "Influence":0.57 },{ "Name":"Kestrel Reach Compact", "Influence":0.245 }] }""",
                """{ "timestamp":"2026-11-09T17:40:00Z", "event":"FSDJump", "StarSystem":"Haoria", "SystemAddress":130, "StarPos":[0,0,0], "Factions":[{ "Name":"Kestrel Reach Compact", "Influence":0.526, "ActiveStates":[{ "State":"War" }] },{ "Name":"Lave Purple Gang", "Influence":0.312, "ActiveStates":[{ "State":"War" }] }], "Conflicts":[{ "WarType":"war", "Status":"active", "Faction1":{ "Name":"Kestrel Reach Compact", "Stake":"Haoria Hub", "WonDays":2 }, "Faction2":{ "Name":"Lave Purple Gang", "Stake":"", "WonDays":1 } }] }""",
                """{"timestamp":"2026-11-09T16:00:00Z","event":"MissionCompleted","FactionEffects":[{"Faction":"Arque Purple Cartel","Influence":[{"SystemAddress":129,"Trend":"UpGood","Influence":"+"}]}]}""",
                """{"timestamp":"2026-11-09T16:30:00Z","event":"MissionCompleted","FactionEffects":[{"Faction":"Lave Purple Gang","Influence":[{"SystemAddress":130,"Trend":"UpGood","Influence":"+"}]}]}""");

            Store.Choose(Frontier, FrontKind.Conflict, "Haoria", BgsTick.AddDays(-20));
            Store.Resolve(Frontier, FrontKind.Conflict, "Haoria", Entry("Conflict", "Haoria", "Won", BgsTick.AddDays(-19), BgsTick.AddDays(-18)));
            Store.Choose(Frontier, FrontKind.Coup, "Arque", BgsTick.AddDays(-5));
            Store.Resolve(Frontier, FrontKind.Coup, "Arque", Entry("Coup threat", "Arque", "Resolved", BgsTick.AddDays(-4)));

            var report = Report();
            Snapshot = new CampaignFrontsSnapshot(
                Frontier, Us, report, BgsTick.AddMinutes(5), CampaignFronts.Of(report, State.Standings, Store.For(Frontier), Now), BgsTick.AddMinutes(6), null, null);
        }

        public CampaignSources Sources => new(
            () => State,
            Store,
            () => Snapshot,
            () => Us,
            () => Tick,
            CapabilityRegistry.Build([CampaignCapability.Create(Store, () => State)]))
        {
            Now = () => TheCampaignAndBgsTablesAreArrangedTests.Now,
        };
    }

    private static ChronicleEntry Entry(string kind, string system, string outcome, params DateTimeOffset[] ticks) =>
        new(kind, system, Rival, outcome, ticks, [new ChronicleWork(BgsWorkKind.Mission, 7)]);

    private static FactionSystem Place(string name, double influence, bool controls, string[]? active = null, string[]? pending = null, double rival = 0.10, int age = 1, StarPosition? at = null) => new()
    {
        Position = at,
        Name = name,
        Influence = influence,
        Controls = controls,
        ActiveStates = active ?? [],
        PendingStates = pending ?? [],
        Others = [new FactionNeighbour(Rival, rival)],
        ReportedAt = Now.AddDays(-age),
        Population = 1_000_000,
    };

    private static FactionReport Report() => new(Us, "Cooperative", "Independent",
    [
        Place("Gliese 868", 0.728, true, ["Boom"], rival: 0.147, at: new StarPosition(0, 6, 8)),
        Place("Kanati", 0.610, true, rival: 0.225, age: 41, at: new StarPosition(3, 4, 0)),
        Place("Haoria", 0.526, true, ["War", "Outbreak"], rival: 0.312, at: new StarPosition(0, 0, 0)),
        Place("Wolf 1301", 0.440, true, rival: 0.406, at: new StarPosition(-9530.5, -910.28, 19808.13)),
        Place("Arque", 0.245, true, rival: 0.572, at: new StarPosition(1, 2, 2)),
        Place("LHS 1914", 0.041, false, pending: ["Retreat"], rival: 0.351, at: new StarPosition(0, 0, 12)),
    ], []);

    private static ViewStateStore Views() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static (CampaignPage Page, Window Window) Open(ColumnArrangementMemory memory, bool headset, int section, Fixture? fixture = null)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var page = new CampaignPage(
            (fixture ?? new Fixture()).Sources,
            headset: headset,
            copy: headset ? null : _ => Task.FromResult(true),
            columnArrangements: memory);
        var window = new Window
        {
            Content = page,
            Width = headset ? 1024 : 1280,
            Height = 1300,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        page.GetVisualDescendants().OfType<Segment>().First(segment => segment.Name == CampaignPage.SectionsName).PressOption(section);
        Dispatcher.UIThread.RunJobs();

        return (page, window);
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

    private static List<string?> Texts(Visual root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text)];

    private static List<string> SystemOrder(Visual root, string id) =>
    [
        .. root.GetVisualDescendants().OfType<TableGrid>()
            .Where(grid => grid.Layout.Id == id && !grid.IsHeader)
            .Select(grid => grid.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).FirstOrDefault(text => text is not null && SystemNames.Contains(text)))
            .OfType<string>(),
    ];

    [AvaloniaTheory]
    [MemberData(nameof(EveryTable))]
    public void WithNothingStoredEachTableDrawsItsOwnColumnsAndOffersTheColumnsTile(string id, bool headset)
    {
        var table = Tables.Single(known => known.Id == id);
        var (page, window) = Open(new ColumnArrangementMemory(Views(), headset), headset, table.Section);

        Assert.Equal(headset ? table.HeadsetDefaults : table.Defaults, Keys(page, id));
        Assert.NotNull(Named<Button>(page, ColumnArranger.NameOf(id, "Tile")));

        window.Close();
    }

    [AvaloniaTheory]
    [MemberData(nameof(EveryTable))]
    public void AnAddedMovedAndRemovedColumnIsKeptWhenThePageOpensAgain(string id, bool headset)
    {
        var views = Views();
        var table = Tables.Single(known => known.Id == id);
        var defaults = headset ? table.HeadsetDefaults : table.Defaults;
        var (first, firstWindow) = Open(new ColumnArrangementMemory(views, headset), headset, table.Section);

        Press(first, ColumnArranger.NameOf(id, "Tile"));
        Press(first, $"{table.Head}.Choose.{table.Remove}");
        Press(first, ColumnArranger.NameOf(id, "Remove"));
        Press(first, $"{table.Head}.Choose.{table.Move}");
        Press(first, ColumnArranger.NameOf(id, "MoveLeft"));

        if (table.Add is { } add)
        {
            Press(first, ColumnArranger.NameOf(id, "Add"));
            Named<CheckBox>(first, ColumnArranger.NameOf(id, $"Show.{add}")).IsChecked = true;
            Dispatcher.UIThread.RunJobs();
        }

        Press(first, ColumnArranger.NameOf(id, "Done"));

        var arranged = Keys(first, id);
        Assert.DoesNotContain(table.Remove, arranged);
        Assert.True(Array.IndexOf(arranged, table.Move) < Array.IndexOf(defaults, table.Move));

        if (table.Add is { } added)
        {
            Assert.Contains(added, arranged);
        }

        firstWindow.Close();

        var (again, againWindow) = Open(new ColumnArrangementMemory(views, headset), headset, table.Section);
        Assert.Equal(arranged, Keys(again, id));

        Press(again, ColumnArranger.NameOf(id, "Tile"));
        Press(again, ColumnArranger.NameOf(id, "Default"));
        Assert.Equal(defaults, Keys(again, id));
        againWindow.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnOpenColumnsBarDoesNotRedrawTheTableEndlessly(bool headset)
    {
        foreach (var table in Tables)
        {
            var (page, window) = Open(new ColumnArrangementMemory(Views(), headset), headset, table.Section);

            Press(page, ColumnArranger.NameOf(table.Id, "Tile"));
            var tile = Named<Button>(page, ColumnArranger.NameOf(table.Id, "Tile"));

            for (var i = 0; i < 20; i++)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }

            Assert.Same(tile, Named<Button>(page, ColumnArranger.NameOf(table.Id, "Tile")));
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TheHeadsetOffersYourWorkOnSystemsAndDoesNotShowItUntilAdded()
    {
        var table = Tables[0];
        var (page, window) = Open(new ColumnArrangementMemory(Views(), true), true, table.Section);

        Assert.DoesNotContain("Your work", Keys(page, table.Id));
        Assert.Contains(Texts(page), text => text?.StartsWith("Under each system: your work", StringComparison.Ordinal) == true);

        Press(page, ColumnArranger.NameOf(table.Id, "Tile"));
        Press(page, $"{table.Head}.Choose.Intent");
        Press(page, ColumnArranger.NameOf(table.Id, "Remove"));
        Press(page, ColumnArranger.NameOf(table.Id, "Add"));
        Named<CheckBox>(page, ColumnArranger.NameOf(table.Id, "Show.Your work")).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Press(page, ColumnArranger.NameOf(table.Id, "Done"));

        Assert.Equal("Your work", Keys(page, table.Id)[^1]);
        Assert.DoesNotContain(Texts(page), text => text?.StartsWith("Under each system: your work", StringComparison.Ordinal) == true);

        window.Close();
    }

    [AvaloniaFact]
    public void AFrontsAddedColumnsReadYesOrNoAndTheFirstThreeItems()
    {
        var table = Tables[1];
        var (page, window) = Open(new ColumnArrangementMemory(Views()), false, table.Section);

        Press(page, ColumnArranger.NameOf(table.Id, "Tile"));
        Press(page, ColumnArranger.NameOf(table.Id, "Add"));

        foreach (var key in new[] { "Controls", "Opponent", "Intent", "Lead", "States", "At stake" })
        {
            Named<CheckBox>(page, ColumnArranger.NameOf(table.Id, $"Show.{key}")).IsChecked = true;
        }

        Dispatcher.UIThread.RunJobs();
        Press(page, ColumnArranger.NameOf(table.Id, "Done"));

        var texts = Texts(page);
        Assert.Contains("Yes", texts);
        Assert.Contains("No", texts);
        Assert.Contains(Rival, texts);
        Assert.Contains("Take", Keys(page, table.Id)[^1..]);

        Assert.DoesNotContain(
            page.GetVisualDescendants().OfType<CheckBox>(),
            box => box.Name is { } name && name.StartsWith(ColumnArranger.NameOf(table.Id, "Show.Margin"), StringComparison.Ordinal));

        window.Close();
    }

    [AvaloniaFact]
    public void ASystemsSortFromAHeaderFollowsItsColumnAndTheHubSentenceNamesIt()
    {
        var table = Tables[0];
        var (page, window) = Open(new ColumnArrangementMemory(Views()), false, table.Section);

        var byDefault = SystemOrder(page, table.Id);
        Press(page, $"{table.Head}.Lead");
        var sorted = SystemOrder(page, table.Id);
        Assert.NotEqual(byDefault, sorted);
        Assert.Contains("LEAD ▼", Texts(page));
        Assert.Contains(Texts(page), text => text?.Contains("by Lead ▼", StringComparison.Ordinal) == true);

        Press(page, ColumnArranger.NameOf(table.Id, "Tile"));
        Press(page, $"{table.Head}.Choose.Lead");
        Press(page, ColumnArranger.NameOf(table.Id, "MoveLeft"));
        Press(page, ColumnArranger.NameOf(table.Id, "MoveLeft"));
        Press(page, ColumnArranger.NameOf(table.Id, "Done"));

        Assert.Equal(sorted, SystemOrder(page, table.Id));
        Assert.Contains("LEAD ▼", Texts(page));
        Assert.Contains(Texts(page), text => text?.Contains("by Lead ▼", StringComparison.Ordinal) == true);
        Assert.Equal("Lead", Keys(page, table.Id)[1]);

        window.Close();
    }

    [AvaloniaFact]
    public void WorkLeavesInfluenceOutWithoutATickAndKeepsItsStoredPlace()
    {
        var table = Tables[3];
        var views = Views();
        var memory = new ColumnArrangementMemory(views);
        memory.Remember(table.Id, ["Influence", "System", "Missions", "Combat", "Trade", "Data"]);

        var (without, withoutWindow) = Open(memory, false, table.Section, new Fixture { Tick = null });
        Assert.Equal(["System", "Missions", "Combat", "Trade", "Data"], Keys(without, table.Id));
        withoutWindow.Close();

        var (with, withWindow) = Open(new ColumnArrangementMemory(views), false, table.Section);
        Assert.Equal(["Influence", "System", "Missions", "Combat", "Trade", "Data"], Keys(with, table.Id));
        withWindow.Close();
    }
}
