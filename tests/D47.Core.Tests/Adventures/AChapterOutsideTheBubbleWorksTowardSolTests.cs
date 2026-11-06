using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Knowledge;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

public sealed class AChapterOutsideTheBubbleWorksTowardSolTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly AdventureStory Chapter = new("s", "S", "public", "hidden", 1, 10, 3, Slots: [PlannedChapterWorld.Bounty]);

    private static RoundScriptedLlmProvider Provider() =>
        new(Saying(PlannedChapterWorld.Lines(ChapterPlanner.MostObjectives)), Saying(PlannedChapterWorld.NoObjections));

    private static void Succeeded(AdventureOutcome outcome) => Assert.True(outcome.Succeeded, outcome.Refusal);

    private static string Prompt(RoundScriptedLlmProvider provider) =>
        provider.Requests.First(request => request.ResponseSchema?.Name == "story_lines").Prompt.History[0].Text;

    [Fact]
    public void AChapter588LightYearsOutHasTheBandFrom288To488() =>
        Assert.Equal(new ChapterBand.Range(288, 488), ChapterBand.Of(588));

    [Theory]
    [InlineData(450)]
    [InlineData(500)]
    public void AChapterInsideTheBubbleHasNoBand(double lightYears) => Assert.Null(ChapterBand.Of(lightYears));

    [Fact]
    public void AChapter1200LightYearsOutHasTheBandFrom900To1100() =>
        Assert.Equal(new ChapterBand.Range(900, 1100), ChapterBand.Of(1200));

    [Theory]
    [InlineData("beacon")]
    [InlineData("finale")]
    [InlineData("rewrite")]
    [InlineData("short")]
    public void ABeaconFinaleRewriteOrShortStoryHasNoBand(string kind)
    {
        var ask = kind switch
        {
            "beacon" => new AdventureAsk(Story: Chapter with { Beacon = new AdventureBeacon(1, "B") }),
            "finale" => new AdventureAsk(Story: Chapter with { FinaleChapter = 2 }),
            "rewrite" => new AdventureAsk(Story: Chapter, Rewrite: new AdventureRewrite(null!, 0)),
            _ => new AdventureAsk(Length: AdventureLength.Short),
        };

        Assert.Null(ChapterBand.Of(new StarPosition(588, 0, 0), ask));
        Assert.NotNull(ChapterBand.Of(new StarPosition(588, 0, 0), new AdventureAsk(Story: Chapter)));
    }

    [Fact]
    public async Task TheChapterPromptSaysHowFarFromSolTheCommanderIs()
    {
        var provider = Provider();

        Succeeded(await Generator(provider, new Galaxy(), 588).GenerateAsync(new AdventureAsk(Story: Chapter), Now, CancellationToken.None));

        Assert.Contains("588 light years from Sol", Prompt(provider));
    }
    [Fact]
    public async Task AChapter300LightYearsOutHasNoBandInItsPrompt()
    {
        var provider = Provider();

        Succeeded(await Generator(provider, new Galaxy(), 300).GenerateAsync(new AdventureAsk(Story: Chapter), Now, CancellationToken.None));

        Assert.DoesNotContain("light years from Sol", Prompt(provider));
    }

    [Fact]
    public async Task TheInBandSearchIsCentred150LightYearsTowardSolWithin50()
    {
        var galaxy = new Galaxy();

        await Places(galaxy, 588);

        Assert.Equal([(new StarPosition(438, 0, 0), 50.0), (new StarPosition(438, 0, 0), 50.0)], galaxy.Around);
    }
    [Fact]
    public async Task APlaceOutsideTheBandOrNotFixedIsNotListedAsInsideIt()
    {
        var galaxy = new Galaxy
        {
            InBandStations =
            [
                Galaxy.HarbourPoint with { Name = "Far Point", SystemName = "Far Reach", Position = new StarPosition(500, 0, 0) },
                Galaxy.HarbourPoint with { Name = "Fleet Carrier K2X", Type = "Drake-Class Carrier" },
            ],
            InBandBodies = [],
        };

        var places = await Places(galaxy, 588);

        Assert.Equal(ChapterAreaState.NoneFound, places.InBand.State);
        Assert.Empty(places.InBand.Stations);
    }
    [Fact]
    public async Task WhenTheFirstSearchFindsNothingTheSecondIsCentred200TowardSolWithin100()
    {
        var galaxy = new Galaxy { OnlyRadius = 100 };

        var places = await Places(galaxy, 588);

        Assert.Equal(
            [(new StarPosition(438, 0, 0), 50.0), (new StarPosition(438, 0, 0), 50.0), (new StarPosition(388, 0, 0), 100.0), (new StarPosition(388, 0, 0), 100.0)],
            galaxy.Around);
        Assert.Equal("Harbour Point", Assert.Single(places.InBand.Stations).Name);
    }

    [Fact]
    public void WithNoPlaceInsideTheBandTheChapterIsNotHeldToIt()
    {
        var start = new StarPosition(1000, 0, 0);
        var inputs = PlannerWorld.Inputs(PlannerWorld.Bounty) with { Position = start, Band = ChapterBand.Of(start)!.Value, Reach = 300 };
        var places = OutsideTheBubble(ChapterAreaState.NoneFound);

        foreach (var seed in new uint[] { 1, 2, 3 })
        {
            Assert.NotNull(PlannerWorld.Planned(inputs, places, seed));
        }
    }

    [Fact]
    public void WhenTheInBandSearchIsDownTheBandStillApplies()
    {
        var start = new StarPosition(1000, 0, 0);
        var inputs = PlannerWorld.Inputs(PlannerWorld.Bounty) with { Position = start, Band = ChapterBand.Of(start)!.Value, Reach = 300 };
        var result = ChapterPlanner.Plan(inputs, OutsideTheBubble(ChapterAreaState.Unavailable), 3);

        Assert.Null(result.Plan);
        Assert.Equal(ChapterRules.Band, result.Refusal?.Rule);
    }
    [Theory]
    [InlineData("bubble")]
    [InlineData("black")]
    public async Task AChapterInTheBubbleOrInTheBlackMakesNoInBandSearch(string where)
    {
        var galaxy = new Galaxy();

        await Places(galaxy, where == "black" ? 588 : 300, where == "black");

        Assert.Empty(galaxy.Around);
    }

    private static async Task<ChapterPlaces> Places(Galaxy galaxy, double lightYears, bool black = false) =>
        await Generator(null!, galaxy, lightYears).ChapterPlacesAsync(new AdventureAsk(Story: black ? Chapter with { InTheBlack = true } : Chapter), CancellationToken.None);

    private static ChapterPlaces OutsideTheBubble(ChapterAreaState state) => new()
    {
        Systems = [PlannerWorld.Place(PlannerWorld.Start, 1, 1000), PlannerWorld.Place("Fixture Out", 2, 980), PlannerWorld.Place("Fixture Mid", 3, 1060)],
        Stations = [PlannerWorld.Station("Out Port", "Fixture Out", 2, 200, 980)],
        InBand = new ChapterArea(state, [], []),
    };

    private static AdventureGenerator Generator(ILlmProvider provider, Galaxy galaxy, double lightYears, TimeProvider? time = null)
    {
        galaxy.At = lightYears;

        return AdventureGeneratorTests.Generator(provider, galaxy, Now, $"{lightYears},0,0", time);
    }

    internal sealed class Galaxy : StubGalaxyService
    {
        public static readonly StationSummary HarbourPoint = new()
        {
            Name = "Harbour Point",
            SystemName = "Velan's Rest",
            MarketId = 9001,
            SystemAddress = 9002,
            Type = "Coriolis Starport",
            HasLargePad = true,
            Distance = 3,
            Position = new StarPosition(438, 0, 0),
        };

        private static readonly StationSummary LanternHub = new()
        {
            Name = "Lantern Hub",
            SystemName = "Ossen's Lantern",
            MarketId = 9003,
            SystemAddress = AdventureFixtures.Lantern,
            Type = "Coriolis Starport",
            HasLargePad = true,
        };

        public double FromSol { get; init; } = 400;

        /// <summary>Where the Commander is along the x axis; stations to stop at lie toward Sol from it.</summary>
        public double At { get; set; }

        public bool Down { get; init; }

        public IReadOnlyList<StationSummary> InBandStations { get; init; } = [HarbourPoint];

        public IReadOnlyList<BodySummary> InBandBodies { get; init; } =
        [
            new() { Name = "Velan's Rest 2 a", SystemName = "Velan's Rest", BodyId = 7, SystemAddress = 9002, IsLandable = true, Distance = 3, Position = new StarPosition(438, 0, 0) },
        ];

        /// <summary>The only radius a position-centred search finds anything at; null for every radius.</summary>
        public double? OnlyRadius { get; init; }

        public bool InBandDown { get; init; }

        /// <summary>The centre, rounded, and radius of each position-centred search, in order.</summary>
        public List<(StarPosition At, double Radius)> Around { get; } = [];

        private bool Answers(StarPosition at, double radius)
        {
            Around.Add((new StarPosition(Math.Round(at.X, 6), Math.Round(at.Y, 6), Math.Round(at.Z, 6)), radius));

            return InBandDown
                ? throw new GalaxyUnavailableException("the search is down")
                : OnlyRadius is null || OnlyRadius == radius;
        }

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            var named = string.Equals(query.ReferenceSystem, "Ossen's Lantern", StringComparison.OrdinalIgnoreCase)
                ? new List<SystemSummary> { new() { Name = "Ossen's Lantern", SystemAddress = AdventureFixtures.Lantern, Government = "Democracy", Distance = 5 } }
                : [];

            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, named.Count, named));
        }

        public override Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Down && to == "Sol"
                ? throw new GalaxyUnavailableException("the service is down")
                : Task.FromResult<double?>(to == "Sol" ? FromSol : 12);

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            IReadOnlyList<StationSummary> found = query.ReferencePosition is { } at
                ? Answers(at, query.MaxDistance) ? InBandStations : []
                : query is { StationName: "Lantern Hub" } ? [LanternHub]
                : [.. Enumerable.Range(1, 6).Select(index => new StationSummary
                {
                    Name = $"Stop Port {index}", SystemName = $"Stop {index}", SystemAddress = 6000 + index, MarketId = 6100 + index,
                    Position = new StarPosition(At - (index * 30), 0, 0), Type = "Coriolis Starport", LargePads = 2,
                })];

            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, found.Count, found));
        }

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            IReadOnlyList<BodySummary> found = query.ReferencePosition is { } at && Answers(at, query.MaxDistance) ? InBandBodies : [];

            return Task.FromResult(new BodySearchResult(query.ReferenceSystem, found.Count, found));
        }
    }
}
