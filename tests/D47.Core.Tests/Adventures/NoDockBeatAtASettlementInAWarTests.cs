using System.Globalization;
using System.Text.Json;
using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class NoDockBeatAtASettlementInAWarTests
{
    private const string Ltt = "LTT 7786";

    private static readonly DateTimeOffset GalaxyReported = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly StationSummary Settlement =
        new() { Name = "Wolff's Haven", SystemName = Ltt, MarketId = 1, Type = "Settlement" };

    private static readonly StationSummary Starport =
        new() { Name = "Bamford Port", SystemName = Ltt, MarketId = 2, Type = "Coriolis Starport" };

    [Fact]
    public async Task ASettlementInASystemWithAnActiveWarIsRefused()
    {
        var resolution = await ResolveAsync(Settlement, Standings("war", "active", "2026-10-02T14:41:19Z"));

        Assert.False(resolution.Succeeded);
        Assert.Contains("Wolff's Haven", resolution.Refusal);
        Assert.Contains("at war", resolution.Refusal);
    }

    [Fact]
    public async Task AStarportInTheSameSystemIsAccepted()
    {
        var resolution = await ResolveAsync(Starport, Standings("war", "active", "2026-10-02T14:41:19Z"));

        Assert.True(resolution.Succeeded);
    }

    [Fact]
    public async Task ASettlementWhereTheOnlyConflictIsAnElectionIsAccepted()
    {
        var resolution = await ResolveAsync(Settlement, Standings("election", "active", "2026-10-02T14:41:19Z"));

        Assert.True(resolution.Succeeded);
    }

    [Fact]
    public async Task ANewerJournalReadingWithNoWarOverridesAnOlderGalaxyRecordOfOne()
    {
        var resolution = await ResolveAsync(Settlement, Standings("civilwar", string.Empty, "2026-10-02T14:41:19Z"), galaxyAtWar: true);

        Assert.True(resolution.Succeeded);
    }

    [Fact]
    public async Task ANewerGalaxyRecordOfAWarOverridesAnOlderJournalReading()
    {
        var resolution = await ResolveAsync(Settlement, Standings("election", "active", "2026-09-20T10:00:00Z"), galaxyAtWar: true);

        Assert.False(resolution.Succeeded);
    }

    private static SystemStandings Standings(string warType, string status, string timestamp)
    {
        using var document = JsonDocument.Parse($$$"""
            {"timestamp":"{{{timestamp}}}","event":"Location","StarSystem":"{{{Ltt}}}",
             "Factions":[{"Name":"Red","Influence":0.5},{"Name":"Blue","Influence":0.5}],
             "Conflicts":[{"WarType":"{{{warType}}}","Status":"{{{status}}}",
               "Faction1":{"Name":"Red","Stake":"","WonDays":0},
               "Faction2":{"Name":"Blue","Stake":"","WonDays":0}}]}
            """);

        return SystemStandings.Empty.Apply(new JournalEvent(DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture), "Location", document.RootElement.Clone()));
    }

    private static Task<Resolution> ResolveAsync(StationSummary station, SystemStandings standings, bool galaxyAtWar = false) =>
        new AdventureResolver(new Galaxy(station, galaxyAtWar), standings)
            .ResolveAsync(TriggerKind.Dock, Ltt, station.Name, null, "Beat 1", null, CancellationToken.None);

    private sealed class Galaxy(StationSummary station, bool atWar) : IGalaxyService
    {
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult(
                query.ReferenceSystem, 1, [new SystemSummary { Name = Ltt, SystemAddress = 1, Distance = 0, AtWar = atWar, ReportedAt = GalaxyReported }]));

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => Task.FromResult<double?>(1);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult(query.ReferenceSystem, 1, [station]));

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, 0, []));

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
