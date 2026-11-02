using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A search the Commander asked for opens its kind's page on every surface with the answer drawn (#1083).</summary>
public sealed class ASpokenSearchOpensItsPageOnEverySurfaceTests
{
    private const long Ltt7786 = 633608311522;

    private sealed class Galaxy : StubGalaxyService
    {
        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult(
                "LTT 7786",
                1,
                [new SystemSummary
                {
                    Name = "LTT 7786",
                    SystemAddress = Ltt7786,
                    Distance = 0,
                    Government = "Anarchy",
                    Factions = [new FactionPresence("Diamond Frogs", 0.6) { State = "Boom" }],
                }]));

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult("LTT 7786", 0, []));

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult("LTT 7786", 0, []));

    }

    private sealed class Systems : StubStarSystemService
    {
        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(new StarSystemProfile { Name = $"System {systemAddress}", SystemAddress = systemAddress });

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class World : GalaxySearchWorld
    {
        public World()
            : base(new Systems(), lookups: true, suggesting: false)
        {
            Registry = CapabilityRegistry.Build(
                [GalaxyCapability.Create(new Galaxy(), () => "LTT 7786", Settings, searches: Board)]);
        }

        public Task<ToolResult> AskAsync(string tool, params (string Name, string Value)[] values) =>
            AskedByVoice.AskAsync(Registry, Routing, tool, values);
    }

    private static (PanelView Panel, Window Window) Shown(World world, bool headset = false)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableStarSystem(world.Surface);
        world.Routing.RouteNavigation(panel.Nav, action => action());

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    private static List<Control> Rows(Control root) =>
        [.. root.GetVisualDescendants().OfType<Control>().Where(control => control.Classes.Contains(GalaxySearchPage.RowClass))];

    [AvaloniaFact]
    public async Task FindAnarchySystemsInBoomOpensSystemsOnTheWindowAndTheHeadsetWithTheAnswerDrawn()
    {
        var world = new World();
        var (window, windowHost) = Shown(world);
        var (headset, headsetHost) = Shown(world, headset: true);

        await world.AskAsync("search_systems", ("government", "Anarchy"), ("faction_state", "Boom"));

        Assert.All([window, headset], panel =>
        {
            Assert.Equal(PanelTab.Search, panel.Tab);
            Assert.Equal([SystemSearchPage.RootKey], panel.Nav.TrailOf(PanelTab.Search).Select(crumb => crumb.Key));

            var page = panel.GetVisualDescendants().OfType<SystemSearchPage>().Single();
            Assert.Equal(GalaxySearchView.Answer, page.View);
            Assert.Single(Rows(panel));
        });

        windowHost.Close();
        headsetHost.Close();
    }

    [AvaloniaFact]
    public async Task EachKindOpensItsOwnPage()
    {
        var world = new World();
        var (panel, host) = Shown(world);

        await world.AskAsync("search_stations", ("services", "Black Market"));
        Assert.Equal(StationSearchPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));
        Assert.Equal(GalaxySearchView.NoResults, panel.GetVisualDescendants().OfType<StationSearchPage>().Single().View);

        await world.AskAsync("find_body", ("landable", "true"));
        Assert.Equal(BodySearchPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));
        Assert.Equal(GalaxySearchView.NoResults, panel.GetVisualDescendants().OfType<BodySearchPage>().Single().View);

        host.Close();
    }

    [AvaloniaFact]
    public async Task ASurfaceHoldingAModalPageDoesNotMove()
    {
        var world = new World();
        var (held, heldHost) = Shown(world);
        var (free, freeHost) = Shown(world, headset: true);
        held.Nav.Take(new NavCrumb("chooser", "Chooser"));

        await world.AskAsync("search_systems", ("government", "Anarchy"));

        Assert.Equal(PanelTab.Transcript, held.Tab);
        Assert.Equal("chooser", held.Nav.Trail[^1].Key);
        Assert.Equal(SystemSearchPage.RootKey, free.Nav.RootKeyOf(PanelTab.Search));
        Assert.Equal(PanelTab.Search, free.Tab);

        heldHost.Close();
        freeHost.Close();
    }

    [AvaloniaFact]
    public async Task ARefusedFilterMovesNothing()
    {
        var world = new World();
        var (panel, host) = Shown(world);

        var result = await world.AskAsync("search_systems", ("government", "Pirate Kingdom"));

        Assert.True(result.IsError);
        Assert.Null(result.Page);
        Assert.Equal(PanelTab.Transcript, panel.Tab);

        host.Close();
    }
}
