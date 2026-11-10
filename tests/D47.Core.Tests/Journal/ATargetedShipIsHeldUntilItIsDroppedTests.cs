using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class ATargetedShipIsHeldUntilItIsDroppedTests
{
    private const string Stage0 = """{ "timestamp":"2025-07-05T02:18:48Z", "event":"ShipTargeted", "TargetLocked":true, "Ship":"adder", "ScanStage":0 }""";
    private const string Stage1 = """{ "timestamp":"2025-07-05T02:18:50Z", "event":"ShipTargeted", "TargetLocked":true, "Ship":"adder", "ScanStage":1, "PilotName":"$npc_name_decorate:#name=Doug Quiad;", "PilotName_Localised":"Doug Quiad", "PilotRank":"Master" }""";
    private const string Stage2 = """{ "timestamp":"2025-07-05T02:18:52Z", "event":"ShipTargeted", "TargetLocked":true, "Ship":"adder", "ScanStage":2, "PilotName":"$npc_name_decorate:#name=Doug Quiad;", "PilotName_Localised":"Doug Quiad", "PilotRank":"Master", "ShieldHealth":100.000000, "HullHealth":100.000000 }""";
    private const string Stage3 = """{ "timestamp":"2025-07-05T02:18:54Z", "event":"ShipTargeted", "TargetLocked":true, "Ship":"adder", "ScanStage":3, "PilotName":"$npc_name_decorate:#name=Doug Quiad;", "PilotName_Localised":"Doug Quiad", "PilotRank":"Master", "ShieldHealth":100.000000, "HullHealth":100.000000, "Faction":"The Prime Spade", "LegalStatus":"Wanted", "Bounty":92454 }""";
    private const string Dropped = """{ "timestamp":"2025-07-03T23:38:15Z", "event":"ShipTargeted", "TargetLocked":false }""";
    private const string CommanderStage3 = """{ "timestamp":"2025-07-03T23:38:14Z", "event":"ShipTargeted", "TargetLocked":true, "Ship":"corsair", "ScanStage":3, "PilotName":"$RolePanel2_unmanned; $cmdr_decorate:#name=CALVIN INSTI;", "PilotName_Localised":"unmanned CMDR CALVIN INSTI", "PilotRank":"Mostly Harmless", "SquadronID":"OV40", "ShieldHealth":100.000000, "HullHealth":100.000000, "LegalStatus":"Clean", "Power":"Edmund Mahon" }""";

    private static DateTimeOffset At(int minute, int second) => new(2025, 7, 5, 2, minute, second, TimeSpan.Zero);

    private const string Subsystem = """{ "timestamp":"2025-07-05T02:19:10Z", "event":"ShipTargeted", "TargetLocked":true, "Ship":"adder", "ScanStage":3, "PilotName":"$npc_name_decorate:#name=Doug Quiad;", "PilotName_Localised":"Doug Quiad", "ShieldHealth":62.4, "HullHealth":88.0, "Subsystem":"$int_powerplant_size4_class5;", "Subsystem_Localised":"Power Plant", "SubsystemHealth":75.0 }""";

    [Fact]
    public void ATargetedSubsystemHoldsItsNameAndHealthAndEarlierStagesHoldNone()
    {
        var target = Fold(Subsystem).Target!;

        Assert.Equal("Power Plant", target.Subsystem);
        Assert.Equal(75, target.SubsystemHealth);
        Assert.Equal(62, target.ShieldHealth);
        Assert.Equal(88, target.HullHealth);

        var early = Fold(Stage0, Stage1).Target!;

        Assert.Null(early.ShieldHealth);
        Assert.Null(early.HullHealth);
        Assert.Null(early.Subsystem);
        Assert.Null(early.SubsystemHealth);
    }

    private static CommanderGameState Fold(params string[] lines)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Tester"));

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state.Apply(parsed!);
        }

        return state;
    }

    [Fact]
    public void AStageZeroTargetIsHeldWithNoName()
    {
        var target = Fold(Stage0).Target;

        Assert.Equal(new TargetedShip("adder", ScannedAt: At(18, 48)), target);
        Assert.False(target!.IsNameable);
    }

    [Fact]
    public void AStageOneTargetBecomesNameable()
    {
        var target = Fold(Stage0, Stage1).Target;

        Assert.Equal(new TargetedShip("adder", "Doug Quiad", "Master", ScannedAt: At(18, 50)), target);
        Assert.True(target!.IsNameable);
    }

    [Fact]
    public void AStageTwoTargetKeepsTheNameAndRank()
    {
        Assert.Equal(
            new TargetedShip("adder", "Doug Quiad", "Master", ShieldHealth: 100, HullHealth: 100, ScannedAt: At(18, 52)),
            Fold(Stage0, Stage1, Stage2).Target);
    }

    [Fact]
    public void AStageThreeTargetAddsFactionLegalStatusAndBounty()
    {
        Assert.Equal(
            new TargetedShip("adder", "Doug Quiad", "Master", "The Prime Spade", "Wanted", Bounty: 92454, ShieldHealth: 100, HullHealth: 100, ScannedAt: At(18, 54)),
            Fold(Stage0, Stage1, Stage2, Stage3).Target);
    }

    [Fact]
    public void DroppingTheTargetClearsIt()
    {
        Assert.Null(Fold(Stage0, Stage1, Stage2, Stage3, Dropped).Target);
    }

    [Fact]
    public void ACommanderTargetIsHeldAsACommanderWithNoName()
    {
        var target = Fold(CommanderStage3).Target;

        Assert.Equal(new TargetedShip("corsair", null, "Mostly Harmless", null, "Clean", true, ShieldHealth: 100, HullHealth: 100, ScannedAt: new DateTimeOffset(2025, 7, 3, 23, 38, 14, TimeSpan.Zero)), target);
        Assert.False(target!.IsNameable);
    }

    [Theory]
    [InlineData("""{ "timestamp":"2025-07-05T02:20:00Z", "event":"FSDJump", "StarSystem":"Sol", "SystemAddress":10477373803 }""")]
    [InlineData("""{ "timestamp":"2025-07-05T02:20:00Z", "event":"CarrierJump", "StarSystem":"Sol", "SystemAddress":10477373803 }""")]
    [InlineData("""{ "timestamp":"2025-07-05T02:20:00Z", "event":"Docked", "StationName":"Abraham Lincoln", "StarSystem":"Sol" }""")]
    [InlineData("""{ "timestamp":"2025-07-05T02:20:00Z", "event":"LoadGame", "FID":"F1", "Commander":"Tester" }""")]
    public void AJumpDockingOrLoadGameClearsTheTarget(string clearing)
    {
        Assert.Null(Fold(Stage0, Stage1, clearing).Target);
    }
}
