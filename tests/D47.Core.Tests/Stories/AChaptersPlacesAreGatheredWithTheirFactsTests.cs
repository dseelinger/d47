using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>
/// The gather step reads what the drawn chapter needs and no more: Anarchy systems for a crime, war state for a bond, the supply
/// of the nearest systems for a map activity, stations only for an activity done at one, and the destination of a mission held,
/// so the planner needs no galaxy search of its own.
/// </summary>
public sealed class AChaptersPlacesAreGatheredWithTheirFactsTests
{
    private static readonly AdventureStory Story = new(
        "fixture-story", "Fixture Story", "Public", "Hidden", Chapter: 3, DaysRunning: 4, DaysSinceBeacon: 2,
        Slots: [new DrawnActivity(TriggerKind.Map, null, 3), new DrawnActivity(TriggerKind.Bond, null, 5), new DrawnActivity(TriggerKind.Mission, MissionFamilies.Illegal[0], 2) { PlaceBefore = true }]);

    private static readonly AdventureStory Exploring = Story with { Slots = [new DrawnActivity(TriggerKind.Map, null, 3), new DrawnActivity(TriggerKind.Codex, null, 4)] };

    [Fact]
    public async Task EachSystemCarriesItsGovernmentWarAndSupply()
    {
        var places = await Generator().ChapterPlacesAsync(new AdventureAsk(Story: Story), TestContext.Current.CancellationToken);

        Assert.True(places.System("Fixture Alpha")!.AtWar);
        Assert.Equal("Anarchy", places.System("Fixture Beta")!.Government);
        Assert.False(places.System("Fixture Beta")!.AtWar);
        Assert.Equal(4, places.System("Fixture Alpha")!.Mappable);
        Assert.Contains(places.Stations, station => station.Name == "Alpha Port");
        Assert.Contains(places.Bodies, body => body.Name == "Fixture Beta 2");
    }

    [Fact]
    public async Task AnExplorationChapterSearchesNoStationsAndReadsNoWar()
    {
        var galaxy = new Galaxy();
        var places = await Generator(galaxy).ChapterPlacesAsync(new AdventureAsk(Story: Exploring), TestContext.Current.CancellationToken);

        Assert.Equal(0, galaxy.StationSearches);
        Assert.Equal(0, galaxy.SystemSearches);
        Assert.DoesNotContain(places.Stations, station => station.Name == "Alpha Port");
        Assert.All(places.Systems, system => Assert.False(system.AtWar));
        Assert.Contains(places.Bodies, body => body.Name == "Fixture Beta 2");
        Assert.Equal(1, places.System("Fixture Beta")!.Mappable);
    }

    [Fact]
    public async Task AMissionHeldsDestinationIsAStop()
    {
        var places = await Generator().ChapterPlacesAsync(new AdventureAsk(Story: Story), TestContext.Current.CancellationToken);
        var stop = Assert.Single(places.Missions);

        Assert.Equal(4700001, stop.MissionId);
        Assert.Equal("Fixture Gate", stop.Station);
        Assert.Equal(4700002, stop.MarketId);
        Assert.Equal(new StarPosition(125, 0, 0), stop.Position);
        Assert.Contains(places.Stations, station => station.MarketId == 4700002);
    }

    [Fact]
    public async Task TheGatheredPlacesGiveAPlan()
    {
        var generator = Generator();
        var ask = new AdventureAsk(Story: Story);
        var places = await generator.ChapterPlacesAsync(ask, TestContext.Current.CancellationToken);
        var result = ChapterPlanner.Plan(ChapterInputs.Of(State(), ask), places, 5);

        Assert.True(result.Succeeded, result.Refusal?.Text);
        Assert.Equal("Fixture Alpha", result.Plan!.Objectives.SelectMany(objective => objective.Steps).Single(step => step.Kind == TriggerKind.Map).System);
    }

    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in EmbeddedFixture.Lines("planner.Journal.2026-11-01T100000.01.log"))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent));
            store.Apply(journalEvent!);
        }

        return store.Active!;
    }

    private static AdventureGenerator Generator(Galaxy? galaxy = null)
    {
        var state = State();
        galaxy ??= new Galaxy();

        return new AdventureGenerator(() => null, () => null, () => null, () => null, () => null, () => state, () => galaxy, () => null, null, null, NullLogger.Instance);
    }

    private sealed class Galaxy : StubGalaxyService, IGalaxyService
    {
        private static readonly StationSummary Alpha = new()
        {
            Name = "Alpha Port", SystemName = "Fixture Alpha", SystemAddress = 2, MarketId = 200, Position = new StarPosition(110, 0, 0), Type = "Coriolis Starport", LargePads = 2,
        };

        private static readonly StationSummary Gate = new()
        {
            Name = "Fixture Gate", SystemName = "Fixture Second", SystemAddress = 47002, MarketId = 4700002, Type = "Coriolis Starport", LargePads = 2,
        };

        private static readonly BodySummary Beta = new()
        {
            Name = "Fixture Beta 2", SystemName = "Fixture Beta", SystemAddress = 3, BodyId = 2, Position = new StarPosition(120, 0, 0), IsLandable = true,
        };

        private static readonly Dictionary<string, SystemSummary> Systems = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Fixture Alpha"] = new() { Name = "Fixture Alpha", SystemAddress = 2, Government = "Democracy", AtWar = true },
            ["Fixture Beta"] = new() { Name = "Fixture Beta", SystemAddress = 3, Government = "Anarchy" },
            ["Fixture Second"] = new() { Name = "Fixture Second", SystemAddress = 47002, Government = "Corporate" },
        };

        /// <summary>Station searches other than the mission destination's.</summary>
        public int StationSearches { get; private set; }

        /// <summary>Searches for one named system.</summary>
        public int SystemSearches { get; private set; }

        public Task<StarPosition?> PositionAsync(string system, CancellationToken cancellationToken) =>
            Task.FromResult<StarPosition?>(system == "Fixture Second" ? new StarPosition(125, 0, 0) : null);

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            IReadOnlyList<SystemSummary> found = query.ReferenceSystem switch
            {
                "Fixture Prime" when query.Criteria.Any(criterion => criterion.Choices.Contains("War")) => [Systems["Fixture Alpha"] with { Position = new StarPosition(110, 0, 0) }],
                "Fixture Prime" => [Systems["Fixture Beta"] with { Position = new StarPosition(120, 0, 0) }],
                { } name when Systems.TryGetValue(name, out var system) => [system],
                _ => [],
            };

            if (query.ReferenceSystem is not "Fixture Prime" and not "Fixture Second")
            {
                SystemSearches++;
            }


            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, found.Count, found));
        }

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            if (query.StationName is not "Fixture Gate")
            {
                StationSearches++;
            }

            IReadOnlyList<StationSummary> found = query switch
            {
                { StationName: "Fixture Gate" } => [Gate],
                { StationName: null, Services.Count: 0 } => [Alpha],
                _ => [],
            };

            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, found.Count, found));
        }

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(query.Signal is null ? new BodySearchResult(query.ReferenceSystem, 1, [Beta]) : new BodySearchResult(query.ReferenceSystem, 0, []));

        public override Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BodySummary>>(
            [
                .. Enumerable.Range(1, systemAddress == 2 ? 4 : 1).Select(id => new BodySummary { Name = $"Body {id}", SystemName = "Any", BodyId = id, Subtype = "Rocky body" }),
                new BodySummary { Name = "Star", SystemName = "Any", BodyId = 0, Subtype = "G (White-Yellow) Star" },
            ]);

        public override Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult(new SystemBiology(systemAddress, []));
    }
}
