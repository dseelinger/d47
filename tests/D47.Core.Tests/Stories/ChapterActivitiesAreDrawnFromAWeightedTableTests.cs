using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A chapter's counted activities are drawn by code from a table, weighted by careers and limited by gates.</summary>
public sealed class ChapterActivitiesAreDrawnFromAWeightedTableTests
{
    private const int Seeds = 1000;

    private static readonly string[] Banned = ["Mission_Altruism", "Mission_AltruismCredits", "Mission_MassacreThargoid"];

    private static ShipModule Module(string item, string? blueprint = null) => new("Slot", item, true, 100, 1) { Blueprint = blueprint };

    private static ShipLoadout Ship(int? cargo = 64, params ShipModule[] modules) =>
        new() { Type = "cobramkiii", CargoCapacity = cargo, Modules = modules };

    private static RankState Ranks(int combat = 0, int soldier = 0) => new()
    {
        TakenAt = DateTimeOffset.UnixEpoch,
        Standings = [new RankStanding("Combat", combat), new RankStanding("Soldier", soldier)],
    };

    private static CareerStatistics Statistics(string figures) =>
        CareerStatistics.Empty.Apply(AdventureFixtures.Event($$"""{ "timestamp":"2026-10-19T10:00:00Z", "event":"Statistics", {{figures}} }"""));

    private static IReadOnlyList<DrawnActivity> Draw(
        uint seed,
        ShipLoadout? ship = null,
        RankState? ranks = null,
        bool odyssey = false,
        IReadOnlyList<string>? recent = null,
        IReadOnlyList<string>? refused = null,
        bool comfort = false,
        CareerStatistics? statistics = null,
        int fixedPlaces = 0,
        CareerWeights? weights = null) =>
        ChapterDraw.Draw(weights ?? CareerWeights.Even, ranks ?? Ranks(), ship ?? Ship(), odyssey, recent ?? [], refused, comfort, statistics ?? CareerStatistics.Empty, fixedPlaces, seed);

    private static IEnumerable<IReadOnlyList<DrawnActivity>> Many(Func<uint, IReadOnlyList<DrawnActivity>> draw) =>
        Enumerable.Range(0, Seeds).Select(seed => draw((uint)seed));

    private static bool Is(DrawnActivity slot, TriggerKind kind, string? family = null) =>
        slot.Kind == kind && (family is null || slot.Family?.StartsWith(family, StringComparison.Ordinal) == true);

