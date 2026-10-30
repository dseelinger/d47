using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>max_report_age_hours reaches the query, is refused out of range, and is named in the answer (#1769).</summary>
public class AReportAgeBoundIsAppliedAndSaidTests
{
    private sealed class FakeGalaxy : IGalaxyService
    {
        public StationQuery? LastQuery { get; private set; }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;

            return Task.FromResult(new StationSearchResult("Sol", 0, []));
        }

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static (CapabilityRegistry Registry, FakeGalaxy Galaxy) Build()
    {
        var galaxy = new FakeGalaxy();
        var settings = TestSurface.For(new MemoryInstall()).Settings;

        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        return (CapabilityRegistry.Build([GalaxyCapability.Create(galaxy, () => "Sol", settings)]), galaxy);
    }

    private static Task<ToolResult> Ask(CapabilityRegistry registry, string tool, params (string Name, string Value)[] values) =>
        registry.InvokeAsync(
            tool,
            new ToolArguments(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("search_stations", "station_type", "Starport", "max_report_age_hours")]
    [InlineData("search_outfitting", "items", "Fuel Scoop", "max_report_age_hours")]
    [InlineData("find_nearest_station", "module", "Frame Shift Drive", "max_price_age_hours")]
    public async Task TheBoundReachesTheQueryAndTheAnswerNamesIt(string tool, string key, string value, string age)
    {
        var (registry, galaxy) = Build();

        var result = await Ask(registry, tool, (key, value), (age, "48"));

        Assert.False(result.IsError, result.Content);
        Assert.Equal(48, galaxy.LastQuery!.MaxReportAgeHours);
        Assert.Contains("Only reports from the last 2 days were considered.", result.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("search_stations", "station_type", "Starport", "max_report_age_hours")]
    [InlineData("find_nearest_station", "module", "Frame Shift Drive", "max_price_age_hours")]
    public async Task ZeroAgeSendsNoBound(string tool, string key, string value, string age)
    {
        var (registry, galaxy) = Build();

        var result = await Ask(registry, tool, (key, value), (age, "0"));

        Assert.Null(galaxy.LastQuery!.MaxReportAgeHours);
        Assert.DoesNotContain("Only reports", result.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("search_stations", "station_type", "Starport")]
    [InlineData("search_outfitting", "items", "Fuel Scoop")]
    [InlineData("find_nearest_station", "module", "Frame Shift Drive")]
    public async Task NoAgeSearchesThreeDaysAndSaysSo(string tool, string key, string value)
    {
        var (registry, galaxy) = Build();

        var result = await Ask(registry, tool, (key, value));

        Assert.Equal(72, galaxy.LastQuery!.MaxReportAgeHours);
        Assert.Contains("Only reports from the last 3 days were considered.", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAgeAboveTheCeilingIsRefusedAndNothingIsSearched()
    {
        var (registry, galaxy) = Build();

        var result = await Ask(registry, "search_stations", ("station_type", "Starport"), ("max_report_age_hours", "9000"));

        Assert.True(result.IsError);
        Assert.Contains("8,760", result.Content, StringComparison.Ordinal);
        Assert.Null(galaxy.LastQuery);
    }
}
