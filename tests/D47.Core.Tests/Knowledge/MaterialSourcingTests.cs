using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The sourcing half of engineering: where a material is, and what the Commander could do about it
/// without going anywhere.
/// <para>
/// The galaxy service is a double, because what is being tested is the <em>question asked of it</em>
/// — which index, which filter, which key — rather than what the galaxy contains today. The shapes
/// those queries take were measured against the live service and are pinned in
/// <c>SpanshRequestTests</c>.
/// </para>
/// </summary>
public class MaterialSourcingTests
{
    private sealed class FakeGalaxy : IGalaxyService
    {
        public GalaxyQuery? LastQuery { get; private set; }

        public BodyQuery? LastBodyQuery { get; private set; }

        public StationQuery? LastStationQuery { get; private set; }

        public GalaxySearchResult Systems { get; set; } = new(
            "Sol",
            1,
            [new SystemSummary { Name = "Alpha Centauri", Distance = 4.38, Population = 12_000_000 }]);

        public BodySearchResult Bodies { get; set; } = new(
            "Sol",
            2,
            [
                new BodySummary
                {
                    Name = "Poor Rock",
                    SystemName = "Sol",
                    Distance = 1.0,
                    Materials = [("Yttrium", 0.4)],
                },
                new BodySummary
                {
                    Name = "Rich Rock",
                    SystemName = "Sol",
                    Distance = 9.0,
                    Materials = [("Yttrium", 1.9)],
                },
            ]);

