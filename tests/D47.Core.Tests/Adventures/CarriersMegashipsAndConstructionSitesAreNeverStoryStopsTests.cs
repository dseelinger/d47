using D47.Core.Adventures;
using D47.Core.Knowledge;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Stories;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class CarriersMegashipsAndConstructionSitesAreNeverStoryStopsTests
{
    private const string NeverStops = "Fleet carriers, megaships and construction sites are never story stops.";

    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly StationSummary[] NotStops =
    [
        Station("K7Q-1HT", "Drake-Class Carrier"),
        Station("Gnosis", "Mega ship"),
        Station("Hale Depot", "Space Construction Depot"),
        Station("Orbital Construction Site: Hale's Reach", "Coriolis Starport"),
    ];

    [Fact]
    public async Task ADockBeatAtAStationTheFilteredSearchDoesNotReturnIsRefused()
    {
        var galaxy = new AdventureGeneratorTests.Galaxy();

        var resolution = await new AdventureResolver(galaxy)
            .ResolveAsync(TriggerKind.Dock, "Dyson's Hollow", "K7Q-1HT", null, "Beat 1", null, CancellationToken.None);

        Assert.False(resolution.Succeeded);
        Assert.Contains("names a station, \"K7Q-1HT\", that is not in Dyson's Hollow.", resolution.Refusal);
        Assert.Contains(NeverStops, resolution.Refusal);
        Assert.True(Assert.Single(galaxy.StationQueries).FixedOnly);
    }

    [Theory]
    [InlineData("Planetary Construction Site: Hale's Reach", "Planetary Outpost")]
    [InlineData("K7Q-1HT", "Drake-Class Carrier")]
    [InlineData("Gnosis", "Mega ship")]
    [InlineData("Hale Depot", "Planetary Construction Depot")]
    [InlineData("Quiet Rest", null)]
    public async Task AReturnedStationThatIsNotAFixedFinishedStationIsRefusedTheSameWay(string name, string? type)
    {
        var galaxy = new AdventureGeneratorTests.Galaxy { Extra = [Station(name, type)] };

        var resolution = await new AdventureResolver(galaxy)
            .ResolveAsync(TriggerKind.Dock, "Dyson's Hollow", name, null, "Beat 1", null, CancellationToken.None);

        Assert.False(resolution.Succeeded);
        Assert.Contains($"\"{name}\", that is not in Dyson's Hollow.", resolution.Refusal);
        Assert.Contains(NeverStops, resolution.Refusal);
    }

    [Fact]
    public async Task AFixedStationInTheSameSystemStillResolves()
    {
        var galaxy = new AdventureGeneratorTests.Galaxy { Extra = NotStops };

        var resolution = await new AdventureResolver(galaxy)
            .ResolveAsync(TriggerKind.Dock, "Dyson's Hollow", "Maren Anchorage", null, "Beat 1", null, CancellationToken.None);

        Assert.True(resolution.Succeeded, resolution.Refusal);
    }

    [Fact]
    public async Task TheStationSearchForCandidatesIsFixedOnlyAndNoCarrierMegashipOrConstructionSiteIsPlannedAsADock()
    {
        var galaxy = new AdventureGeneratorTests.Galaxy { Extra = NotStops };

        await AdventureGeneratorTests.Generator(null!, galaxy).ChapterPlacesAsync(PlannedChapterWorld.Ask(), CancellationToken.None);

        Assert.True(Assert.Single(galaxy.StationQueries, query => query.MaxDistance > 1 && query.Services.Count == 0).FixedOnly);

        var places = PlannerWorld.Places() with
        {
            Stations = [.. PlannerWorld.Places().Stations, .. NotStops.Select(station => station with { SystemName = "Fixture Alpha", SystemAddress = 2, Position = new StarPosition(110, 0, 0) })],
        };
        var inputs = PlannerWorld.Inputs(new DrawnActivity(TriggerKind.Sell, null, 20) { PlaceBefore = true });

        for (uint seed = 1; seed <= 40; seed++)
        {
            Assert.DoesNotContain(PlannerWorld.Steps(PlannerWorld.Planned(inputs, places, seed)), step => step.Kind == TriggerKind.Dock && NotStops.Any(station => station.Name == step.Station));
        }
    }

    private static StationSummary Station(string name, string? type) => new()
    {
        Name = name,
        SystemName = "Dyson's Hollow",
        SystemAddress = AdventureFixtures.Hollow,
        MarketId = 4000 + name.Length,
        Distance = 12,
        HasLargePad = true,
        Type = type,
    };
}
