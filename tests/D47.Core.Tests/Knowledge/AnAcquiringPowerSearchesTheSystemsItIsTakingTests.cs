using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Journal;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>acquiring_power finds the unoccupied systems a Power is taking, and stations can start past a distance (#2093).</summary>
public class AnAcquiringPowerSearchesTheSystemsItIsTakingTests
{
    private sealed class FakeGalaxy : StubGalaxyService
    {
        public GalaxyQuery? SystemQuery { get; private set; }

        public StationQuery? StationQuery { get; private set; }

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            SystemQuery = query;
            return Task.FromResult(new GalaxySearchResult("Sol", 0, []));
        }

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            StationQuery = query;
            return Task.FromResult(new StationSearchResult("Sol", 0, []));
        }
    }

    private static async Task<(ToolResult Result, FakeGalaxy Galaxy)> Run(
        string tool, CommanderGameState? state, params (string Name, string Value)[] values)
    {
        var galaxy = new FakeGalaxy();
        var settings = TestSurface.For(new MemoryInstall()).Settings;
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var registry = CapabilityRegistry.Build(
            [GalaxyCapability.Create(galaxy, () => "Sol", settings, gameState: () => state)]);

        var result = await registry.InvokeAsync(
            tool,
            new ToolArguments(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

        return (result, galaxy);
    }

    private static CommanderGameState Pledged() => JournalScript.Commander(
        "F123",
        "Jameson",
        """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Aisling Duval","Rank":5,"Merits":1000}""");

    [Fact]
    public void ItIsSentAsThePowerFieldForSystemsAndSystemPowerForStations()
    {
        var filter = GalaxyFilters.Find("acquiring_power")!;

        Assert.Equal("power", filter.FieldOn(GalaxySearchKind.Systems));
        Assert.Equal("system_power", filter.FieldOn(GalaxySearchKind.Stations));
        Assert.False(filter.Honours(GalaxySearchKind.Bodies));
    }

    [Fact]
    public async Task MineBecomesThePledgedPower()
    {
        var (result, galaxy) = await Run(
            "search_systems", Pledged(), ("acquiring_power", "mine"), ("power_state", "Unoccupied"), ("distance", "200-"));

        Assert.False(result.IsError, result.Content);

        var criteria = galaxy.SystemQuery!.Criteria;
        Assert.Equal(["Aisling Duval"], criteria.Single(c => c.Filter.Name == "acquiring_power").Choices);
        Assert.Equal(200, criteria.Single(c => c.Filter.Name == "distance").Min);
        Assert.Null(criteria.Single(c => c.Filter.Name == "distance").Max);
        Assert.Equal(["Unoccupied"], criteria.Single(c => c.Filter.Name == "power_state").Choices);
    }

    [Theory]
    [InlineData("search_systems")]
    [InlineData("search_stations")]
    public async Task AnUnpledgedCommanderGetsASentenceAndNoSearch(string tool)
    {
        var (result, galaxy) = await Run(tool, null, ("acquiring_power", "mine"), ("power_state", "Unoccupied"));

        Assert.True(result.IsError);
        Assert.Contains("isn't pledged", result.Content);
        Assert.Null(galaxy.SystemQuery);
        Assert.Null(galaxy.StationQuery);
    }

    [Fact]
    public async Task AStationSearchResolvesMineAndStartsPastTheMinimumDistance()
    {
        var (result, galaxy) = await Run(
            "search_stations", Pledged(), ("acquiring_power", "mine"), ("min_distance", "200"));

        Assert.False(result.IsError, result.Content);

        var query = galaxy.StationQuery!;
        Assert.Equal(200, query.MinDistance);
        Assert.True(query.Unbounded);
        Assert.Equal(["Aisling Duval"], query.Criteria.Single().Choices);
    }
}
