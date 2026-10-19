using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>With no station within a chapter's reach, the draw never picks an activity that needs one.</summary>
public sealed class ADrawnChapterNeedsAStationInReachTests
{
    private const int Seeds = 1000;

    private static readonly TriggerKind[] NeedStation =
        [TriggerKind.Bounty, TriggerKind.Bond, TriggerKind.Conflict, TriggerKind.Sell, TriggerKind.DataSale, TriggerKind.Rescue, TriggerKind.Mission];

    private static readonly ShipLoadout Ship = new() { Type = "cobramkiii", CargoCapacity = 64 };

    private static readonly RankState Ranks = new()
    {
        TakenAt = DateTimeOffset.UnixEpoch,
        Standings = [new RankStanding("Combat", 0), new RankStanding("Soldier", 0)],
    };

    private static IReadOnlyList<DrawnActivity> Draw(
        uint seed, bool stationsInReach, bool comfort = false, CareerStatistics? statistics = null, ShipLoadout? ship = null) =>
        ChapterDraw.Draw(
            CareerWeights.Even, Ranks, ship ?? Ship, odyssey: true, [], null, comfort, statistics ?? CareerStatistics.Empty, 0, seed, stationsInReach: stationsInReach);

    private static CareerStatistics Statistics(string figures) =>
        CareerStatistics.Empty.Apply(AdventureFixtures.Event($$"""{ "timestamp":"2026-10-19T10:00:00Z", "event":"Statistics", {{figures}} }"""));

    [Fact]
    public void WithNoStationInReachNoSeedDrawsARowThatNeedsOne()
    {
        for (uint seed = 0; seed < Seeds; seed++)
        {
            Assert.DoesNotContain(Draw(seed, stationsInReach: false), slot => NeedStation.Contains(slot.Kind));
        }
    }

    [Fact]
    public void WithNoStationInReachTheUngatedRowsAreStillDrawn()
    {
        var kinds = Enumerable.Range(0, Seeds).SelectMany(seed => Draw((uint)seed, stationsInReach: false)).Select(slot => slot.Kind).ToHashSet();

        Assert.Contains(TriggerKind.Organic, kinds);
        Assert.Contains(TriggerKind.Signal, kinds);
    }

    [Fact]
    public void WithAStationInReachTheDrawIsUnchanged()
    {
        for (uint seed = 0; seed < Seeds; seed++)
        {
            var asked = Draw(seed, stationsInReach: true);
            var plain = ChapterDraw.Draw(CareerWeights.Even, Ranks, Ship, odyssey: true, [], null, false, CareerStatistics.Empty, 0, seed);

            Assert.Equal(plain, asked);
        }

        Assert.Contains(Enumerable.Range(0, Seeds).SelectMany(seed => Draw((uint)seed, stationsInReach: true)), slot => NeedStation.Contains(slot.Kind));
    }

    [Fact]
    public void ABountyComfortChapterWithNoStationInReachTakesMineOrOrganic()
    {
        var statistics = Statistics("\"Combat\":{ \"Bounties_Claimed\":0 }, \"Mining\":{ \"Quantity_Mined\":9 }, \"Exobiology\":{ \"Organic_Data\":9 }");
        var mining = new ShipLoadout
        {
            Type = "cobramkiii",
            CargoCapacity = 64,
            Modules =
            [
                new ShipModule("Slot", "hpt_mininglaser_fixed_small", true, 100, 1),
                new ShipModule("Slot", "int_refinery_size1_class1", true, 100, 1),
            ],
        };

        Assert.Equal(TriggerKind.Bounty, Draw(1, stationsInReach: true, comfort: true, statistics: statistics, ship: mining)[0].Kind);

        for (uint seed = 0; seed < Seeds; seed++)
        {
            Assert.Contains(Draw(seed, stationsInReach: false, comfort: true, statistics: statistics, ship: mining)[0].Kind, new[] { TriggerKind.Mine, TriggerKind.Organic });
        }
    }

    [Fact]
    public void AComfortChapterWithNeitherMineNorOrganicHasNoComfortSlot()
    {
        var statistics = Statistics("\"Combat\":{ \"Bounties_Claimed\":0 }");
        var ungated = new[] { TriggerKind.Signal, TriggerKind.Codex, TriggerKind.Wreck, TriggerKind.Uss, TriggerKind.Salvage, TriggerKind.Organic };

        foreach (var seed in Enumerable.Range(0, Seeds))
        {
            var slots = ChapterDraw.Draw(CareerWeights.Even, Ranks, Ship, odyssey: false, [], null, true, statistics, 0, (uint)seed, stationsInReach: false);

            Assert.All(slots, slot => Assert.Contains(slot.Kind, ungated));
            Assert.DoesNotContain(slots, slot => slot.Kind is TriggerKind.Mine or TriggerKind.Organic);
        }
    }

    private sealed class Stations(IReadOnlyList<StationSummary> found, Exception? fails = null) : IGalaxyService
    {
        public List<StationQuery> Queries { get; } = [];

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            Queries.Add(query);

            return fails is null ? Task.FromResult(new StationSearchResult(null, found.Count, found)) : throw fails;
        }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static readonly string LockedSystem = PermitSystemTable.Names.First();

