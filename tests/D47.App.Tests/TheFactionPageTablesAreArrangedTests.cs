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
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.App.Tests.UiWait;

namespace D47.App.Tests;

/// <summary>The Faction page's Systems, Conflicts and Assets tables take the COLUMNS control and keep what it stores (#2520).</summary>
[Trait("Category", "Integration")]
public sealed class TheFactionPageTablesAreArrangedTests
{
    private const string Ours = "Kestrel Reach Compact";

    private static readonly DateTimeOffset Fetched = DateTimeOffset.UtcNow.AddMinutes(-4);

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

    private sealed record Table(string Id, int Section, string Head, string[] Defaults, string Remove, string? Add, string Move);

    private static readonly Table[] Tables =
    [
        new(
            FactionPage.TableId,
            1,
            FactionPage.HeadName,
            ["System", "Influence", "Active", "Pending", "Recovering", "Controls", "Updated"],
            "Pending",
            "InConflict",
            "Updated"),
        new(
            FactionPage.ConflictsTableId,
            2,
            FactionPage.ConflictsHeadName,
            ["System", "Conflict", "Opponent", "Days won", "At stake"],
            "Opponent",
            null,
            "At stake"),
        new(
            FactionPage.AssetsTableId,
            3,
            FactionPage.AssetsHeadName,
            ["Station", "System", "Type", "Pad", "Distance"],
            "Type",
            null,
            "Distance"),
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

    private static ViewStateStore Views() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static FactionReport Report()
    {
        var rows = Enumerable.Range(0, 8)
            .Select(i => new FactionSystem
            {
                Name = $"Hollin {i:000}",
                SystemAddress = 1000 + i,
                Influence = 0.7 - i * 0.01,
                ActiveStates = i == 1 ? ["War"] : i % 2 == 0 ? ["Expansion"] : [],
                PendingStates = i == 3 ? ["Election"] : [],
                Controls = i % 3 == 0,
                ReportedAt = Fetched.AddHours(-i),
            })
            .ToList();

        return new FactionReport(Ours, "Confederacy", "Independent", rows, [new FactionStation("Hub", "Coriolis", null, 100, "Hollin 000")])
        {
            FetchedAt = Fetched,
        };
    }

    private static (PanelView Panel, Window Window) Open(ColumnArrangementMemory memory, bool headset, int section)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableColumnArrangements(memory);
        panel.EnableStarSystem(new StarSystemSurface(new Systems(), () => new JournalLocation("Hollin 002", null, false, null))
        {
            Searches = new GalaxySearchSurface(CapabilityRegistry.Build([]), new GalaxySearchBoard(), null, () => string.Empty, () => [])
            {
                Faction = (_, _, _, _) => Task.FromResult(Report()),
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
        page.ShowSection(section);
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
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

    [AvaloniaTheory]
    [MemberData(nameof(EveryTable))]
    public void WithNothingStoredEachTableDrawsItsOwnColumnsAndOffersTheColumnsTile(string id, bool headset)
    {
        var table = Tables.Single(known => known.Id == id);
        var (panel, window) = Open(new ColumnArrangementMemory(Views(), headset), headset, table.Section);

        Assert.Equal(table.Defaults, Keys(panel, id));
        Assert.NotNull(Named<Button>(panel, ColumnArranger.NameOf(id, "Tile")));

        window.Close();
    }

    [AvaloniaTheory]
    [MemberData(nameof(EveryTable))]
    public void AnAddedMovedAndRemovedColumnIsKeptWhenThePageOpensAgain(string id, bool headset)
    {
        var views = Views();
        var table = Tables.Single(known => known.Id == id);
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
        Assert.True(Array.IndexOf(arranged, table.Move) < Array.IndexOf(table.Defaults, table.Move));

        if (table.Add is { } added)
        {
            Assert.Contains(added, arranged);
        }

        firstWindow.Close();

        var (again, againWindow) = Open(new ColumnArrangementMemory(views, headset), headset, table.Section);
        Assert.Equal(arranged, Keys(again, id));

        Press(again, ColumnArranger.NameOf(id, "Tile"));
        Press(again, ColumnArranger.NameOf(id, "Default"));
        Assert.Equal(table.Defaults, Keys(again, id));
        againWindow.Close();
    }

    [AvaloniaFact]
    public void ASystemsInConflictColumnReadsYesOrNo()
    {
        var table = Tables[0];
        var (panel, window) = Open(new ColumnArrangementMemory(Views()), false, table.Section);

        Press(panel, ColumnArranger.NameOf(table.Id, "Tile"));
        Press(panel, ColumnArranger.NameOf(table.Id, "Add"));
        Named<CheckBox>(panel, ColumnArranger.NameOf(table.Id, "Show.InConflict")).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Press(panel, ColumnArranger.NameOf(table.Id, "Done"));

        var texts = Texts(panel);
        Assert.Contains("Yes", texts);
        Assert.Contains("No", texts);

        window.Close();
    }

    [AvaloniaFact]
    public void ASystemsSortFromAHeaderFollowsItsColumnWhenItMoves()
    {
        var table = Tables[0];
        var (panel, window) = Open(new ColumnArrangementMemory(Views()), false, table.Section);

        List<string?> Order() => [.. Texts(panel).Where(text => text is not null && text.StartsWith("Hollin ", StringComparison.Ordinal))];

        var byDefault = Order();
        Press(panel, $"{table.Head}.Active");
        var sorted = Order();
        Assert.NotEqual(byDefault, sorted);
        Assert.Contains("ACTIVE ▲", Texts(panel));

        Press(panel, ColumnArranger.NameOf(table.Id, "Tile"));
        Press(panel, $"{table.Head}.Choose.Active");
        Press(panel, ColumnArranger.NameOf(table.Id, "MoveLeft"));
        Press(panel, ColumnArranger.NameOf(table.Id, "MoveLeft"));
        Press(panel, ColumnArranger.NameOf(table.Id, "Done"));

        Assert.Equal(sorted, Order());
        Assert.Contains("ACTIVE ▲", Texts(panel));
        Assert.Equal("Active", Keys(panel, table.Id)[0]);

        window.Close();
    }

    [AvaloniaFact]
    public void NoConflictsHeaderSorts()
    {
        var table = Tables[1];
        var (panel, window) = Open(new ColumnArrangementMemory(Views()), false, table.Section);

        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), button => button.Name is { } name && name.StartsWith(table.Head, StringComparison.Ordinal));
        Assert.DoesNotContain(Texts(panel), text => text is not null && (text.EndsWith('▲') || text.EndsWith('▼')));

        window.Close();
    }
}
