using System.Globalization;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>A NewCommander for a Frontier id empties that id's histories, live and in the history walk (#1802).</summary>
public sealed class AnEraseClearsEveryGameStateHistoryTests
{
    private const string F = "F1";

    private const string G = "G2";

    private const string Booster = "Int_GuardianFSDBooster_Size1";

    private const string Reinforcement = "Int_GuardianHullReinforcement_Size1_Class1";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string Line(string time, string body) => "{\"timestamp\":\"2026-10-" + time + "Z\"," + body + "}";

    private static string Login(string fid, string day) =>
        Line(day + "T09:00:00", "\"event\":\"LoadGame\",\"FID\":\"" + fid + "\",\"Commander\":\"Jameson\"");

    private static string Erase(string fid, string day) =>
        Line(day + "T10:00:00", "\"event\":\"NewCommander\",\"FID\":\"" + fid + "\",\"Name\":\"Jameson\",\"Package\":\"Default\"");

    private static string[] Journals(MemoryFileSystem files, params string[][] contents)
    {
        var paths = new string[contents.Length];

        for (var i = 0; i < contents.Length; i++)
        {
            paths[i] = $"j/Journal.2026-10-{10 + i:00}T000000.01.log";
            files.WriteLines(paths[i], contents[i]);
        }

        return paths;
    }

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Stored(string day, int id, string type) =>
        Line(
            day + "T09:30:00",
            "\"event\":\"StoredShips\",\"StarSystem\":\"Somewhere\",\"StationName\":\"Somewhere Station\",\"ShipsHere\":[{\"ShipID\":"
            + id + ",\"ShipType\":\"" + type + "\",\"Value\":1}],\"ShipsRemote\":[]");

    private static string CarrierStats(string day) =>
        Line(day + "T09:05:00", "\"event\":\"CarrierStats\",\"CarrierID\":1,\"Callsign\":\"BNH-T2F\",\"Name\":\"Sacred Fire\",\"DockingAccess\":\"all\",\"FuelLevel\":900");

    private static string CarrierAt(string day, string system) =>
        Line(day + "T09:06:00", "\"event\":\"CarrierLocation\",\"CarrierID\":1,\"CarrierType\":\"FleetCarrier\",\"StarSystem\":\"" + system + "\"");

    private static string DockedAt(string day, long market, string ship) =>
        Line(
            day + "T09:05:00",
            "\"event\":\"Docked\",\"StationName\":\"$EXT_PANEL_ColonisationShip; " + ship
            + "\",\"StationType\":\"SurfaceStation\",\"StarSystem\":\"Col 285\",\"SystemAddress\":123,\"MarketID\":" + market);

    private static string Depot(string day, long market, double progress) =>
        Line(
            day + "T09:06:00",
            "\"event\":\"ColonisationConstructionDepot\",\"MarketID\":" + market + ",\"ConstructionProgress\":"
            + progress.ToString(CultureInfo.InvariantCulture)
            + ",\"ConstructionComplete\":false,\"ConstructionFailed\":false,\"ResourcesRequired\":[{\"Name\":\"$steel_name;\",\"Name_Localised\":\"Steel\",\"RequiredAmount\":500,\"ProvidedAmount\":80,\"Payment\":3239}]");

    private static string Unlock(string day, string item) =>
        Line(day + "T09:10:00", "\"event\":\"TechnologyBroker\",\"BrokerType\":\"guardian\",\"ItemsUnlocked\":[{\"Name\":\"" + item + "\"}]");

    private static string Pledge(string day) =>
        Line(day + "T09:01:00", "\"event\":\"Powerplay\",\"Power\":\"Li Yong-Rui\",\"Rank\":8,\"Merits\":44000");

    private static string Merits(string day, int gained) =>
        Line(day + "T09:20:00", "\"event\":\"PowerplayMerits\",\"Power\":\"Li Yong-Rui\",\"MeritsGained\":" + gained + ",\"TotalMerits\":45000");

    private static string Accepted(string day, long id) =>
        Line(
            day + "T09:10:00",
            "\"event\":\"MissionAccepted\",\"Faction\":\"Party\",\"Name\":\"Mission_Courier\",\"LocalisedName\":\"Courier\",\"DestinationSystem\":\"Wadjuk\",\"DestinationStation\":\"Crown\",\"Expiry\":\"2026-10-30T00:00:00Z\",\"Reward\":1000,\"MissionID\":"
            + id);

