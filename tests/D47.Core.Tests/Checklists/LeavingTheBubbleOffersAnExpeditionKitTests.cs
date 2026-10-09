using D47.Core.Storage;
using System.Text.Json;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

[Trait("Category", "Integration")]
public class LeavingTheBubbleOffersAnExpeditionKitTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private const long CarrierId = 3700000000L;

    private const string Scoop = """{"Slot":"Slot01_Size6","Item":"int_fuelscoop_size6_class5","On":true,"Priority":0}""";
    private const string Bay = """{"Slot":"Slot02_Size4","Item":"int_buggybay_size4_class2","On":true,"Priority":0}""";
    private const string Scanner = """{"Slot":"Slot03_Size1","Item":"int_detailedsurfacescanner_tiny","On":true,"Priority":0}""";
    private const string Laser = """{"Slot":"HugeHardpoint1","Item":"hpt_miningtoolv2_fixed_huge","On":true,"Priority":0}""";
    private const string Collector = """{"Slot":"Slot04_Size3","Item":"int_dronecontrol_collection_size3_class3","On":true,"Priority":0}""";
    private const string Refinery = """{"Slot":"Slot05_Size3","Item":"int_refinery_size3_class2","On":true,"Priority":0}""";

    private sealed class Routes(CarrierRoute route) : IRouteService
    {
        public Task<PlottedRoute?> PlotAsync(RouteQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<PlottedRoute?>(null);

        public Task<RichesRoute?> PlotRichesAsync(RichesQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<RichesRoute?>(null);

        public Task<ExobiologyRoute?> PlotExobiologyAsync(ExobiologyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<ExobiologyRoute?>(null);

        public Task<CarrierRoute?> PlotCarrierAsync(CarrierRouteQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CarrierRoute?>(route);
    }

    /// <summary>The recorded Sol, Colonia, Sol plot, with spansh's x, y, z on every jump.</summary>
    private static CarrierRoute SolColoniaSol()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "spansh-fleetcarrier-route-sol-colonia-sol.json");

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        return new CarrierRoute([.. document.RootElement.GetProperty("result").GetProperty("jumps").EnumerateArray()
            .Select(jump => new CarrierWaypoint(
                jump.GetProperty("name").GetString()!,
                jump.GetProperty("distance").GetDouble(),
                jump.GetProperty("distance_to_destination").GetDouble(),
                jump.GetProperty("fuel_in_tank").GetInt32(),
                jump.GetProperty("fuel_used").GetInt32(),
                jump.GetProperty("must_restock").GetInt32() != 0,
                jump.GetProperty("restock_amount").GetInt32(),
                jump.GetProperty("tritium_in_market").GetInt32(),
                jump.GetProperty("has_icy_ring").GetBoolean(),
                jump.GetProperty("is_system_pristine").GetBoolean(),
                jump.GetProperty("is_desired_destination").GetInt32() != 0)
            {
                Position = new StarPosition(
                    jump.GetProperty("x").GetDouble(),
                    jump.GetProperty("y").GetDouble(),
                    jump.GetProperty("z").GetDouble()),
            })]);
    }

    /// <summary>Sol to a system 300 ly out, which never leaves the bubble.</summary>
    private static CarrierRoute InsideTheBubble() => new(
    [
        new CarrierWaypoint("Sol", 0, 300, 1_000, 0, false, 0, 0, false, false, true) { Position = StarPosition.Origin },
        new CarrierWaypoint("Nearby", 300, 0, 980, 20, false, 0, 0, false, false, true) { Position = new StarPosition(180, 0, 240) },
    ]);

    private static string Loadout(int id, string ship, string name, params string[] modules) =>
        $$"""{"timestamp":"2026-09-05T10:0{{id}}:00Z","event":"Loadout","Ship":"{{ship}}","ShipID":{{id}},"ShipName":"{{name}}","CargoCapacity":100,"Modules":[{{string.Join(",", modules)}}]}""";

    /// <summary>A snapshot taken at a station, with each listed ship stored at the carrier's market.</summary>
    private static string StoredAtTheCarrier(params (int Id, string Type, string Name)[] ships) =>
        $$"""
        {"timestamp":"2026-09-05T11:00:00Z","event":"StoredShips","StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra","MarketID":128666762,
         "ShipsHere":[],
         "ShipsRemote":[{{string.Join(",", ships.Select(ship =>
             $$"""{"ShipID":{{ship.Id}},"ShipType":"{{ship.Type}}","Name":"{{ship.Name}}","StarSystem":"Sol","ShipMarketID":{{CarrierId}},"TransferPrice":0,"TransferTime":0,"Value":1000,"Hot":false}"""))}}]}
        """;

    private static readonly string Stats =
        $$"""
        {"timestamp":"2026-09-05T12:00:00Z","event":"CarrierStats","CarrierID":{{CarrierId}},"Callsign":"K7Q-B4X",
         "Name":"Sacred Fire","CarrierType":"FleetCarrier","FuelLevel":0,
         "SpaceUsage":{"TotalCapacity":25000,"Crew":0,"Cargo":0,"CargoSpaceReserved":0,"ShipPacks":0,"ModulePacks":0,"FreeSpace":25000},
         "Crew":[]}
        """;

    private static async Task<(string Said, ChecklistService Checklists)> PlotAsync(
        TempInstall install,
        CarrierRoute route,
        params string[] events)
    {
        var gameState = new GameStateStore();

        foreach (var json in (string[])
                 [
                     """{"timestamp":"2026-09-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                     Stats,
                     .. events,
                 ])
        {
            Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
            gameState.Apply(parsed!);
        }

        var settings = TestSurface.For(install).Settings;
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var checklists = TestSurface.Checklists(install.Paths, gameState);
        var plans = new RoutePlanBook(Path.Combine(install.Root, "data", "route-plans.json"), new DiskFileSystem(), NullLogger<RoutePlanBook>.Instance);

        var registry = CapabilityRegistry.Build(
            [RouteCapability.Create(new Routes(route), null, () => gameState.Active, settings, plans, () => Now, null, checklists)]);

        var result = await registry.InvokeAsync(
            "plot_carrier_route",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["from"] = "Sol", ["to"] = "Colonia" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError, result.Content);

        return (result.Content, checklists);
    }

    [Fact]
    public async Task TheSolColoniaSolPlotOffersTheKitOneProposalPerLine()
    {
        using var install = new TempInstall();

        var (said, checklists) = await PlotAsync(install, SolColoniaSol());

        var waiting = checklists.Proposals.PendingFor("F1");

        Assert.Contains($"so I have proposed {waiting.Count} expedition kit lines", said, StringComparison.Ordinal);
        Assert.All(waiting, proposal =>
        {
            Assert.Equal(ProposalKind.Add, proposal.Kind);
            Assert.Equal(ChecklistSource.ExpeditionKit, proposal.Source);
            Assert.Single(proposal.Items);
        });

        Assert.Equal(
            [
                "Carry a tritium mining kit — laser, collector controllers, refinery — the only fuel out there is what you mine.",
                "AFMU and repair limpet controllers — nothing repairs modules out there but you.",
                "An SRV bay and spare SRVs — the one nobody counts until it is zero.",
                "Limpets on the carrier market — no station out there sells them.",
            ],
            waiting.Select(proposal => proposal.Items[0].Text));
    }

    [Fact]
    public async Task APlotInsideTheBubbleOffersNothing()
    {
        using var install = new TempInstall();

        var (said, checklists) = await PlotAsync(install, InsideTheBubble());

        Assert.DoesNotContain("expedition kit", said, StringComparison.Ordinal);
        Assert.Empty(checklists.Proposals.PendingFor("F1"));
    }

    [Fact]
    public async Task OneScooplessShipAtTheCarrierGetsOneScoopLineNamingIt()
    {
        using var install = new TempInstall();

        var (_, checklists) = await PlotAsync(
            install,
            SolColoniaSol(),
            Loadout(1, "Type9", "Mule", Bay, Scanner, Laser, Collector, Refinery),
            Loadout(2, "Krait_MkII", "Wanderer", Scoop, Bay, Scanner),
            Loadout(3, "Anaconda", "Elsewhere", Bay, Scanner),
            StoredAtTheCarrier((1, "Type9", "Mule"), (2, "Krait_MkII", "Wanderer")));

        var waiting = checklists.Proposals.PendingFor("F1");
        var scoops = waiting.Where(proposal => proposal.Items[0].Text.StartsWith("Store a fuel scoop", StringComparison.Ordinal)).ToList();

        var scoop = Assert.Single(scoops);

        Assert.StartsWith("Store a fuel scoop for Mule", scoop.Items[0].Text, StringComparison.Ordinal);
        Assert.EndsWith("— nothing out there sells one.", scoop.Items[0].Text, StringComparison.Ordinal);
        Assert.Equal(ChecklistScope.Ship(1), scoop.Scope);

        // The Mule is mining-fit, both ships carry an SRV bay and a scanner.
        Assert.DoesNotContain(waiting, proposal => proposal.Items[0].Text.StartsWith("Carry a tritium mining kit", StringComparison.Ordinal));
        Assert.DoesNotContain(waiting, proposal => proposal.Items[0].Text.StartsWith("An SRV bay", StringComparison.Ordinal));
        Assert.DoesNotContain(waiting, proposal => proposal.Items[0].Text.StartsWith("A Detailed Surface Scanner", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NothingIsOnTheChecklistUntilALineIsAcceptedAndAcceptingOneAddsExactlyThatLine()
    {
        using var install = new TempInstall();

        var (_, checklists) = await PlotAsync(install, SolColoniaSol());

        Assert.Empty(checklists.Document.Items);

        var limpets = checklists.Proposals.PendingFor("F1")
            .Single(proposal => proposal.Items[0].Text.StartsWith("Limpets", StringComparison.Ordinal));

        checklists.Accept(limpets.Id);

        var added = Assert.Single(checklists.Document.Items);

        Assert.Equal("Limpets on the carrier market — no station out there sells them.", added.Text);
        Assert.Equal(ChecklistItemKind.Authored, added.Kind);
        Assert.Equal(3, checklists.Proposals.PendingFor("F1").Count);
    }

    [Fact]
    public async Task PlottingAgainOffersTheSameLinesWithoutFilingThemTwice()
    {
        using var install = new TempInstall();

        var (first, checklists) = await PlotAsync(install, SolColoniaSol());
        var count = checklists.Proposals.PendingFor("F1").Count;

        var (second, again) = await PlotAsync(install, SolColoniaSol());

        Assert.Equal(count, again.Proposals.PendingFor("F1").Count);
        Assert.Contains($"proposed {count} expedition kit lines", second, StringComparison.Ordinal);
        Assert.Contains($"proposed {count} expedition kit lines", first, StringComparison.Ordinal);
    }
}