        public StationSearchResult Stations { get; set; } = new(
            "Sol",
            2,
            [
                new StationSummary
                {
                    Name = "Broglie Terminal",
                    SystemName = "61 Cygni",
                    Distance = 11.1,
                    TraderType = "Raw",
                },
                new StationSummary { Name = "Nowhere Dock", SystemName = "Nanomam", Distance = 12.0 },
            ]);

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(Systems);
        }

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(0);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            LastStationQuery = query;
            return Task.FromResult(Stations);
        }

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            LastBodyQuery = query;
            return Task.FromResult(Bodies);
        }
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>A stock of grade 1 and grade 2 raw materials, so a trade has something to work from.</summary>
    private const string Materials =
        """
        {"timestamp":"3311-01-01T00:01:00Z","event":"Materials",
         "Raw":[{"Name":"iron","Count":300},{"Name":"vanadium","Count":60},{"Name":"carbon","Count":150}],
         "Manufactured":[],"Encoded":[]}
        """;

    private static (CapabilityRegistry Registry, FakeGalaxy Galaxy) Build(bool withMaterials = true)
    {
        var gameState = new GameStateStore();
        gameState.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));
        gameState.Apply(Event("""{"timestamp":"3311-01-01T00:00:30Z","event":"Location","StarSystem":"Sol","SystemAddress":10477373803}"""));

        if (withMaterials)
        {
            gameState.Apply(Event(Materials));
        }

        var galaxy = new FakeGalaxy();

        return (
            CapabilityRegistry.Build([EngineeringCapability.Create(() => gameState.Active, galaxy)]),
            galaxy);
    }

    private static async Task<string> Ask(
        CapabilityRegistry registry,
        string tool,
        params (string Name, string Value)[] values)
    {
        var result = await registry.InvokeAsync(
            tool,
            new ToolArguments(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result.Content;
    }

    // ---- find_material -----------------------------------------------------------------------

    [Fact]
    public async Task ARawMaterialIsRankedByShareLocallyAndSaysWhatItRankedOver()
    {
        var (registry, galaxy) = Build();

        var answer = await Ask(registry, "find_material", ("material", "Yttrium"));

        // Filtered remotely on presence, because the index will not filter or sort on share.
        Assert.Equal("Yttrium", galaxy.LastBodyQuery?.Material);
        Assert.True(galaxy.LastBodyQuery?.Landable);

        // Ranked locally, richest first — the opposite of the distance order it arrived in.
        var rich = answer.IndexOf("Rich Rock", StringComparison.Ordinal);
        var poor = answer.IndexOf("Poor Rock", StringComparison.Ordinal);

        Assert.True(rich > 0 && rich < poor, answer);
        Assert.Contains("1.9%", answer, StringComparison.Ordinal);

        // And it says what the ranking was over. "The best in the galaxy" would not be true.
        Assert.Contains("Richest of the 2 nearest landable bodies", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMaterialTheIndexDoesNotCarryIsDeclinedByNameRatherThanSearchedFor()
    {
        var (registry, galaxy) = Build();

        var answer = await Ask(registry, "find_material", ("material", "Rhenium"));

        // Three of the 28 raw materials are missing from the index. A search would come back
        // empty, and empty reads as "there is none near you" — a wrong answer wearing the shape of
        // a right one.
        Assert.Contains("does not carry Rhenium", answer, StringComparison.Ordinal);
        Assert.Contains("gap in the index", answer, StringComparison.Ordinal);
        Assert.Null(galaxy.LastBodyQuery);
    }

    [Fact]
    public async Task AStateGatedMaterialBecomesASystemSearchWordedAsAReport()
    {
        var (registry, galaxy) = Build();

        // Imperial Shielding's origin names both a superpower and nothing else; Proto Radiolic
        // Alloys names a state. Either way the origin decides the search.
        var answer = await Ask(registry, "find_material", ("material", "Imperial Shielding"));

        Assert.NotNull(galaxy.LastQuery);
        Assert.Contains(galaxy.LastQuery!.Criteria, criterion => criterion.Filter.Name == "allegiance");

        // Crowd-reported and it turns over on the background simulation's tick, so the wording is
        // "reported in", the same framing the station stock carries.
        Assert.Contains("reported", answer, StringComparison.Ordinal);
        Assert.Contains("Alpha Centauri", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheStateFilterIsTheOneTheSearchHonours()
    {
        var (registry, galaxy) = Build();

        await Ask(registry, "find_material", ("material", "Proto Radiolic Alloys"));

        var state = galaxy.LastQuery?.Criteria.FirstOrDefault(criterion => criterion.Filter.Name == "state");

        Assert.NotNull(state);

        // The word d47 uses is short; the key that matches anything is not.
        Assert.Equal("controlling_minor_faction_state", state!.Filter.Field);
    }

    [Fact]
    public async Task WhatTheCommanderHoldsComesBeforeWhereToGo()
    {
        var (registry, _) = Build();

        var answer = await Ask(registry, "find_material", ("material", "Vanadium"));

        Assert.Contains("You hold 60 of a possible", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATradeOutOfWhatIsAboardIsOfferedAndPricedInLowestTerms()
    {
        var (registry, _) = Build();

        var answer = await Ask(registry, "find_material", ("material", "Vanadium"));

        // Iron is grade 1 and Vanadium grade 2, and they are in different lines — so the exchange
        // is the published rate reduced, and 300 Iron is worth a real number of Vanadium.
        Assert.Contains("A trader could make it out of what you already hold:", answer, StringComparison.Ordinal);
        Assert.Contains("Iron", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoTradeIsOfferedAcrossTypes()
    {
        var (registry, _) = Build();

        // The Commander holds only Raw. Each trader deals in one type, so a Raw surplus cannot
        // become a Manufactured shortfall at any price — and no trade in 1,096 measured ever did.
        var answer = await Ask(registry, "find_material", ("material", "Phase Alloys"));

        Assert.DoesNotContain("could make it out of what you already hold", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACommodityIsNamedAsOneRatherThanSearchedForAsAMaterial()
    {
        var (registry, galaxy) = Build();

        // Gold ×200 is two hundred tonnes of cargo, not two hundred units against a materials cap.
        var answer = await Ask(registry, "find_material", ("material", "Gold"));

        Assert.Contains("not a ship material", answer, StringComparison.Ordinal);
        Assert.Null(galaxy.LastBodyQuery);
    }

    [Fact]
    public async Task AMaterialNobodyHasHeardOfIsRefusedWithNearMisses()
    {
        var (registry, _) = Build();

        var answer = await Ask(registry, "find_material", ("material", "Yttrum"));

        Assert.Contains("I don't know a material called", answer, StringComparison.Ordinal);
        Assert.Contains("Yttrium", answer, StringComparison.Ordinal);
    }

    // ---- find_material_trader ----------------------------------------------------------------

    [Fact]
    public async Task TheTraderSearchAsksTheIndexForTheTypeRatherThanGuessingFromEconomies()
    {
        var (registry, galaxy) = Build();

        var answer = await Ask(registry, "find_material_trader", ("type", "Raw"));

        // The index carries material_trader outright, which retires the economy heuristic that
        // classified 152 of 200 real traders as Raw.
        Assert.Equal("Raw", galaxy.LastStationQuery?.TraderType);
        Assert.True(galaxy.LastStationQuery?.IsAboutTraders);

        Assert.Contains("Broglie Terminal in 61 Cygni", answer, StringComparison.Ordinal);
        Assert.Contains("raw", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATraderTheIndexCannotClassifyIsSaidToBeUnrecordedRatherThanGuessedAt()
    {
        var (registry, _) = Build();

        // One station in fifty carries the service and no type. That is a third state, and it
        // needs a sentence rather than a guess.
        var answer = await Ask(registry, "find_material_trader");

        Assert.Contains("Nowhere Dock in Nanomam", answer, StringComparison.Ordinal);
        Assert.Contains("kind unrecorded", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoGalaxyServiceTheTraderSearchSaysSoRatherThanFailing()
    {
        var gameState = new GameStateStore();
        gameState.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        var registry = CapabilityRegistry.Build([EngineeringCapability.Create(() => gameState.Active)]);

        var answer = await Ask(registry, "find_material_trader");

        Assert.Contains("not switched on", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoGalaxyServiceAMaterialStillAnswersFromTheShippedTable()
    {
        var gameState = new GameStateStore();
        gameState.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        var registry = CapabilityRegistry.Build([EngineeringCapability.Create(() => gameState.Active)]);

        // Capabilities are state, not guards: where it is found is a shipped fact and does not
        // need the network.
        var answer = await Ask(registry, "find_material", ("material", "Yttrium"));

        Assert.Contains("Yttrium — a grade 4 raw material", answer, StringComparison.Ordinal);
        Assert.Contains("Found at:", answer, StringComparison.Ordinal);
    }
}