    private static string Snapshot(string day, long id) =>
        Line(
            day + "T09:11:00",
            "\"event\":\"Missions\",\"Active\":[{\"MissionID\":" + id
            + ",\"Name\":\"Mission_Courier\",\"PassengerMission\":false,\"Expires\":60}],\"Failed\":[],\"Complete\":[]");

    private static string DockedAtMarket(string day, long market) =>
        Line(
            day + "T09:05:00",
            "\"event\":\"Docked\",\"StationName\":\"Hutton\",\"MarketID\":" + market
            + ",\"StarSystem\":\"Alpha\",\"SystemAddress\":1,\"StationFaction\":{\"Name\":\"Alpha Mining\"}");

    private static string Sale(string day, long market) =>
        Line(
            day + "T09:07:00",
            "\"event\":\"MarketSell\",\"MarketID\":" + market
            + ",\"Type\":\"gold\",\"Count\":10,\"SellPrice\":50000,\"TotalSale\":500000,\"AvgPricePaid\":40000");

    private static string[] MiningRun(string day, string type) =>
    [
        Line(day + "T09:01:00", "\"event\":\"LaunchDrone\",\"Type\":\"Prospector\""),
        Line(day + "T09:02:00", "\"event\":\"MiningRefined\",\"Type\":\"" + type + "\""),
        Line(day + "T09:03:00", "\"event\":\"Docked\",\"StationName\":\"Hutton\",\"MarketID\":100"),
    ];

    [Fact]
    public void AFleetFromBeforeTheEraseIsGoneAndAnotherCommandersIsKept()
    {
        var files = new MemoryFileSystem();
        var paths = Journals(
            files,
            [Login(G, "10"), Stored("10", 7, "Python")],
            [Login(F, "11"), Stored("11", 51, "Anaconda")],
            [Erase(F, "12")],
            [Login(F, "13"), Line("13T09:10:00", "\"event\":\"Loadout\",\"Ship\":\"sidewinder\",\"ShipID\":1,\"ShipName\":\"New\"")]);

        var fleets = FleetBackfill.FromHistory(files, paths, NullLogger.Instance, Cancel);

        Assert.False(fleets.ContainsKey(F));
        Assert.Equal([7], fleets[G].Ships.Select(ship => ship.ShipId));
    }

    [Fact]
    public void ACarrierFromBeforeTheEraseIsGoneAndOnlyALaterLocationRemains()
    {
        var files = new MemoryFileSystem();
        var paths = Journals(
            files,
            [Login(G, "10"), CarrierAt("10", "Colonia")],
            [Login(F, "11"), CarrierStats("11"), CarrierAt("11", "Meene")],
            [Erase(F, "12")],
            [Login(F, "13"), CarrierAt("13", "Sol")]);

        var carriers = CarrierBackfill.FromHistory(files, paths, NullLogger.Instance, Cancel);

        Assert.Equal("Sol", carriers[F].StarSystem);
        Assert.Null(carriers[F].CallSign);
        Assert.Equal("Colonia", carriers[G].StarSystem);
    }

    [Fact]
    public void AConstructionSiteFromBeforeTheEraseIsGoneAndAnotherCommandersIsKept()
    {
        var files = new MemoryFileSystem();
        var paths = Journals(
            files,
            [Login(G, "10"), DockedAt("10", 200, "Other"), Depot("10", 200, 0.5)],
            [Login(F, "11"), DockedAt("11", 100, "Old"), Depot("11", 100, 0.25)],
            [Erase(F, "12")],
            [Login(F, "13"), DockedAt("13", 300, "Fresh"), Depot("13", 300, 0.75)]);

        var sites = ColonisationBackfill.FromHistory(files, paths, NullLogger.Instance, Cancel);

        Assert.Equal([300L], sites[F].All.Select(site => site.MarketId));
        Assert.Equal([200L], sites[G].All.Select(site => site.MarketId));
    }

    [Fact]
    public void ATechBrokerUnlockFromBeforeTheEraseIsGoneAndAnotherCommandersIsKept()
    {
        var files = new MemoryFileSystem();
        var paths = Journals(
            files,
            [Login(G, "10"), Unlock("10", Booster)],
            [Login(F, "11"), Unlock("11", Booster)],
            [Erase(F, "12")],
            [Login(F, "13"), Unlock("13", Reinforcement)]);

        var unlocks = TechBrokerBackfill.FromHistory(files, paths, NullLogger.Instance, Cancel);

        Assert.DoesNotContain(unlocks[F].NotKnownToBeUnlocked("guardian"), row => row.Name == "Guardian Hull Reinforcement");
        Assert.Contains(unlocks[F].NotKnownToBeUnlocked("guardian"), row => row.Name == "Guardian FSD Booster");
        Assert.DoesNotContain(unlocks[G].NotKnownToBeUnlocked("guardian"), row => row.Name == "Guardian FSD Booster");
        Assert.Contains(unlocks[G].NotKnownToBeUnlocked("guardian"), row => row.Name == "Guardian Hull Reinforcement");
    }