    private static CommanderGameState Flying(string ship = "cobramkiii")
    {
        var game = Loaded(1_000_000, ship, cargo: 64);

        game.Apply(AdventureFixtures.Event(
            $$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"Location", "StarSystem":"Far Out", "StarPos":[500,500,500], "SystemAddress":1, "Docked":false }"""));

        return game;
    }

    private static StoryFixtures Scanned()
    {
        var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(NextSpine), RoundScriptedLlmProvider.Saying(NextBeats)));

        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now.AddDays(-30),
            BeaconScanAt = Now.AddDays(-20),
        });

        return fixtures;
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AChapterWithNoStationInReachAsksOnceAndIsDrawnNothingThatNeedsOne()
    {
        var galaxy = new Stations([]);

        using var fixtures = Scanned();
        fixtures.Director.Game = () => Flying();
        fixtures.Director.Galaxy = () => galaxy;

        await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None);

        var query = Assert.Single(galaxy.Queries);

        Assert.Equal("Far Out", query.ReferenceSystem);
        Assert.Equal(AdventureGenerator.Radius(AdventureReach.Session, 30.5, false), query.MaxDistance);
        Assert.DoesNotContain(Assert.Single(fixtures.Asks).Story!.Slots!, slot => NeedStation.Contains(slot.Kind));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AStationWithNoPadForTheShipOrInAPermitSystemDoesNotCount()
    {
        var galaxy = new Stations(
        [
            new StationSummary { Name = "Tiny", SystemName = "Near", Type = "Coriolis Starport", SmallPads = 2, MediumPads = 0, LargePads = 0 },
            new StationSummary { Name = "Locked", SystemName = LockedSystem, Type = "Coriolis Starport", LargePads = 1 },
        ]);

        using var fixtures = Scanned();
        fixtures.Director.Game = () => Flying("anaconda");
        fixtures.Director.Galaxy = () => galaxy;

        await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None);

        Assert.DoesNotContain(Assert.Single(fixtures.Asks).Story!.Slots!, slot => NeedStation.Contains(slot.Kind));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task WithGalaxySearchOffNoQueryIsMadeAndTheDrawIsNotGated()
    {
        var draws = new List<IReadOnlyList<DrawnActivity>>();

        for (var attempt = 0; attempt < 12; attempt++)
        {
            using var fixtures = Scanned();
            fixtures.Director.Game = () => Flying();
            fixtures.Director.Galaxy = () => null;
            fixtures.Stories.Save("F1", fixtures.Stories.Current("F1")! with { Id = Id, PickedAt = Now.AddDays(-30 - attempt) });

            await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None);
            draws.Add(Assert.Single(fixtures.Asks).Story!.Slots!);
        }

        Assert.Contains(draws.SelectMany(slots => slots), slot => NeedStation.Contains(slot.Kind));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AFailedQueryLeavesTheDrawNotGated()
    {
        var galaxy = new Stations([], new GalaxyUnavailableException("down"));

        using var fixtures = Scanned();
        fixtures.Director.Game = () => Flying();
        fixtures.Director.Galaxy = () => galaxy;

        await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None);

        Assert.Single(galaxy.Queries);
        Assert.Equal(ChapterDraw.Slots, Assert.Single(fixtures.Asks).Story!.Slots!.Count);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ARedrawnSlotAfterNeverAskThisFollowsTheSameGate()
    {
        const string Key = "the-hard-way";

        var galaxy = new Stations([]);

        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(NextBeats)));
        fixtures.WritesDrawnChapters = true;
        fixtures.Director.Game = () => Flying();
        fixtures.Director.Galaxy = () => galaxy;
        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now.AddDays(-30),
            BeaconScanAt = Now.AddDays(-20),
            Chapters = ["chapter-one", Key],
            CluesGiven = 2,
        });

        Assert.Null(fixtures.Book.Write("F1", new Adventure
        {
            Key = Key,
            Name = "The Hard Way",
            Source = AdventureSource.Generated,
            Written = Now,
            Spine = new AdventureSpine { Premise = "A debt.", Want = "To pay it.", Stake = "Whether it can be.", Turn = "It is owed.", Ending = "Forgiven." },
            Opening = "Go.",
            StoryId = Id,
            Beats =
            [
                AdventureFixtures.Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = AdventureFixtures.Lantern, System = "Ossen's Lantern" }, "Scoop here."),
                AdventureFixtures.Beat("First Blood", "catalyst", new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 1 }, "One."),
                AdventureFixtures.Beat("Home", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = AdventureFixtures.Hollow, System = "Dyson's Hollow" }, "End."),
            ],
        }));
        Assert.Null(fixtures.Book.Begin("F1", Key, Now));
        fixtures.Book.Observe(AdventureFixtures.Jump(AdventureFixtures.Lantern, Now.AddMinutes(1)), "F1");
        fixtures.Director.Clock = () => Now.AddHours(1);

        await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken);

        Assert.Single(galaxy.Queries);
        Assert.DoesNotContain(Assert.Single(fixtures.Asks).Story!.Slots!, slot => NeedStation.Contains(slot.Kind));
    }
}
