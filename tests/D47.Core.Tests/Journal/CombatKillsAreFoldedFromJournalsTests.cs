using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class CombatKillsAreFoldedFromJournalsTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string At(string time, string body) => "{\"timestamp\":\"" + time + "\"," + body + "}";

    private static CombatRecord Fold(params string[] lines) =>
        lines.Aggregate(CombatRecord.None, (record, line) => record.Apply(Event(line)));

    private static string Bounty(string time, string rest) =>
        At(time, "\"event\":\"Bounty\"," + rest);

    private static string Eagle(string time) =>
        Bounty(time, "\"Target\":\"eagle\",\"TotalReward\":1000,\"VictimFaction\":\"Pirates of X\",\"Rewards\":[{\"Faction\":\"A\",\"Reward\":1000}]");

    [Fact]
    public void ABountyRecordsItsTargetVictimTotalAndEachPayer()
    {
        var kill = Assert.Single(Fold(Bounty(
            "2026-10-10T10:00:00Z",
            "\"Target\":\"anaconda\",\"Target_Localised\":\"Anaconda\",\"TotalReward\":300,\"VictimFaction\":\"Zed Gang\","
            + "\"Rewards\":[{\"Faction\":\"A\",\"Reward\":100},{\"Faction\":\"B\",\"Reward\":200}]")).Kills);

        Assert.Equal((KillKind.Bounty, "anaconda", "Zed Gang", 300L), (kill.Kind, kill.Target, kill.VictimFaction, kill.Awarded));
        Assert.Equal([new KillPayment("A", 100), new KillPayment("B", 200)], kill.PaidBy);
    }

    [Fact]
    public void BothOnFootClassesFoldIntoOneTargetNamedByTheClassOneName()
    {
        var record = Fold(
            Bounty("2026-10-10T10:00:00Z", "\"Target\":\"assaultsuitai_class2\",\"Target_Localised\":\"$AssaultSuitAI_Class1_Name;\",\"TotalReward\":10"),
            Bounty("2026-10-10T10:01:00Z", "\"Target\":\"assaultsuitai_class1\",\"Target_Localised\":\"Commando\",\"TotalReward\":20"));

        Assert.Equal(["assaultsuitai"], record.Kills.Select(kill => kill.Target).Distinct());
        Assert.Equal("Commando", record.TargetName("assaultsuitai_class2"));
        Assert.Equal("Commando", record.TargetName("assaultsuitai"));
    }

    [Fact]
    public void AShipTargetTakesItsNameFromTheShipTableAndAnUnknownOneStaysRaw()
    {
        var record = Fold(Eagle("2026-10-10T10:00:00Z"));

        Assert.Equal("Eagle", record.TargetName("eagle"));
        Assert.Equal("mystery_thing", record.TargetName("mystery_thing"));
    }

    [Fact]
    public void ALocalisedVictimFactionBeatsItsKeyAndAnUnresolvedKeyIsNull()
    {
        var record = Fold(
            Bounty("2026-10-10T10:00:00Z", "\"Target\":\"eagle\",\"TotalReward\":1,\"VictimFaction\":\"$faction_Pirate;\",\"VictimFaction_Localised\":\"Pirates\""),
            Bounty("2026-10-10T10:01:00Z", "\"Target\":\"eagle\",\"TotalReward\":1,\"VictimFaction\":\"$faction_Pirate;\""));

        Assert.Equal(["Pirates", null], record.Kills.Select(kill => kill.VictimFaction));
    }

    [Fact]
    public void ACombatBondHasNoTargetAndTheAwardingFactionAsItsPayer()
    {
        var kill = Assert.Single(Fold(At(
            "2026-10-10T10:00:00Z",
            "\"event\":\"FactionKillBond\",\"Reward\":5000,\"AwardingFaction\":\"Holder\",\"VictimFaction\":\"Losers\"")).Kills);

        Assert.Equal((KillKind.Bond, null, "Losers", 5000L), (kill.Kind, kill.Target, kill.VictimFaction, kill.Awarded));
        Assert.Equal([new KillPayment("Holder", 5000)], kill.PaidBy);
    }

    [Fact]
    public void AKillRecordsTheSystemAndTheVehicleTheCommanderWasIn()
    {
        var record = Fold(
            At("2026-10-10T09:00:00Z", "\"event\":\"FSDJump\",\"StarSystem\":\"Sol\",\"SystemAddress\":10477373803"),
            At("2026-10-10T09:01:00Z", "\"event\":\"Loadout\",\"Ship\":\"anaconda\",\"ShipID\":7,\"ShipName\":\"Grey Lady\""),
            Eagle("2026-10-10T10:00:00Z"),
            At("2026-10-10T10:01:00Z", "\"event\":\"Disembark\",\"SRV\":false"),
            Eagle("2026-10-10T10:02:00Z"),
            At("2026-10-10T10:03:00Z", "\"event\":\"LaunchSRV\""),
            Eagle("2026-10-10T10:04:00Z"),
            At("2026-10-10T10:05:00Z", "\"event\":\"DockSRV\""),
            Eagle("2026-10-10T10:06:00Z"));

        Assert.All(record.Kills, kill => Assert.Equal(10477373803, kill.SystemAddress));
        Assert.Equal(
            [(CombatVehicle.Ship, (int?)7), (CombatVehicle.OnFoot, null), (CombatVehicle.Srv, null), (CombatVehicle.Ship, 7)],
            record.Kills.Select(kill => (kill.Vehicle, kill.ShipId)));
        Assert.Equal("Sol", record.SystemName(10477373803));
        Assert.Null(record.SystemName(1));
        Assert.Equal("Grey Lady (Anaconda)", record.ShipName(7));
    }

    [Fact]
    public void AShipWithoutANameIsNamedByItsType()
    {
        var record = Fold(At("2026-10-10T09:01:00Z", "\"event\":\"ShipyardSwap\",\"ShipType\":\"eagle\",\"ShipID\":3"));

        Assert.Equal("Eagle", record.ShipName(3));
        Assert.Null(record.ShipName(4));
    }

    [Fact]
    public void SinceCountsAndSumsOnlyTheKillsAtOrAfterIt()
    {
        var record = Fold(
            Eagle("2026-10-10T10:00:00Z"),
            Bounty("2026-10-10T11:00:00Z", "\"Target\":\"anaconda\",\"TotalReward\":500,\"VictimFaction\":\"Zed\",\"Rewards\":[{\"Faction\":\"B\",\"Reward\":500}]"));

        var slice = record.Since(DateTimeOffset.Parse("2026-10-10T10:30:00Z"));

        Assert.Equal((1, 500L), (slice.Kills, slice.Awarded));
        Assert.Equal([new CombatGroup<string?>("anaconda", 1, 500)], slice.ByTarget);
        Assert.Equal([new CombatGroup<string?>("Zed", 1, 500)], slice.ByVictimFaction);
        Assert.Equal([new CombatGroup<string>("B", 1, 500)], slice.ByPayingFaction);
        Assert.Equal(2, record.Since(null).Kills);
    }

    [Fact]
    public void BondsGroupUnderOneNullTarget()
    {
        var bond = At("2026-10-10T10:00:00Z", "\"event\":\"FactionKillBond\",\"Reward\":10,\"AwardingFaction\":\"H\",\"VictimFaction\":\"L\"");

        var slice = Fold(bond, bond.Replace("10:00:00", "10:01:00")).Since(null);

        Assert.Equal([new CombatGroup<string?>(null, 2, 20)], slice.ByTarget);
    }

    [Fact]
    public void AMergeCountsAKillInTheWalkAndTheLiveJournalOnce()
    {
        var walk = Fold(Eagle("2026-10-10T10:00:00Z"), Eagle("2026-10-10T10:00:30Z"), Eagle("2026-10-10T10:00:30Z"));
        var live = Fold(
            Eagle("2026-10-10T10:00:30Z"),
            Eagle("2026-10-10T10:00:30Z"),
            Eagle("2026-10-10T10:00:30Z"),
            Eagle("2026-10-10T10:05:00Z"));

        Assert.Equal(5, walk.With(live).Kills.Count);
        Assert.Equal(5, walk.With(walk.With(live)).Kills.Count);
    }

    [Fact]
    public void ARecordIsWholeOnlyAfterAMerge()
    {
        var live = Fold(Eagle("2026-10-10T10:00:00Z"));

        Assert.False(live.IsWhole);
        Assert.True(CombatRecord.None.With(live).IsWhole);
        Assert.True(live.With(CombatRecord.None).IsWhole);
    }

    [Fact]
    public void ARecordIsOnRecordSinceItsFirstEvent()
    {
        var live = Fold(At("2026-10-10T09:00:00Z", "\"event\":\"Music\""), Eagle("2026-10-10T10:00:00Z"));

        Assert.Equal(DateTimeOffset.Parse("2026-10-10T09:00:00Z"), live.OnRecordSince);
    }

    [Fact]
    public void ANewCommanderClearsTheKillsButKeepsTheRecordWhole()
    {
        var record = CombatRecord.None.With(Fold(Eagle("2026-10-10T10:00:00Z")))
            .Apply(Event(At("2026-10-10T11:00:00Z", "\"event\":\"NewCommander\"")));

        Assert.Empty(record.Kills);
        Assert.True(record.IsWhole);
    }

    [Fact]
    public void TwoCommandersKillsInOneWalkLandInTwoRecords()
    {
        var files = new MemoryFileSystem();
        files.WriteText(
            "j/Journal.1.log",
            string.Join("\n",
                At("2026-10-10T09:00:00Z", "\"event\":\"Commander\",\"FID\":\"F1\",\"Name\":\"One\""),
                At("2026-10-10T09:00:01Z", "\"event\":\"LoadGame\",\"FID\":\"F1\",\"Ship\":\"eagle\",\"ShipID\":1"),
                Eagle("2026-10-10T10:00:00Z"),
                At("2026-10-10T11:00:00Z", "\"event\":\"Commander\",\"FID\":\"F2\",\"Name\":\"Two\""),
                At("2026-10-10T11:00:01Z", "\"event\":\"LoadGame\",\"FID\":\"F2\",\"Ship\":\"anaconda\",\"ShipID\":2"),
                Eagle("2026-10-10T12:00:00Z"),
                Eagle("2026-10-10T12:01:00Z")));

        var found = CombatBackfill.FromHistory(files, ["j/Journal.1.log"], NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Equal((1, 2), (found["F1"].Kills.Count, found["F2"].Kills.Count));
        Assert.Equal(1, found["F1"].Kills[0].ShipId);
        Assert.Equal(2, found["F2"].Kills[0].ShipId);
    }

    [Fact]
    public void ANewCommanderInAWalkClearsTheCommanderItNamesNotTheOneBeforeIt()
    {
        var files = new MemoryFileSystem();
        files.WriteText(
            "j/Journal.1.log",
            string.Join("\n",
                At("2026-10-10T09:00:00Z", "\"event\":\"LoadGame\",\"FID\":\"F1\",\"Ship\":\"eagle\",\"ShipID\":1"),
                Eagle("2026-10-10T10:00:00Z"),
                At("2026-10-10T11:00:00Z", "\"event\":\"LoadGame\",\"FID\":\"F2\",\"Ship\":\"eagle\",\"ShipID\":2"),
                Eagle("2026-10-10T12:00:00Z"),
                At("2026-10-10T13:00:00Z", "\"event\":\"NewCommander\",\"FID\":\"F1\",\"Name\":\"Again\""),
                At("2026-10-10T13:00:01Z", "\"event\":\"LoadGame\",\"FID\":\"F1\",\"Ship\":\"eagle\",\"ShipID\":1")));

        var found = CombatBackfill.FromHistory(files, ["j/Journal.1.log"], NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Empty(found["F1"].Kills);
        Assert.Single(found["F2"].Kills);
    }
}