    [Fact]
    public void CycleMeritsFromBeforeTheEraseAreGoneAndAnotherCommandersAreKept()
    {
        var files = new MemoryFileSystem();
        var paths = Journals(
            files,
            [Login(G, "10"), Pledge("10"), Merits("10", 100)],
            [Login(F, "11"), Pledge("11"), Merits("11", 900)],
            [Erase(F, "12")],
            [Login(F, "13"), Pledge("13"), Merits("13", 40)]);

        var merits = PowerplayCycleBackfill.FromHistory(files, paths, NullLogger.Instance, Cancel);
        var since = DateTimeOffset.Parse("2026-10-01T00:00:00Z", CultureInfo.InvariantCulture);

        Assert.Equal(40, merits[F].Since(since));
        Assert.Equal(100, merits[G].Since(since));
    }

    [Fact]
    public void MissionsFromBeforeTheEraseAreGoneAndOnlyALaterAcceptRemains()
    {
        var files = new MemoryFileSystem();
        var paths = Journals(
            files,
            [Login(F, "10"), Accepted("10", 1), Snapshot("10", 1)],
            [Erase(F, "11")],
            [Login(F, "12"), Accepted("12", 2)]);

        var boards = MissionBackfill.FromHistory(paths, NullLogger.Instance, files.ReadText, Cancel);

        Assert.Equal([2L], boards[F].Missions.Select(mission => mission.Id));
    }

    [Fact]
    public void BgsWorkFromBeforeTheEraseIsGoneAndAnotherCommandersIsKept()
    {
        var files = new MemoryFileSystem();
        var paths = Journals(
            files,
            [Login(G, "10"), DockedAtMarket("10", 100), Sale("10", 100)],
            [Login(F, "11"), DockedAtMarket("11", 100), Sale("11", 100)],
            [Erase(F, "12")],
            [Login(F, "13"), DockedAtMarket("13", 100), Sale("13", 100)]);

        var work = BgsWorkBackfill.FromHistory(files, paths, NullLogger.Instance, Cancel);

        Assert.Single(work[F].Entries);
        Assert.Single(work[G].Entries);
    }

    [Fact]
    public void MiningRunsFromBeforeTheEraseAreGoneAndAnotherCommandersAreKept()
    {
        var files = new MemoryFileSystem();
        var paths = Journals(
            files,
            [Login(G, "10"), .. MiningRun("10", "Platinum")],
            [Login(F, "11"), .. MiningRun("11", "Gold")],
            [Erase(F, "12")],
            [Login(F, "13"), .. MiningRun("13", "Painite")]);

        var runs = MiningBackfill.FromHistory(files, paths, NullLogger.Instance, Cancel);

        Assert.Single(runs[F]);
        Assert.Single(runs[G]);
        Assert.Equal(runs[F][0].Refined.Keys.Single(), runs[F][0].Refined.Keys.Single(key => key.Contains("painite", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ANewCommanderEmptiesTheLiveHistories()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
        {
            Login(F, "10"),
            Stored("10", 51, "Anaconda"),
            CarrierStats("10"),
            CarrierAt("10", "Meene"),
            DockedAt("10", 100, "Old"),
            Depot("10", 100, 0.25),
            Unlock("10", Booster),
            Pledge("10"),
            Merits("10", 900),
        })
        {
            store.Apply(Event(line));
        }

        var state = store.Active!;
        Assert.True(state.Fleet.IsKnown);
        Assert.True(state.Carrier.IsKnown);
        Assert.True(state.Colonisation.IsKnown);
        Assert.True(state.TechBroker.IsKnown);
        Assert.True(state.CycleMerits.IsKnown);

        store.Apply(Event(Erase(F, "11")));

        Assert.Equal(FleetRegistry.Empty, state.Fleet);
        Assert.Equal(CarrierState.None, state.Carrier);
        Assert.Equal(ColonisationSites.Empty, state.Colonisation);
        Assert.Equal(TechBrokerUnlocks.Empty, state.TechBroker);
        Assert.Equal(PowerplayCycleMerits.None, state.CycleMerits);
    }
}
