using D47.Core.Activities;
using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class AComfortChapterPointsAtANeverRunMissionFamilyTests
{
    private const int Seeds = 200;

    private static readonly string[] Run = ["Mission_Courier", "Mission_Delivery"];

    private static readonly ShipLoadout Ship = new() { Type = "cobramkiii", CargoCapacity = 64 };

    private static readonly RankState Ranks = new()
    {
        TakenAt = DateTimeOffset.UnixEpoch,
        Standings = [new RankStanding("Combat", 0), new RankStanding("Soldier", 0)],
    };

    private static bool NeverRun(string family) => !Run.Contains(family);

    private static DrawnActivity First(uint seed, ShipLoadout? ship = null, bool odyssey = true, IReadOnlyList<string>? refused = null, Func<string, bool>? neverCompleted = null) =>
        ChapterDraw.Draw(
            CareerWeights.Even, Ranks, ship ?? Ship, odyssey, [], refused, true, CareerStatistics.Empty, 0, seed, comfortFirst: (int)seed, neverCompleted: neverCompleted ?? NeverRun)[0];

    [Fact]
    public void ACompletedMissionIsFiledUnderItsFamilyAndUnderMissions()
    {
        var ledger = new ActivityLedger(null, new MemoryInstall().Files, NullLogger.Instance);
        var load = JournalEvent.TryParse("""{"timestamp":"2026-10-01T10:00:00Z","event":"LoadGame","FID":"F1"}""", NullLogger.Instance, out var started);
        var done = JournalEvent.TryParse("""{"timestamp":"2026-10-02T10:00:00Z","event":"MissionCompleted","Name":"Mission_Smuggle_Famine_name"}""", NullLogger.Instance, out var completed);

        Assert.True(load && done);
        ledger.Apply([started!, completed!]);

        Assert.NotNull(ledger.LastDone("mission:Mission_Smuggle", "F1"));
        Assert.NotNull(ledger.LastDone("missions", "F1"));
        Assert.Null(ledger.LastDone("mission:Mission_Courier", "F1"));
    }

    [Fact]
    public void AnOnFootNameFilesUnderTheLongestFamilyItStartsWith()
    {
        Assert.Equal("mission:Mission_OnFoot_RebootRestore", ActivityLedger.MissionFamilyKey("Mission_OnFoot_RebootRestore_name"));
        Assert.Equal("mission:Mission_Courier", ActivityLedger.MissionFamilyKey("Mission_Courier_Boom_name"));
        Assert.Null(ActivityLedger.MissionFamilyKey("Mission_Unheard_Of"));
    }

    [Fact]
    public void TheSlotIsAMissionFamilyTheCommanderHasNotCompleted()
    {
        for (uint seed = 0; seed < Seeds; seed++)
        {
            var slot = First(seed);

            Assert.Equal(TriggerKind.Mission, slot.Kind);
            Assert.DoesNotContain(slot.Family, Run);
        }
    }

    [Fact]
    public void AFamilyThatFailsAGateIsNeverTheSlot()
    {
        var held = ChapterActivities.Held(Ranks, Ship, odyssey: false);

        for (uint seed = 0; seed < Seeds; seed++)
        {
            var family = First(seed, odyssey: false).Family!;

            Assert.DoesNotContain("Passenger", family);
            Assert.DoesNotContain("Sightseeing", family);
            Assert.DoesNotContain("OnFoot", family);
            Assert.Contains(ChapterActivities.All, row => row.Family == family && ChapterActivities.Passes(row, held));
        }
    }

    [Fact]
    public void ARefusedFamilyIsNeverTheSlot()
    {
        string[] refused = ["mission:Mission_Smuggle", "mission:Mission_Piracy"];

        for (uint seed = 0; seed < Seeds; seed++)
        {
            Assert.DoesNotContain(First(seed, refused: refused).Family, new[] { "Mission_Smuggle", "Mission_Piracy" });
        }
    }

    [Fact]
    public void WhenEveryFamilyHasBeenCompletedTheSlotIsTheOneTheLeastDoneRuleChooses()
    {
        for (uint seed = 0; seed < Seeds; seed++)
        {
            var plain = ChapterDraw.Draw(CareerWeights.Even, Ranks, Ship, true, [], null, true, CareerStatistics.Empty, 0, seed, comfortFirst: (int)seed);

            Assert.Equal(plain[0], First(seed, neverCompleted: _ => false));
        }
    }
}
