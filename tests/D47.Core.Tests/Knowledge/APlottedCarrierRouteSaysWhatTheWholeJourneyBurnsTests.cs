using System.Text.Json;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

[Trait("Category", "Integration")]
public class APlottedCarrierRouteSaysWhatTheWholeJourneyBurnsTests
{
    /// <summary>Answers with the recorded Sol, Colonia, Sol plot for the used capacity it is sent.</summary>
    private sealed class RecordedCarrierRoutes : IRouteService
    {
        public CarrierRouteQuery? Last { get; private set; }

        public Task<PlottedRoute?> PlotAsync(RouteQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<PlottedRoute?>(null);

        public Task<RichesRoute?> PlotRichesAsync(RichesQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<RichesRoute?>(null);

        public Task<ExobiologyRoute?> PlotExobiologyAsync(ExobiologyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<ExobiologyRoute?>(null);

        public Task<CarrierRoute?> PlotCarrierAsync(CarrierRouteQuery query, CancellationToken cancellationToken)
        {
            Last = query;

            var fixture = query.CapacityUsed switch
            {
                0 => "spansh-fleetcarrier-route-sol-colonia-sol.json",
                10_000 => "spansh-fleetcarrier-route-sol-colonia-sol-10000t.json",
                _ => throw new InvalidOperationException($"No recording at {query.CapacityUsed} t used."),
            };

            return Task.FromResult<CarrierRoute?>(Read(fixture));
        }

        private static CarrierRoute Read(string fixture)
        {
            using var document = EmbeddedFixture.Json(fixture);

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
                    jump.GetProperty("is_desired_destination").GetInt32() != 0))]);
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private static string Stats(int freeSpace, string type = "FleetCarrier") =>
        $$"""
        {"timestamp":"2026-09-05T12:00:00Z","event":"CarrierStats","CarrierID":3700000000,"Callsign":"K7Q-B4X",
         "Name":"Sacred Fire","CarrierType":"{{type}}","FuelLevel":0,
         "SpaceUsage":{"TotalCapacity":25000,"Crew":0,"Cargo":{{25_000 - freeSpace}},"CargoSpaceReserved":0,
                       "ShipPacks":0,"ModulePacks":0,"FreeSpace":{{freeSpace}}},
         "Crew":[]}
        """;

    private static (CapabilityRegistry Registry, RecordedCarrierRoutes Routes, RoutePlanBook Plans) Build(
        TempInstall install,
        params string[] events)
    {
        var gameState = new GameStateStore();

        foreach (var json in (string[])
                 [
                     """{"timestamp":"2026-09-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                     .. events,
                 ])
        {
            Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
            gameState.Apply(parsed!);
        }

        var routes = new RecordedCarrierRoutes();
        var settings = TestSurface.For(install).Settings;
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var plans = new RoutePlanBook(
            Path.Combine(install.Root, "data", "route-plans.json"),
            NullLogger<RoutePlanBook>.Instance);

        return (
            CapabilityRegistry.Build(
                [RouteCapability.Create(routes, null, () => gameState.Active, settings, plans, () => Now)]),
            routes,
            plans);
    }

    private static ToolArguments Args(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal));

    [Fact]
    public async Task ThePlotIsKeptAsTheCarriersOwnPlanAndTheAnswerGivesTheTotalAndTheReadingsAge()
    {
        using var install = new TempInstall();
        var (registry, routes, plans) = Build(install, Stats(freeSpace: 25_000));

        var result = await registry.InvokeAsync(
            "plot_carrier_route",
            Args(("from", "Sol"), ("to", "Colonia")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError, result.Content);
        Assert.Equal(["Colonia", "Sol"], routes.Last?.Destinations);
        Assert.Contains("Sol to Colonia and back: 90 jumps, 6,084 t of tritium for the whole journey.", result.Content, StringComparison.Ordinal);
        Assert.Contains("and 4 more stops.", result.Content, StringComparison.Ordinal);
        Assert.Contains("40 waypoints have a pristine icy ring", result.Content, StringComparison.Ordinal);
        Assert.Contains("read from carrier management 2 days ago", result.Content, StringComparison.Ordinal);
        Assert.Contains("The tritium in the hold is not known, so the plot assumed none.", result.Content, StringComparison.Ordinal);

        var kept = plans.Last(RoutePlanKind.Carrier);

        Assert.NotNull(kept);
        Assert.Equal(3700000000, kept.CarrierId);
        Assert.Equal(Now, kept.PlottedAt);
        Assert.Equal("Sol to Colonia and back", kept.Headline);
        Assert.Equal(92, kept.Carrier?.Waypoints.Count);
        Assert.Equal(new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero), kept.CarrierStatsSeenAt);
    }

    [Fact]
    public async Task TenThousandTonnesMoreAboardStatesALargerTotal()
    {
        using var empty = new TempInstall();
        using var laden = new TempInstall();

        var light = await Build(empty, Stats(freeSpace: 25_000)).Registry.InvokeAsync(
            "plot_carrier_route",
            Args(("from", "Sol"), ("to", "Colonia")),
            TestContext.Current.CancellationToken);

        var heavy = await Build(laden, Stats(freeSpace: 15_000)).Registry.InvokeAsync(
            "plot_carrier_route",
            Args(("from", "Sol"), ("to", "Colonia")),
            TestContext.Current.CancellationToken);

        Assert.Contains("6,084 t of tritium", light.Content, StringComparison.Ordinal);
        Assert.Contains("8,300 t of tritium", heavy.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASquadronCarrierIsRefusedAndNothingIsKept()
    {
        using var install = new TempInstall();
        var (registry, routes, plans) = Build(install, Stats(freeSpace: 25_000, type: "SquadronCarrier"));

        var result = await registry.InvokeAsync(
            "plot_carrier_route",
            Args(("from", "Sol"), ("to", "Colonia")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("I don't know of a fleet carrier the Commander owns.", result.Content);
        Assert.Null(routes.Last);
        Assert.Null(plans.Last(RoutePlanKind.Carrier));
    }

    [Fact]
    public async Task ACarrierWhoseHoldWasNeverReadIsRefusedAndNothingIsKept()
    {
        using var install = new TempInstall();
        var (registry, routes, plans) = Build(
            install,
            """{"timestamp":"2026-09-05T12:00:00Z","event":"CarrierBuy","CarrierID":3700000000,"Callsign":"K7Q-B4X","Location":"Sol","BoughtAtMarket":1,"Price":5000000000,"Variant":"CarrierDockB"}""");

        var result = await registry.InvokeAsync(
            "plot_carrier_route",
            Args(("to", "Colonia")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("Open carrier management once so I can read the hold.", result.Content);
        Assert.Null(routes.Last);
        Assert.Null(plans.Last(RoutePlanKind.Carrier));
    }
}
