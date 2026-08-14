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
}