    [Fact]
    public void TheSameInputsAndSeedGiveTheSameThreeSlots()
    {
        var first = Draw(7, odyssey: true);
        var second = Draw(7, odyssey: true);

        Assert.Equal(ChapterDraw.Slots, first.Count);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ADifferentChapterNumberGivesADifferentDraw()
    {
        var draws = Enumerable.Range(1, 12)
            .Select(chapter => string.Join("|", Draw(ChapterDraw.Seed("story", chapter), odyssey: true).Select(slot => $"{slot.Kind}{slot.Family}{slot.Count}")))
            .Distinct()
            .Count();

        Assert.True(draws > 1);
        Assert.NotEqual(ChapterDraw.Seed("story", 2), ChapterDraw.Seed("story", 3));
        Assert.NotEqual(ChapterDraw.Seed("story", 2), ChapterDraw.Seed("story", 2, replaced: 1));
    }

    [Fact]
    public void AnUnengineeredShipAtCombatRankZeroNeverDrawsADangerousActivity()
    {
        foreach (var slots in Many(seed => Draw(seed, odyssey: true)))
        {
            Assert.DoesNotContain(slots, slot => Is(slot, TriggerKind.Bond) || Is(slot, TriggerKind.Conflict) || Is(slot, TriggerKind.Mission, "Mission_Massacre") || Is(slot, TriggerKind.Mission, "Mission_Assassinate"));
        }
    }

    [Fact]
    public void ARankedAndEngineeredShipCanDrawThem()
    {
        var ship = Ship(64, Module("a", "x"), Module("b", "x"), Module("c", "x"));
        var kinds = Many(seed => Draw(seed, ship, Ranks(combat: 3))).SelectMany(slots => slots).Select(slot => slot.Kind).ToHashSet();

        Assert.Contains(TriggerKind.Bond, kinds);
        Assert.Contains(TriggerKind.Conflict, kinds);
    }

    [Fact]
    public void AMissingModuleOrGameRemovesWhatNeedsIt()
    {
        foreach (var slots in Many(seed => Draw(seed, Ship(null))))
        {
            Assert.DoesNotContain(slots, slot => slot.Kind is TriggerKind.Mine or TriggerKind.Map or TriggerKind.Sell or TriggerKind.Salvage or TriggerKind.Organic or TriggerKind.Srv);
            Assert.DoesNotContain(slots, slot => slot.Family is { } family && (family.Contains("OnFoot", StringComparison.Ordinal) || family.Contains("Passenger", StringComparison.Ordinal) || family == "Mission_Sightseeing"));
            Assert.DoesNotContain(slots, slot => slot.Family is "Mission_Delivery" or "Mission_Collect" or "Mission_Mining" or "Mission_Smuggle" or "Mission_Piracy");
        }
    }

    [Fact]
    public void FittedModulesAndOdysseyAddThemBack()
    {
        var ship = Ship(64, Module("hpt_mininglaser_fixed_small"), Module("int_refinery_size2_class1"), Module("int_detailedsurfacescanner_tiny"), Module("int_passengercabin_size3_class1"), Module("int_buggybay_size2_class1"));
        var drawn = Many(seed => Draw(seed, ship, odyssey: true)).SelectMany(slots => slots).ToList();

        Assert.Contains(drawn, slot => slot.Kind == TriggerKind.Mine);
        Assert.Contains(drawn, slot => slot.Kind == TriggerKind.Map);
        Assert.Contains(drawn, slot => slot.Kind == TriggerKind.Srv);
        Assert.Contains(drawn, slot => slot.Kind == TriggerKind.Organic);
        Assert.Contains(drawn, slot => slot.Family == "Mission_PassengerVIP");
    }

    [Fact]
    public void ARefusedActivityIsNeverDrawn()
    {
        var refused = new[] { "bounty", "mission:Mission_OnFoot", "signal" };

        foreach (var slots in Many(seed => Draw(seed, odyssey: true, refused: refused)))
        {
            Assert.DoesNotContain(slots, slot => slot.Kind is TriggerKind.Bounty or TriggerKind.Signal);
            Assert.DoesNotContain(slots, slot => slot.Family?.StartsWith("Mission_OnFoot", StringComparison.Ordinal) == true);
        }
    }

    [Fact]
    public void AnActivityUsedInTheLastThreeChaptersIsDrawnLessOften()
    {
        var recent = new[] { "rescue" };

        int Rescues(IReadOnlyList<string> keys) => Many(seed => Draw(seed, recent: keys)).Count(slots => slots.Any(slot => slot.Kind == TriggerKind.Rescue));

        Assert.True(Rescues(recent) < Rescues([]));
    }

    [Fact]
    public void AComfortChapterStartsWithTheLeastDoneActivityThatPassesTheGates()
    {
        var statistics = Statistics("""
            "Combat":{ "Bounties_Claimed":500, "Combat_Bonds":0 }, "Mining":{ "Quantity_Mined":0 },
            "Exobiology":{ "Organic_Data":0 }, "Search_And_Rescue":{ "SearchRescue_Count":5 },
            "Passengers":{ "Passengers_Missions_Delivered":20 }
            """);

        foreach (var slots in Many(seed => Draw(seed, comfort: true, statistics: statistics)))
        {
            Assert.Equal(TriggerKind.Rescue, slots[0].Kind);
        }
    }

    [Fact]
    public void AnEngineeredShipsComfortChapterCanStartWithABond()
    {
        var ship = Ship(64, Module("a", "x"), Module("b", "x"), Module("c", "x"));
        var statistics = Statistics("\"Combat\":{ \"Bounties_Claimed\":500, \"Combat_Bonds\":0 }, \"Mining\":{ \"Quantity_Mined\":9 }, \"Exobiology\":{ \"Organic_Data\":9 }, \"Search_And_Rescue\":{ \"SearchRescue_Count\":9 }, \"Passengers\":{ \"Passengers_Missions_Delivered\":9 }");

        Assert.Equal(TriggerKind.Bond, Draw(1, ship, Ranks(combat: 3), comfort: true, statistics: statistics)[0].Kind);
    }

    [Fact]
    public void PlaceObjectivesFillTheRoomTheChapterHasLeft()
    {
        var ship = Ship(64, Module("a", "x"), Module("b", "x"), Module("c", "x"));

        static bool Flagged(DrawnActivity slot) => slot.PlaceBefore;

        foreach (var slots in Many(seed => Draw(seed, ship, Ranks(combat: 3), odyssey: true, fixedPlaces: 2)))
        {
            Assert.DoesNotContain(slots, Flagged);
        }

        foreach (var slots in Many(seed => Draw(seed, ship, Ranks(combat: 3), odyssey: true, fixedPlaces: 1)))
        {
            Assert.True(slots.Count(Flagged) <= 1);
        }

        foreach (var slots in Many(seed => Draw(seed, ship, Ranks(combat: 3), odyssey: true)))
        {
            Assert.True(slots.Count(Flagged) <= 2);
        }
    }

    [Fact]
    public void EveryCrimeAndConflictNeedsAPlaceBefore()
    {
        Assert.All(ChapterActivities.All.Where(row => row.Career == Career.Crime || row.Kind == TriggerKind.Conflict), row => Assert.True(row.PlaceBefore));
        Assert.All(ChapterActivities.All.Where(row => row.Family is { } family && MissionFamilies.IsIllegal(family)), row => Assert.Equal(Career.Crime, row.Career));
    }

    [Fact]
    public void OnFootKillBondsNeedOnlyOdysseyWhileShipKillBondsStillNeedDanger()
    {
        Assert.Equal(new DrawableActivity(TriggerKind.OnFootBond, null, Career.Combat, ActivityGate.Odyssey, 5, 15), ChapterActivities.Find(TriggerKind.OnFootBond, null, null));
        Assert.Equal(new DrawableActivity(TriggerKind.Bond, null, Career.Combat, ActivityGate.Danger, 5, 15), ChapterActivities.Find(TriggerKind.Bond, null, null));
    }

    [Fact]
    public void NothingThatIsNeverDrawnIsInTheTable()
    {
        Assert.DoesNotContain(ChapterActivities.All, row => Banned.Contains(row.Family) || MissionFamilies.IsSetAside(row.Family));
        Assert.DoesNotContain(ChapterActivities.All, row => row.Kind is TriggerKind.OnFoot or TriggerKind.Collect or TriggerKind.SuitMod or TriggerKind.Faction);

        var ship = Ship(64, Module("a", "x"), Module("b", "x"), Module("c", "x"));

        foreach (var slots in Many(seed => Draw(seed, ship, Ranks(3, 3), odyssey: true)))
        {
            Assert.DoesNotContain(slots, slot => Banned.Contains(slot.Family) || MissionFamilies.IsSetAside(slot.Family));
        }
    }

    [Fact]
    public void CountsStayInTheirRangesAndTonsInTheHold()
    {
        foreach (var slots in Many(seed => Draw(seed, Ship(12, Module("hpt_mininglaser_fixed_small"), Module("int_refinery_size2_class1")), odyssey: true)))
        {
            foreach (var slot in slots)
            {
                var row = ChapterActivities.All.First(row => row.Kind == slot.Kind && row.Family == slot.Family && row.Organic == slot.Organic);

                Assert.InRange(slot.Count, Math.Min(row.Min, 12), row.IsTons ? 12 : row.Max);
            }
        }
    }

    [Fact]
    public void RatingsFollowTheStatisticsOnlyWhileEveryRatingIsTheDefault()
    {
        var statistics = Statistics("\"Combat\":{ \"Bounties_Claimed\":3 }, \"Mining\":{ \"Quantity_Mined\":0 }");
        var even = CareerWeights.From(null, statistics);
        var rated = CareerWeights.From(new Dictionary<Career, int> { [Career.Mining] = 80, [Career.Crime] = 1 }, statistics);

        Assert.Equal(75, even.Weight(Career.Combat));
        Assert.Equal(50, even.Weight(Career.Mining));
        Assert.Equal(50, even.Weight(Career.Crime));
        Assert.Equal(50, rated.Weight(Career.Combat));
        Assert.Equal(80, rated.Weight(Career.Mining));
        Assert.Equal(50, rated.Weight(Career.Crime));
        Assert.Equal(75, CareerWeights.From(new Dictionary<Career, int> { [Career.Crime] = 90 }, statistics).Weight(Career.Combat));
    }
}
