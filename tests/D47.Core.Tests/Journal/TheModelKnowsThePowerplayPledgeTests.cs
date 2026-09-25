using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>#454: asked who they were pledged to, d47 said it did not know.</summary>
public class TheModelKnowsThePowerplayPledgeTests
{
    private const string Snapshot =
        """{ "timestamp":"2026-09-24T13:15:47Z", "event":"Powerplay", "Power":"Li Yong-Rui", "Rank":8, "Merits":45263, "TimePledged":22514119 }""";

    private static string? Block(params string[] lines)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"2026-09-24T13:15:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        store.Apply(Event("""{"timestamp":"2026-09-24T13:15:01Z","event":"Location","StarSystem":"Lembava","Docked":false}"""));

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        return Situation.Describe(store.Active);
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void TheSnapshotNamesThePowerAndTheRank()
    {
        Assert.Contains("Powerplay: pledged to Li Yong-Rui, rank 8, 45,263 merits.", Block(Snapshot));
    }

    [Fact]
    public void LeavingSaysNotPledged()
    {
        var block = Block(
            Snapshot,
            """{"timestamp":"2026-09-24T13:20:00Z","event":"PowerplayLeave","Power":"Li Yong-Rui"}""");

        Assert.Contains("Powerplay: not pledged to any Power.", block);
        Assert.DoesNotContain("Li Yong-Rui", block);
    }

    [Fact]
    public void JoiningBeforeTheNextSnapshotLeavesTheRankUnknown()
    {
        var block = Block("""{"timestamp":"2026-09-24T13:20:00Z","event":"PowerplayJoin","Power":"Aisling Duval"}""");

        Assert.Contains("Powerplay: pledged to Aisling Duval, rank not yet known.", block);
    }

    [Fact]
    public void NoPowerplayEventMeansNoLine()
    {
        Assert.DoesNotContain("Powerplay", Block());
    }
}
