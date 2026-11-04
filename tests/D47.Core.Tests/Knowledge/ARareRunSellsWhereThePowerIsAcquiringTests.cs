using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Journal;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>find_rare_run asks for the acquisition systems beyond each rare, one search per rare (#2095).</summary>
public class ARareRunSellsWhereThePowerIsAcquiringTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeIndex : IRareIndexSource
    {
        public Task<RareIndex?> GetAsync(CancellationToken cancellationToken) => Task.FromResult<RareIndex?>(null);
    }

    private sealed class FakeGalaxy(Func<string, IReadOnlyList<SystemSummary>> systems) : StubGalaxyService
    {
        public List<GalaxyQuery> Queries { get; } = [];

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            Queries.Add(query);
            var found = systems(query.ReferenceSystem!);

            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, found.Count, found));
        }
    }

    private static IReadOnlyList<SystemSummary> ThreeSystemsBeyond(string rareSystem) =>
    [
        System($"{rareSystem} A", 201, 0.25, Station("Carrier", "Drake-Class Carrier"), Station($"{rareSystem} Port")),
        System($"{rareSystem} B", 202, 0.5, Station($"{rareSystem} Hub", market: false), Station($"{rareSystem} Dock")),
        System($"{rareSystem} C", 203, 0, Station($"{rareSystem} Base"), Station($"{rareSystem} Spare")),
    ];

    private static SystemSummary System(string name, double distance, double progress, params SystemStation[] stations) =>
        new()
        {
            Name = name,
            Distance = distance,
            PowerState = "Unoccupied",
            Acquiring = [new PowerConflict("Aisling Duval", progress)],
            Stations = stations,
        };

    private static SystemStation Station(string name, string type = "Outpost", bool market = true) =>
        new(name, type, market, 100);

    private static CommanderGameState Pledged() => JournalScript.Commander(
        "F123",
        "Jameson",
        """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Aisling Duval","Rank":5,"Merits":1000}""");

    private static async Task<ToolResult> Run(
        IGalaxyService galaxy,
        CommanderGameState? state,
        params (string Name, string Value)[] values)
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(
                galaxy,
                () => "Nowhere",
                settings,
                now: () => Now,
                gameState: () => state,
                currentPosition: () => StarPosition.Origin,
                rareIndex: new FakeIndex()),
        ]);

        return await registry.InvokeAsync(
            "find_rare_run",
            new ToolArguments(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);
    }

    private static RareEntry[] NearestRares(int count) =>
    [
        .. RareListing.Within(StarPosition.Origin, double.MaxValue, null, "distance", descending: false)
            .Take(count)
            .Select(row => row.Rare),
    ];

    [Fact]
    public async Task ANamedRareIsOneSearchFromItsSystem()
    {
        var rare = RareCatalogue.All.First();
        var galaxy = new FakeGalaxy(ThreeSystemsBeyond);

        var result = await Run(galaxy, Pledged(), ("rare", rare.Name));

        Assert.False(result.IsError, result.Content);

        var query = Assert.Single(galaxy.Queries);
        Assert.Equal(rare.System, query.ReferenceSystem);
        Assert.Equal(["Aisling Duval"], query.Criteria.Single(c => c.Filter.Name == "acquiring_power").Choices);
        Assert.Equal(["Unoccupied"], query.Criteria.Single(c => c.Filter.Name == "power_state").Choices);
        Assert.Equal(200, query.Criteria.Single(c => c.Filter.Name == "distance").Min);
        Assert.Null(query.Criteria.Single(c => c.Filter.Name == "distance").Max);

        Assert.Contains(rare.Name, result.Content, StringComparison.Ordinal);
        Assert.Contains($"{rare.System} Port in {rare.System} A, 201.0 light years, acquisition 25%", result.Content, StringComparison.Ordinal);
        Assert.Contains($"{rare.System} Dock in {rare.System} B, 202.0 light years, acquisition 50%", result.Content, StringComparison.Ordinal);
        Assert.Contains($"{rare.System} Base in {rare.System} C", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Carrier in", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain($"{rare.System} Hub", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain($"{rare.System} Spare", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoRareIsFiveSearchesNearestFirst()
    {
        var galaxy = new FakeGalaxy(ThreeSystemsBeyond);

        var result = await Run(galaxy, Pledged());

        Assert.False(result.IsError, result.Content);
        Assert.Equal(
            [.. NearestRares(5).Select(rare => rare.System)],
            galaxy.Queries.Select(query => query.ReferenceSystem ?? string.Empty).ToArray());
    }

    [Fact]
    public async Task AnUnpledgedCommanderIsToldSoAndNothingIsSearched()
    {
        var result = await Run(new StubGalaxyService(), null);

        Assert.True(result.IsError);
        Assert.Contains("isn't pledged", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANamedPowerNeedsNoPledge()
    {
        var galaxy = new FakeGalaxy(ThreeSystemsBeyond);

        var result = await Run(galaxy, null, ("power", "zemina torval"), ("max_rares", "1"), ("min_distance", "150"));

        Assert.False(result.IsError, result.Content);

        var query = Assert.Single(galaxy.Queries);
        Assert.Equal(["Zemina Torval"], query.Criteria.Single(c => c.Filter.Name == "acquiring_power").Choices);
        Assert.Equal(150, query.Criteria.Single(c => c.Filter.Name == "distance").Min);
    }

    [Fact]
    public async Task ARareWithNoAcquisitionStationInRangeSaysSo()
    {
        var rare = RareCatalogue.All.First();
        var galaxy = new FakeGalaxy(_ => []);

        var result = await Run(galaxy, Pledged(), ("rare", rare.Name));

        Assert.False(result.IsError, result.Content);
        Assert.Contains(
            "No system Aisling Duval is acquiring is reported beyond 200 light years of it.",
            result.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheVoiceAnswerNamesThreeStationsAtMost()
    {
        var galaxy = new FakeGalaxy(ThreeSystemsBeyond);

        var result = await Run(galaxy, Pledged());

        static int Stops(string text) => text.Split(", acquisition ").Length - 1;

        Assert.False(result.IsError, result.Content);
        Assert.Equal(3, Stops(result.Content));
        Assert.Contains("4 more rares are planned, with 12 stations between them.", result.Content, StringComparison.Ordinal);
    }
}
