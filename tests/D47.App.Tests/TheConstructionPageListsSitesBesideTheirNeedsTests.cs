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
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Asset Mgmt › Construction: the sites beside the selected one's needs and its last market search (#828).</summary>
public class TheConstructionPageListsSitesBesideTheirNeedsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 18, 0, 0, TimeSpan.Zero);

    private const long Vogel = 3900000001;
    private const long Tamm = 3900000002;
    private const long CarrierId = 3715429376;
    private const string Sign = "BNH-T2F";

    private const string VogelWhere = "Vogel Relay, HIP 47126";

    private static string At(TimeSpan offset) => (Now + offset).ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static (TimeSpan At, string Json) Dock(TimeSpan at, string station, string system, long marketId, string type = "SpaceConstructionDepot") =>
        (at, $$"""{"event":"Docked","StationName":"{{station}}","StationType":"{{type}}","StarSystem":"{{system}}","MarketID":{{marketId}}}""");

    private static (TimeSpan At, string Json) Depot(TimeSpan at, long marketId, double progress, params (string Symbol, string Name, int Required, int Provided)[] rows) =>
        (at, $$"""{"event":"ColonisationConstructionDepot","MarketID":{{marketId}},"ConstructionProgress":{{progress}},"ConstructionComplete":false,"ConstructionFailed":false,"ResourcesRequired":[{{string.Join(",", rows.Select(row => $$"""{"Name":"${{row.Symbol}}_name;","Name_Localised":"{{row.Name}}","RequiredAmount":{{row.Required}},"ProvidedAmount":{{row.Provided}},"Payment":1000}"""))}}]}""");

    private static readonly (TimeSpan At, string Json)[] TwoSites =
    [
        Dock(TimeSpan.FromDays(-20), "Tamm Outpost", "Col 285 Sector KT-Q b5-4", Tamm),
        Depot(TimeSpan.FromDays(-20), Tamm, 0.18, ("steel", "Steel", 9200, 0), ("polymers", "Polymers", 3100, 0)),
        Dock(TimeSpan.FromHours(-2), "Vogel Relay", "HIP 47126", Vogel),
        Depot(
            TimeSpan.FromHours(-2),
            Vogel,
            0.62,
            ("steel", "Steel", 20000, 5180),
            ("aluminium", "Aluminium", 9410, 0),
            ("liquidoxygen", "Liquid oxygen", 1980, 0),
            ("titanium", "Titanium", 1150, 0)),
    ];

    /// <summary>An owned carrier holding 1,980 t of liquid oxygen and 3,900 t of aluminium, an order open on aluminium.</summary>
    private static (TimeSpan At, string Json)[] Carrier(int reportedCargo) =>
    [
        (TimeSpan.FromHours(-1), $$"""{"event":"CarrierBuy","CarrierID":{{CarrierId}},"Callsign":"{{Sign}}","Location":"HIP 47126"}"""),
        Dock(TimeSpan.FromHours(-1) + TimeSpan.FromMinutes(1), Sign, "HIP 47126", CarrierId, "FleetCarrier"),
        (TimeSpan.FromHours(-1) + TimeSpan.FromMinutes(2), """{"event":"CargoTransfer","Transfers":[{"Type":"liquidoxygen","Count":1980,"Direction":"tocarrier"},{"Type":"aluminium","Count":3900,"Direction":"tocarrier"}]}"""),
        (TimeSpan.FromHours(-1) + TimeSpan.FromMinutes(3), $$$"""{"event":"CarrierStats","CarrierID":{{{CarrierId}}},"Callsign":"{{{Sign}}}","Name":"Sacred Fire","SpaceUsage":{"TotalCapacity":25000,"Cargo":{{{reportedCargo}}},"FreeSpace":{{{25000 - reportedCargo}}}}}"""),
        (TimeSpan.FromHours(-1) + TimeSpan.FromMinutes(4), $$"""{"event":"CarrierTradeOrder","CarrierID":{{CarrierId}},"BlackMarket":false,"Commodity":"aluminium","PurchaseOrder":2000,"Price":4100}"""),
    ];

    private static readonly (TimeSpan At, string Json)[] Reconciled = [.. TwoSites, .. Carrier(5880)];

    private static readonly (TimeSpan At, string Json)[] Unreconciled = [.. TwoSites, .. Carrier(9000)];

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

    private static SourcingPosting Posting(string site) =>
        new(
            site,
            new SourcingAnswer(
                new SourcingPlan(
                    [
                        new SourcingStop(
                            new MarketSnapshot { Station = "Bresnik Hub", System = "LHS 2936" },
                            [
                                new SourcingLot("Steel", "steel", 7700, 5120),
                                new SourcingLot("Aluminium", "aluminium", 5510, 3880),
                            ],
                            11.4),
                        new SourcingStop(
                            new MarketSnapshot { Station = "Kanwar Dock", System = "Wolf 397" },
                            [new SourcingLot("Titanium", "titanium", 700, 6210)],
                            18.9),
                    ],
                    ["Liquid oxygen"],
                    new Dictionary<string, int>(StringComparer.Ordinal) { ["Titanium"] = 450 }),
                40,
                0,
                true),
            "HIP 47126",
            Now - TimeSpan.FromMinutes(30));

    private sealed class Searches
    {
        public List<IReadOnlyDictionary<string, string>> Asked { get; } = [];
    }

    private static CapabilityRegistry Registry(Searches searches, SourcingBoard board) => CapabilityRegistry.Build(
    [
        new CapabilityDescriptor
        {
            Id = "colonisation",
            Group = "Knowledge",
            Name = "Colonisation",
            Summary = "Sites.",
            Tools =
            [
                new ToolDefinition
                {
                    Name = ConstructionPage.NeedsTool,
                    Description = "Needs.",
                    Parameters =
                    [
                        new ToolParameter { Name = "site", Type = ToolParameterType.String, Description = "Site." },
                        new ToolParameter { Name = "where_to_buy", Type = ToolParameterType.Boolean, Description = "Buy." },
                    ],
                    Handler = (arguments, _) =>
                    {
                        searches.Asked.Add(new Dictionary<string, string>(arguments.Values, StringComparer.Ordinal));
                        board.Post(Posting(VogelWhere));
                        board.Announce();
                        return Task.FromResult(ToolResult.Ok("2 stops cover it."));
                    },
                },
            ],
        },
    ]);

    private static (Window Window, PanelView Panel) Open(
        CommanderGameState state,
        SourcingBoard? board = null,
        CapabilityRegistry? registry = null,
        bool lookups = true,
        bool onFoot = false)
    {
        var root = TempFolders.Create("d47-construction-page-tests");
        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);
        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);
        var kit = onFoot
            ? new OnFootPlanService(
                new OnFootBuildStore(Path.Combine(root, "on-foot.json"), new MemoryFileSystem(), NullLogger<OnFootBuildStore>.Instance),
                checklists,
                () => state)
            : null;

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableCopy(new NoClipboard());
        panel.EnableLoadout(
            ships,
            checklists,
            () => state,
            kit,
            construction: new ConstructionSurface(() => state, () => Now, registry, board ?? new SourcingBoard(), () => lookups));

        var window = new Window
        {
            Content = panel,
            Width = 1280,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();

        panel.Tab = PanelTab.Assets;
        panel.Nav.SelectRoot(PanelTab.Assets, ConstructionPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private sealed class NoClipboard : D47.Core.Capabilities.Builtin.IClipboard
    {
        public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private static ConstructionPage Page(PanelView panel) => panel.GetVisualDescendants().OfType<ConstructionPage>().Single();

    private static List<Button> Rows(PanelView panel) =>
        [.. Page(panel).GetVisualDescendants().OfType<Button>().Where(button => button.Tag is long && button.Classes.Contains(ListRow.Class))];

    private static IReadOnlyList<string> Text(Visual root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Inlines is { Count: > 0 } inlines
            ? string.Concat(inlines.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text))
            : block.Text ?? string.Empty)];

    private static IEnumerable<Notice> Notices(Visual root) =>
        root.GetVisualDescendants().OfType<Notice>().Where(notice => notice.IsEffectivelyVisible);

    private static bool Shows(Visual root, string text) => Text(root).Contains(text);

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

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void ConstructionLandsBetweenCarrierAndMaterials()
    {
        var (window, panel) = Open(State(Reconciled), onFoot: true);

        Assert.Equal(
            ["Ships", "Stored modules", "Crew", "Suits", "Carrier", "Construction", "Materials"],
            panel.Nav.Roots(PanelTab.Assets).Select(root => root.Word));

        window.Close();
    }

    [AvaloniaFact]
    public void ASurfaceWithoutTheFleetStillGetsConstructionBeforeEngineers()
    {
        var state = State(Reconciled);
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableConstruction(new ConstructionSurface(() => state, () => Now, null, null, () => true));

        Assert.Equal(["Construction"], panel.Nav.Roots(PanelTab.Assets).Select(root => root.Word));
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void OneCurrentAndOneStaleSiteFillOneHeadEachAndTheStaleOneWarns()
    {
        var (window, panel) = Open(State(Reconciled));

        var page = Page(panel);
        Assert.True(Shows(page, "BUILDING · 1"));
        Assert.True(Shows(page, "NOT SEEN SINCE · 1"));
        Assert.Equal([Vogel, Tamm], Rows(panel).Select(row => (long)row.Tag!));
        Assert.Equal(Vogel, page.Selected);
        Assert.Empty(Notices(page));
        Assert.Contains(ListRow.DimClass, Rows(panel)[1].Classes);

        Press(Rows(panel)[1]);

        var notice = Assert.Single(Notices(Page(panel)));
        Assert.Equal(NoticeLevel.Warning, notice.Level);
        Assert.StartsWith("Not seen since ", notice.Label, StringComparison.Ordinal);
        Assert.StartsWith("These figures are 20 days old.", notice.Text, StringComparison.Ordinal);
        Assert.Contains(ListRow.SelectedClass, Rows(panel)[1].Classes);
        Assert.DoesNotContain(ListRow.DimClass, Rows(panel)[1].Classes);

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void ACommodityTheCarrierCoversIsCoveredAndAnUnreconciledCarrierIsUnknown()
    {
        var (window, panel) = Open(State(Reconciled));

        var page = Page(panel);
        Assert.True(Shows(page, ConstructionPage.Covered));
        Assert.False(Shows(page, ConstructionPage.Unknown));
        Assert.True(Shows(page, ConstructionPage.OrderOpen));
        Assert.True(Shows(page, "✓ RECONCILED · " + Now.AddMinutes(-57).ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)));

        window.Close();

        (window, panel) = Open(State(Unreconciled));

        page = Page(panel);
        Assert.Equal(4, Text(page).Count(text => text == ConstructionPage.Unknown));
        Assert.False(Shows(page, ConstructionPage.Covered));
        Assert.Contains(Text(page), text => text.StartsWith("NOT RECONCILED SINCE ", StringComparison.Ordinal));

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void TheLastSearchForThisSiteIsDrawnStopByStop()
    {
        var board = new SourcingBoard();
        board.Post(Posting(VogelWhere));

        var (window, panel) = Open(State(Reconciled), board);

        var page = Page(panel);
        Assert.True(Shows(page, "BRESNIK HUB"));
        Assert.True(Shows(page, "KANWAR DOCK"));
        Assert.True(Shows(page, "Steel"));
        Assert.True(Shows(page, "Aluminium"));
        Assert.True(Shows(page, "Titanium"));
        Assert.True(Shows(page, "39,424,000 cr"));
        Assert.True(Shows(page, "5,120 cr/t"));
        Assert.True(Shows(page, "LIQUID OXYGEN"));
        Assert.True(Shows(page, "TITANIUM · 450 t"));
        Assert.True(Shows(page, "65,149,800 cr"));

        Press(Rows(panel)[1]);

        page = Page(panel);
        Assert.False(Shows(page, "BRESNIK HUB"));
        Assert.Contains(Text(page), text => text.StartsWith("No search for Tamm Outpost yet. The last one, at ", StringComparison.Ordinal)
            && text.EndsWith($", was for {VogelWhere}.", StringComparison.Ordinal));

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void SearchMarketsAsksForTheSelectedSitesShoppingList()
    {
        var board = new SourcingBoard();
        var searches = new Searches();
        var (window, panel) = Open(State(Reconciled), board, Registry(searches, board));

        Assert.False(Shows(Page(panel), "BRESNIK HUB"));

        var search = Page(panel).GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, ConstructionPage.SearchLabel));
        Assert.True(search.IsEnabled);
        Press(search);

        var asked = Assert.Single(searches.Asked);
        Assert.Equal("Vogel Relay", asked["site"]);
        Assert.Equal("true", asked["where_to_buy"]);
        Assert.True(Shows(Page(panel), "BRESNIK HUB"));
        Assert.True(Shows(Page(panel), "2 stops cover it."));

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void WithLookupsOffTheSearchTileIsDisabledAndSaysWhy()
    {
        var board = new SourcingBoard();
        var (window, panel) = Open(State(Reconciled), board, Registry(new Searches(), board), lookups: false);

        var search = Page(panel).GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, ConstructionPage.SearchLabel));
        Assert.False(search.IsEnabled);
        Assert.True(Shows(Page(panel), ConstructionPage.LookupsOff));

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void ANewDepotEventRedrawsThePageOnTheTick()
    {
        var state = State(Reconciled);
        var (window, panel) = Open(state);

        Assert.False(panel.TickConstruction());

        state.Apply(Event(TimeSpan.FromMinutes(-5), $$"""{"event":"Docked","StationName":"Vogel Relay","StationType":"SpaceConstructionDepot","StarSystem":"HIP 47126","MarketID":{{Vogel}}}"""));
        state.Apply(Event(TimeSpan.FromMinutes(-5), Depot(TimeSpan.Zero, Vogel, 0.7, ("steel", "Steel", 20000, 20000)).Json));

        Assert.True(panel.TickConstruction());
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shows(Page(panel), "70%"));

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void WithNoSitesTheEmptyStateDraws()
    {
        using var look = AppLook.Put();
        var (window, panel) = Open(State());

        var page = Page(panel);
        Assert.Equal("No construction sites in your journals", page.Summary);
        Assert.True(Shows(page, "CONSTRUCTION SITES"));
        Assert.True(Shows(page, ConstructionPage.NoSites));
        Assert.Empty(Rows(panel));

        Capture(window, "assets-construction-empty.png");
        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    public void TheConstructionPageIsCaptured(string theme)
    {
        using var look = AppLook.Put(theme);
        var board = new SourcingBoard();
        board.Post(Posting(VogelWhere));

        var (window, panel) = Open(State(Reconciled), board, Registry(new Searches(), board));
        Assert.Equal("1 site building · 1 not seen lately · carrier reconciled", Page(panel).Summary);
        Capture(window, $"assets-construction-{theme}.png");

        window.Close();

        (window, panel) = Open(State(Unreconciled), board, Registry(new Searches(), board));
        Press(Rows(panel)[1]);
        Capture(window, $"assets-construction-stale-{theme}.png");

        window.Close();
    }
}
