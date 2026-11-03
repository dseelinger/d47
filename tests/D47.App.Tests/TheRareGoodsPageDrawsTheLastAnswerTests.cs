using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;
using static D47.App.Tests.ControlReading;

namespace D47.App.Tests;

/// <summary>Search › Rare goods draws every rare in reach, most profit first, with a rare run from each (#2096).</summary>
public sealed class TheRareGoodsPageDrawsTheLastAnswerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly string Pledge = GalaxyFilters.Powers[0];

    private sealed class Index(RareIndex? index) : IRareIndexSource
    {
        public Task<RareIndex?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(index);
    }

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

    private sealed class Clipboard : IClipboard
    {
        public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class World : GalaxySearchWorld
    {
        public World()
            : base(new Systems(), lookups: false)
        {
            var symbols = RareCatalogue.All.Take(3).Select(rare => rare.Symbol).ToArray();
            var rows = symbols.Select((symbol, at) => $"{symbol}\t{20000 + (at * 1000)}\t3\t{10000 + (at * 100)}\t{at * 5}\t2026-11-03T08:00:00Z");
            var index = RareIndex.Parse(RareIndex.Header + "\n" + string.Join("\n", rows) + "\n");

            Registry = CapabilityRegistry.Build(
            [
                GalaxyCapability.Create(
                    null,
                    () => "LTT 7786",
                    Settings,
                    now: () => Now,
                    searches: Board,
                    systems: new Systems(),
                    currentPosition: () => StarPosition.Origin,
                    rareIndex: new Index(index)),
            ]);
        }

        protected override Func<string?>? Pledged => () => Pledge;

        public Task<ToolResult> AskAsync(params (string Name, string Value)[] values) =>
            AskedByVoice.AskAsync(Registry, Routing, "list_rares", values);
    }

    private static (PanelView Panel, Window Window) Shown(World world, bool headset = false, bool openRoot = true) =>
        SearchPanelHosting.Show(world.Surface, openRoot ? RareGoodsPage.RootKey : null, headset, world.Routing, new Clipboard());

    private static RareGoodsPage Page(Control root) => root.GetVisualDescendants().OfType<RareGoodsPage>().Single();

    private static async Task WaitForAsync(GalaxySearchBoard board, GalaxySearchPosting? before)
    {
        for (var waited = 0; waited < 500 && ReferenceEquals(board.Last(GalaxySearchKind.Rares), before); waited++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void SearchOffersRareGoodsAfterSignals()
    {
        var (panel, window) = Shown(new World());

        Assert.Equal(RareGoodsPage.RootKey, panel.Nav.Roots(PanelTab.Search).Select(root => root.Key).Last());

        window.Close();
    }

    [AvaloniaFact]
    public async Task ASpokenListOpensThePageOnEverySurfaceWithTheSameQueryAndEveryRare()
    {
        var world = new World();
        var (open, openHost) = Shown(world);
        var (elsewhere, elsewhereHost) = Shown(world, openRoot: false);
        var (headset, headsetHost) = Shown(world, headset: true, openRoot: false);

        // The open page asked for its own list when it opened; the spoken one replaces it.
        var before = world.Board.Last(GalaxySearchKind.Rares);
        await world.AskAsync(("max_distance", "99999"), ("limit", "2"), ("order_by", "name"), ("order", "asc"));
        await WaitForAsync(world.Board, before);

        Assert.All([open, elsewhere, headset], panel =>
        {
            Assert.Equal(PanelTab.Search, panel.Tab);
            Assert.Equal(RareGoodsPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));

            var page = Page(panel);

            Assert.Equal(GalaxySearchView.Answer, page.View);
            Assert.Equal(99999m, page.MaxDistance);
            Assert.Equal(RareCatalogue.All.Count, page.Rows.Count);
            Assert.Equal(page.Rows.OrderBy(row => row.Rare.Name, StringComparer.OrdinalIgnoreCase).Select(row => row.Rare.Name), page.Rows.Select(row => row.Rare.Name));
        });

        openHost.Close();
        elsewhereHost.Close();
        headsetHost.Close();
    }

    [AvaloniaFact]
    public async Task APageSearchAsksForTheMostProfitFirst()
    {
        var world = new World();
        var (panel, window) = Shown(world);

        await world.AskAsync(("max_distance", "99999"));
        Dispatcher.UIThread.RunJobs();

        var before = world.Board.Last(GalaxySearchKind.Rares);
        Press(Named(panel, RareGoodsPage.RunName));
        await WaitForAsync(world.Board, before);

        var posting = world.Board.Last(GalaxySearchKind.Rares)!;

        Assert.Equal("profit", posting.Arguments["order_by"]);

        var profits = Page(panel).Rows.Select(row => row.Profit).Where(profit => profit is not null).ToArray();

        Assert.NotEmpty(profits);
        Assert.Equal(profits.OrderByDescending(profit => profit), profits);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ARareRunFillsInTheStationSearchAndOpensIt()
    {
        var world = new World();
        var (panel, window) = Shown(world);

        await world.AskAsync(("max_distance", "99999"));
        Dispatcher.UIThread.RunJobs();

        var rare = Page(panel).Rows[0].Rare;
        Press(Page(panel).GetVisualDescendants().OfType<Button>().First(button => button.Name == RareGoodsPage.RareRunName));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(StationSearchPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));

        var arguments = panel.GetVisualDescendants().OfType<StationSearchPage>().Single().FormArguments();

        Assert.Equal(rare.System, arguments["near"]);
        Assert.Equal("200", arguments["min_distance"]);
        Assert.Equal(Pledge, arguments["acquiring_power"]);
        Assert.Equal("Unoccupied", arguments["power_state"]);
        Assert.Equal("Market", arguments["services"]);
        Assert.DoesNotContain("max_distance", arguments.Keys);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ThePageIsCaptured()
    {
        var world = new World();
        var (panel, window) = Shown(world);

        await world.AskAsync(("max_distance", "99999"), ("order_by", "profit"));
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("rare-goods");

        window.Close();
    }
}
