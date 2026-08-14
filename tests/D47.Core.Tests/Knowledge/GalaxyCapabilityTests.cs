using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The model's view of galaxy search: what it can ask for, what it is told when it asks for
/// something that does not exist, and what happens when the service is unreachable or off.
/// </summary>
public class GalaxyCapabilityTests
{
    /// <summary>A service that answers from a script, and records what it was asked.</summary>
    private sealed class FakeGalaxy : IGalaxyService
    {
        public GalaxyQuery? LastQuery { get; private set; }

        public GalaxySearchResult Result { get; set; } =
            new("Sol", 1, [new SystemSummary { Name = "Alpha Centauri", Distance = 4.38 }]);

        public double? Distance { get; set; } = 22_000.5;

        public Exception? Throws { get; set; }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;

            return Throws is not null ? Task.FromException<GalaxySearchResult>(Throws) : Task.FromResult(Result);
        }

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Throws is not null ? Task.FromException<double?>(Throws) : Task.FromResult(Distance);

        public StationQuery? LastStationQuery { get; private set; }

        public StationSearchResult Stations { get; set; } =
            new("Sol", 1, [new StationSummary { Name = "Jameson Memorial", SystemName = "Shinrarta Dezhra" }]);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            LastStationQuery = query;

            return Throws is not null
                ? Task.FromException<StationSearchResult>(Throws)
                : Task.FromResult(Stations);
        }

        public BodyQuery? LastBodyQuery { get; private set; }

        public BodySearchResult Bodies { get; set; } =
            new("Sol", 1, [new BodySummary { Name = "Earth", SystemName = "Sol" }]);

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            LastBodyQuery = query;

            return Throws is not null
                ? Task.FromException<BodySearchResult>(Throws)
                : Task.FromResult(Bodies);
        }
    }

    private static (CapabilityRegistry Registry, FakeGalaxy Galaxy) Build(
        TempInstall install,
        bool enabled = true,
        string? currentSystem = "Sol")
    {
        var galaxy = new FakeGalaxy();
        var settings = TestSurface.For(install).Settings;

        settings.Apply(GalaxyCapability.EnabledKey, enabled ? "true" : "false", SettingsCaller.Panel);

        var registry = CapabilityRegistry.Build(
            [GalaxyCapability.Create(galaxy, () => currentSystem, settings)]);

        return (registry, galaxy);
    }

    private static ToolArguments Args(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal));

    [Fact]
    public async Task AFilterTheServiceWouldSilentlyIgnoreNeverReachesIt()
    {
        // The load-bearing one: an unknown filter has to stop here, because it does not stop
        // anywhere downstream.
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        var result = await registry.InvokeAsync(
            "search_systems",
            Args(("distance", "20"), ("allegiance", "Klingon")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Null(galaxy.LastQuery);
    }

    [Fact]
    public async Task ASearchWithNoFiltersIsRefusedRatherThanMatchingTheGalaxy()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        var result = await registry.InvokeAsync("search_systems", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("whole galaxy", result.Content, StringComparison.Ordinal);
        Assert.Null(galaxy.LastQuery);
    }

    [Fact]
    public async Task WhereTheCommanderIsBecomesTheReferenceWhenNoneIsNamed()
    {
        // "The nearest high tech system" cannot be asked without saying where from.
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install, currentSystem: "Shinrarta Dezhra");

        await registry.InvokeAsync("search_systems", Args(("distance", "20")), TestContext.Current.CancellationToken);

        Assert.Equal("Shinrarta Dezhra", galaxy.LastQuery?.ReferenceSystem);
    }

    [Fact]
    public async Task AnExplicitReferenceOutranksWhereTheCommanderIs()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install, currentSystem: "Sol");

        await registry.InvokeAsync(
            "search_systems",
            Args(("distance", "20"), ("near", "Colonia")),
            TestContext.Current.CancellationToken);

        Assert.Equal("Colonia", galaxy.LastQuery?.ReferenceSystem);
    }

    [Fact]
    public async Task TheSummarySaysHowManyWereLeftOut()
    {
        // "412 matched; here are the nearest 5" is a different answer from "there are 5".
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Result = new GalaxySearchResult(
            "Sol",
            412,
            [new SystemSummary { Name = "Alpha Centauri", Distance = 4.38, Allegiance = "Federation" }]);

        var result = await registry.InvokeAsync(
            "search_systems",
            Args(("distance", "20")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("412", result.Content, StringComparison.Ordinal);
        Assert.Contains("Alpha Centauri", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSummaryNamesWhatDistancesWereMeasuredFrom()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Result = new GalaxySearchResult("Colonia", 1, [new SystemSummary { Name = "Ratraii" }]);

        var result = await registry.InvokeAsync(
            "search_systems",
            Args(("distance", "20")),
            TestContext.Current.CancellationToken);

        Assert.Contains("measured from Colonia", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreachableServiceIsAnErrorResultNotAnException()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Throws = new GalaxyUnavailableException("I couldn't reach the galaxy search.");

        var result = await registry.InvokeAsync(
            "search_systems",
            Args(("distance", "20")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("couldn't reach", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SwitchedOffIsACapabilityBeingOffRatherThanAFailure()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install, enabled: false);

        var result = await registry.InvokeAsync(
            "search_systems",
            Args(("distance", "20")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("switched off", result.Content, StringComparison.Ordinal);
        Assert.Null(galaxy.LastQuery);
    }

    [Fact]
    public async Task DistanceIsMeasuredFromTheCommanderWhenOnlyADestinationIsGiven()
    {
        using var install = new TempInstall();
        var (registry, _) = Build(install, currentSystem: "Sol");

        var result = await registry.InvokeAsync(
            "distance_between",
            Args(("to", "Colonia")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("Colonia", result.Content, StringComparison.Ordinal);
        Assert.Contains("Sol", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownSystemSaysSoRatherThanReportingADistance()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Distance = null;

        var result = await registry.InvokeAsync(
            "distance_between",
            Args(("to", "Nowhere At All")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("couldn't find", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AModuleNameIsMatchedAgainstTheRealCatalogue()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        // Said the way a Commander says it, not the way the catalogue spells it.
        var result = await registry.InvokeAsync(
            "find_nearest_station",
            Args(("module", "frame shift drive"), ("module_class", "5"), ("module_rating", "A")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("Frame Shift Drive", galaxy.LastStationQuery?.Module);
        Assert.Equal("5", galaxy.LastStationQuery?.ModuleClass);
    }

    [Fact]
    public async Task AModuleNobodyHasHeardOfIsRefusedWithSuggestionsRatherThanSearchedFor()
    {
        // The service would honour this by returning nothing, and "nowhere sells a Frame Shift
        // Drve" is a false statement about the galaxy rather than an answer.
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        var result = await registry.InvokeAsync(
            "find_nearest_station",
            Args(("module", "Frame Shift Drve")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("Frame Shift Drive", result.Content, StringComparison.Ordinal);
        Assert.Null(galaxy.LastStationQuery);
    }

    [Fact]
    public async Task AskingForNeitherAModuleNorAShipIsRefused()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        var result = await registry.InvokeAsync(
            "find_nearest_station",
            Args(("max_distance", "50")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Null(galaxy.LastStationQuery);
    }

    [Fact]
    public async Task TheAgeOfTheStockReportIsPartOfTheAnswer()
    {
        // Crowd-reported data read as current is how a Commander flies 200 light years for a
        // module that is not on the shelf.
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Stations = new StationSearchResult(
            "Sol",
            1,
            [
                new StationSummary
                {
                    Name = "Jameson Memorial",
                    SystemName = "Shinrarta Dezhra",
                    Distance = 22.5,
                    StockLastSeen = new DateTimeOffset(2023, 4, 1, 0, 0, 0, TimeSpan.Zero),
                },
            ]);

        var result = await registry.InvokeAsync(
            "find_nearest_station",
            Args(("module", "Frame Shift Drive")),
            TestContext.Current.CancellationToken);

        Assert.Contains("2023-04-01", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoKnownLocationAndNoOriginTheToolAsksForOne()
    {
        using var install = new TempInstall();
        var (registry, _) = Build(install, currentSystem: null);

        var result = await registry.InvokeAsync(
            "distance_between",
            Args(("to", "Colonia")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("where the Commander is", result.Content, StringComparison.Ordinal);
    }

    // ---- Bodies and signals ---------------------------------------------------------------

    [Fact]
    public async Task ABodyTypeIsMatchedFromWhatAPersonWouldActuallySay()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        // "Earth-like" names exactly one subtype, so the unique-fragment pass takes it. Nobody
        // says "Earth-like world" out loud.
        var result = await registry.InvokeAsync(
            "find_body",
            Args(("body_type", "earth-like")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("Earth-like world", galaxy.LastBodyQuery?.Subtype);
    }

    [Fact]
    public async Task AnAmbiguousBodyTypeIsRefusedWithTheCandidatesRatherThanPickingOne()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        // "gas giant" names six subtypes. Picking one silently is how a Commander is told about
        // the wrong thing with total confidence.
        var result = await registry.InvokeAsync(
            "find_body",
            Args(("body_type", "gas giant")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("Class I gas giant", result.Content, StringComparison.Ordinal);
        Assert.Null(galaxy.LastBodyQuery);
    }

    [Fact]
    public async Task ABodySearchWithNoFiltersIsRefusedRatherThanMatchingEveryBody()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        var result = await registry.InvokeAsync("find_body", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Null(galaxy.LastBodyQuery);
    }

    [Fact]
    public async Task ASignalCountWithNoSignalToCountIsRefused()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        // The service's `count` member means nothing without a `name` beside it — sent alone it
        // returned zero results rather than being ignored, which would read as "nowhere".
        var result = await registry.InvokeAsync(
            "find_body",
            Args(("signal_count", "3")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("which surface signal", result.Content, StringComparison.Ordinal);
        Assert.Null(galaxy.LastBodyQuery);
    }

    [Fact]
    public async Task LeavingLandableOutIsNotTheSameAsAskingForUnlandableBodies()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        await registry.InvokeAsync(
            "find_body",
            Args(("body_type", "Water world")),
            TestContext.Current.CancellationToken);

        Assert.Null(galaxy.LastBodyQuery?.Landable);

        await registry.InvokeAsync(
            "find_body",
            Args(("body_type", "Water world"), ("landable", "false")),
            TestContext.Current.CancellationToken);

        Assert.False(galaxy.LastBodyQuery?.Landable);
    }

    [Fact]
    public async Task AHotspotAnswerCarriesTheRingsAndHowOldTheReportIs()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Bodies = new BodySearchResult(
            "Sol",
            1,
            [
                new BodySummary
                {
                    Name = "Barnard's Star 5",
                    SystemName = "Barnard's Star",
                    Distance = 5.95,
                    ReserveLevel = "Depleted",
                    Rings =
                    [
                        new RingSummary("Barnard's Star 5 A Ring", "Metal Rich")
                        {
                            Hotspots = [("Painite", 2), ("Platinum", 1)],
                            SignalsSeen = new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero),
                        },
                        new RingSummary("Barnard's Star 5 B Ring", "Icy") { Hotspots = [("Tritium", 1)] },
                    ],
                },
            ]);

        var result = await registry.InvokeAsync(
            "find_body",
            Args(("hotspot", "painite")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("2 Painite", result.Content, StringComparison.Ordinal);
        Assert.Contains("reported 2026-08-11", result.Content, StringComparison.Ordinal);
        Assert.Contains("depleted reserves", result.Content, StringComparison.Ordinal);

        // The other ring around the same planet has no Painite in it, and naming it invites the
        // Commander to fly to the wrong one.
        Assert.DoesNotContain("B Ring", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASurfaceSearchReportsSignalsRatherThanRings()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        galaxy.Bodies = new BodySearchResult(
            "Sol",
            1,
            [
                new BodySummary
                {
                    Name = "Luhman 16 B 3",
                    SystemName = "Luhman 16",
                    Distance = 6.5,
                    IsLandable = true,
                    Signals = [("Biological", 4), ("Geological", 2)],
                    Rings = [new RingSummary("Luhman 16 B 3 A Ring", "Icy") { Hotspots = [("Tritium", 1)] }],
                },
            ]);

        var result = await registry.InvokeAsync(
            "find_body",
            Args(("signal", "biological")),
            TestContext.Current.CancellationToken);

        Assert.Contains("4 biological", result.Content, StringComparison.Ordinal);
        Assert.Contains("landable", result.Content, StringComparison.Ordinal);

        // A body search about surfaces should not spend its answer on the other question's rings.
        Assert.DoesNotContain("A Ring", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABodySearchIsOffWithTheRestOfTheGalaxySearch()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install, enabled: false);

        var result = await registry.InvokeAsync(
            "find_body",
            Args(("body_type", "Neutron Star")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("switched off", result.Content, StringComparison.Ordinal);
        Assert.Null(galaxy.LastBodyQuery);
    }
}
