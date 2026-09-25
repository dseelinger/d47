using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>#455: the Powerplay line carries the Commander's merit total with their Power.</summary>
public class TheModelKnowsTheMeritTotalTests
{
    private const string Snapshot =
        """{ "timestamp":"2026-09-24T13:15:47Z", "event":"Powerplay", "Power":"Li Yong-Rui", "Rank":8, "Merits":45263, "TimePledged":22514119 }""";

    private const string Gain =
        """{ "timestamp":"2026-09-24T14:34:33Z", "event":"PowerplayMerits", "Power":"Li Yong-Rui", "MeritsGained":29, "TotalMerits":45292 }""";

    private static CommanderGameState Replay(params string[] lines)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"2026-09-24T13:15:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        store.Apply(Event("""{"timestamp":"2026-09-24T13:15:01Z","event":"Location","StarSystem":"Lembava","Docked":false}"""));

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void AGainAfterTheSnapshotCarriesTheRunningTotal()
    {
        var state = Replay(Snapshot, Gain);

        Assert.Equal(45292, state.Pledge.Merits);
        Assert.Contains("Powerplay: pledged to Li Yong-Rui, rank 8, 45,292 merits.", Situation.Describe(state));
    }

    [Fact]
    public void AGainForAnotherPowerLeavesTheTotalAlone()
    {
        var state = Replay(
            Snapshot,
            """{ "timestamp":"2026-09-24T14:34:33Z", "event":"PowerplayMerits", "Power":"Aisling Duval", "MeritsGained":29, "TotalMerits":900 }""");

        Assert.Equal(45263, state.Pledge.Merits);
    }

    [Theory]
    [InlineData("""{"timestamp":"2026-09-24T15:00:00Z","event":"PowerplayJoin","Power":"Aisling Duval"}""")]
    [InlineData("""{"timestamp":"2026-09-24T15:00:00Z","event":"PowerplayDefect","FromPower":"Li Yong-Rui","ToPower":"Aisling Duval"}""")]
    [InlineData("""{"timestamp":"2026-09-24T15:00:00Z","event":"PowerplayLeave","Power":"Li Yong-Rui"}""")]
    public void ChangingSidesForgetsTheTotal(string change)
    {
        var state = Replay(Snapshot, Gain, change);

        Assert.Null(state.Pledge.Merits);
        Assert.DoesNotContain("merits", Situation.Describe(state));
    }
}
