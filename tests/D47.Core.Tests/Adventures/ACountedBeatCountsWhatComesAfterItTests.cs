using D47.Core.Storage;
using D47.Core.Adventures;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

[Trait("Category", "Integration")]
public class ACountedBeatCountsWhatComesAfterItTests : IDisposable
{
    private const string Labour = "LTT 7786 Labour";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-counted-beats", Guid.NewGuid().ToString("N"));

    public ACountedBeatCountsWhatComesAfterItTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static readonly AdventureTrigger ToLantern = new() { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" };

    private static Adventure Story(AdventureTrigger counted, DateTimeOffset? accepted = null) => LanternRoute(accepted ?? Accepted) with
    {
        Beats =
        [
            Beat("The Lantern", "setup", ToLantern, "Scoop here."),
            Beat("The Count", "catalyst", counted, "Done."),
        ],
    };

    private static AdventureStanding Fold(Adventure adventure, IEnumerable<JournalEvent> events) =>
        events.Aggregate(AdventureFold.Start(adventure), AdventureFold.Apply);

    private static JournalEvent Bond(DateTimeOffset at, string faction = Labour) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"FactionKillBond", "Reward":80000, "AwardingFaction":"{{faction}}", "VictimFaction":"Liberals of LTT 7786" }""");

    private static JournalEvent Sold(DateTimeOffset at, string type, int count) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"MarketSell", "MarketID":3228970752, "Type":"{{type}}", "Count":{{count}}, "SellPrice":50000, "TotalSale":{{count * 50000}} }""");

    private static JournalEvent Refined(DateTimeOffset at, string type) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"MiningRefined", "Type":"{{type}}", "Type_Localised":"Platinum" }""");

    private static JournalEvent Completed(DateTimeOffset at, string name) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"MissionCompleted", "Faction":"{{Labour}}", "Name":"{{name}}", "MissionID":1, "Reward":10000 }""");

    private static DateTimeOffset At(int minutes) => Accepted.AddMinutes(minutes);

    [Fact]
    public void ABondBeatFiresOnTheThirdBondAfterItBecameCurrent()
    {
        var adventure = Story(new AdventureTrigger { Kind = TriggerKind.Bond, Count = 3, Faction = Labour });

        // Two bonds while the arrive beat is still current do not count.
        var standing = Fold(adventure, [Bond(At(1)), Bond(At(2)), Jump(Lantern, At(3)), Bond(At(4)), Bond(At(5))]);

        Assert.Single(standing.Fired);
        Assert.Equal(2, standing.Counted);
        Assert.Equal($"Kill bonds for {Labour}: 2 of 3", standing.NextTrigger());

        standing = AdventureFold.Apply(standing, Bond(At(6)));

        Assert.True(standing.IsDone);
        Assert.Equal(At(6), standing.FinishedAt);
    }

    [Fact]
    public void ABondForAnotherSideDoesNotCount()
    {
        var adventure = Story(new AdventureTrigger { Kind = TriggerKind.Bond, Count = 1, Faction = Labour });

        var standing = Fold(adventure, [Jump(Lantern, At(1)), Bond(At(2), "Liberals of LTT 7786")]);

        Assert.Equal(0, standing.Counted);
        Assert.False(standing.IsDone);
    }

    [Fact]
    public void ASellBeatAddsTonsAcrossSeveralSales()
    {
        var adventure = Story(new AdventureTrigger { Kind = TriggerKind.Sell, Count = 100, Commodity = "Palladium" });

        var standing = Fold(adventure, [Jump(Lantern, At(1)), Sold(At(2), "palladium", 40), Sold(At(3), "gold", 200), Sold(At(4), "palladium", 59)]);

        Assert.Equal(99, standing.Counted);
        Assert.False(standing.IsDone);

        standing = AdventureFold.Apply(standing, Sold(At(5), "palladium", 1));

        Assert.True(standing.IsDone);
    }

    [Fact]
    public void AMineBeatCountsRefinedPlatinumByItsWrappedType()
    {
        var adventure = Story(new AdventureTrigger { Kind = TriggerKind.Mine, Count = 2, Commodity = "platinum" });

        var standing = Fold(adventure, [Jump(Lantern, At(1)), Refined(At(2), "$platinum_name;"), Refined(At(3), "$painite_name;"), Refined(At(4), "$platinum_name;")]);

        Assert.True(standing.IsDone);
        Assert.Equal(At(4), standing.FinishedAt);
    }

    [Fact]
    public void ACourierBeatCountsCourierMissionsAndNotMassacres()
    {
        var courier = new AdventureTrigger { Kind = TriggerKind.Mission, Count = 1, MissionFamily = "Mission_Courier" };

        Assert.True(AdventureFold.Matches(courier, Completed(At(1), "Mission_Courier_Elections_name")));
        Assert.False(AdventureFold.Matches(courier, Completed(At(1), "Mission_Massacre_RankEmp_name")));
    }

    [Fact]
    public void AMassacreBeatDoesNotCountSkimmerMassacres()
    {
        var massacre = new AdventureTrigger { Kind = TriggerKind.Mission, Count = 1, MissionFamily = "Mission_Massacre" };

        Assert.True(AdventureFold.Matches(massacre, Completed(At(1), "Mission_Massacre_RankEmp_name")));
        Assert.False(AdventureFold.Matches(massacre, Completed(At(1), "Mission_Massacre_Skimmer_name")));
    }

    [Theory]
    [InlineData("Mission_Scan")]
    [InlineData("Mission_RS_Massacre")]
    [InlineData("Mission_OnFoot_Hack")]
    public void ASetAsideFamilyIsRefused(string family)
    {
        var problem = Assert.Single(AdventureValidation.Problems(
            Story(new AdventureTrigger { Kind = TriggerKind.Mission, Count = 2, MissionFamily = family })));

        Assert.Contains("set aside", problem);
    }

    [Fact]
    public void AFamilyThatDoesNotStartWithMissionIsRefused()
    {
        var problem = Assert.Single(AdventureValidation.Problems(
            Story(new AdventureTrigger { Kind = TriggerKind.Mission, Count = 2, MissionFamily = "Courier" })));

        Assert.Contains("Mission_", problem);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void ACountUnderOneIsRefused(int count)
    {
        var problem = Assert.Single(AdventureValidation.Problems(Story(new AdventureTrigger { Kind = TriggerKind.Bounty, Count = count })));

        Assert.Contains("count of 1 or more", problem);
    }

    [Fact]
    public void AnyFamilyStartingWithMissionIsAccepted()
    {
        Assert.Empty(AdventureValidation.Problems(
            Story(new AdventureTrigger { Kind = TriggerKind.Mission, Count = 2, MissionFamily = "Mission_Sightseeing" })));
    }

    [Fact]
    public void ACountedBeatNeedsOnlyItsCountToBeResolved()
    {
        Assert.True(new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 1 }.IsResolved);
        Assert.False(new AdventureTrigger { Kind = TriggerKind.Bounty }.IsResolved);
    }

    [Fact]
    public void TheHandOffSaysTheCountStartsNow()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.Bond, Count = 8, Faction = Labour };

        Assert.Equal($"Next: earn 8 kill bonds for {Labour}, counted from now. Kill bonds for {Labour}: 0 of 8.", trigger.HandOff());
    }

    [Fact]
    public void ReplayingTheJournalGivesTheSameProgressAsWatchingIt()
    {
        var adventure = Story(new AdventureTrigger { Kind = TriggerKind.Bond, Count = 5, Faction = Labour });
        JournalEvent[] events = [Commander("F1", At(-1)), Bond(At(1)), Jump(Lantern, At(2)), Bond(At(3)), Bond(At(4))];

        var live = Book();
        live.Write("F1", adventure);

        foreach (var journalEvent in events)
        {
            live.Observe(journalEvent, "F1");
        }

        var replayed = Book("replay");
        replayed.Write("F1", adventure);
        var file = Path.Combine(_folder, "Journal.2026-08-22T194000.01.log");
        File.WriteAllLines(file, events.Select(e => e.Raw.GetRawText()));
        replayed.CatchUp([file]);

        var watched = live.Standing("F1", adventure.Key)!;
        var caughtUp = replayed.Standing("F1", adventure.Key)!;

        Assert.Equal(2, watched.Counted);
        Assert.Equal(watched.Counted, caughtUp.Counted);
        Assert.Equal(watched.Fired, caughtUp.Fired);
    }

    [Fact]
    public void ACountedBeatSurvivesTheFile()
    {
        var path = Path.Combine(_folder, "stored.json");
        var adventure = Story(new AdventureTrigger { Kind = TriggerKind.Sell, Count = 100, Commodity = "palladium", MarketId = 3228970752 });

        new AdventureStore(path, new DiskFileSystem(), NullLogger<AdventureStore>.Instance).Save("F1", adventure);
        var store = new AdventureStore(path, new DiskFileSystem(), NullLogger<AdventureStore>.Instance);
        store.Poll();

        var trigger = Assert.Single(store.For("F1")).Beats[1].Trigger;

        Assert.Equal(TriggerKind.Sell, trigger.Kind);
        Assert.Equal(100, trigger.Count);
        Assert.Equal("palladium", trigger.Commodity);
        Assert.Equal(3228970752, trigger.MarketId);
    }

    private AdventureBook Book(string name = "live")
    {
        var store = new AdventureStore(Path.Combine(_folder, name + ".json"), new DiskFileSystem(), NullLogger<AdventureStore>.Instance);
        return new AdventureBook(store, NullLogger<AdventureBook>.Instance);
    }
}
