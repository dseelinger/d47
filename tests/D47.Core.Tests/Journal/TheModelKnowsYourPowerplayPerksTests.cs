using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>#600: the situation lists the perks the pledged rank holds and whose territory this is.</summary>
public class TheModelKnowsYourPowerplayPerksTests
{
    private static string Snapshot(string power, int rank) =>
        $$"""{ "timestamp":"2026-09-24T13:15:47Z", "event":"Powerplay", "Power":"{{power}}", "Rank":{{rank}}, "Merits":45263, "TimePledged":22514119 }""";

    private static string Block(string power, int rank, string? controlling)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"2026-09-24T13:15:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        var control = controlling is null ? "" : $$""", "ControllingPower":"{{controlling}}" """;
        store.Apply(Event($$"""{"timestamp":"2026-09-24T13:15:01Z","event":"Location","StarSystem":"Lembava","Docked":false{{control}}}"""));
        store.Apply(Event(Snapshot(power, rank)));

        return Situation.Describe(store.Active!) ?? "";
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void OwnTerritoryIsSaidWithThePerksHeld()
    {
        var block = Block("Li Yong-Rui", 8, "Li Yong-Rui");

        Assert.Contains("Powerplay perks at rank 8, in Li Yong-Rui's territory: rebuy -40%, exploration data sales +20%. This system is Li Yong-Rui's.", block);
    }

    [Fact]
    public void ARivalsTerritoryIsNamedAndNotClaimedAsOurs()
    {
        var block = Block("Edmund Mahon", 48, "Jerome Archer");

        Assert.Contains("outside it, rebuy", block);
        Assert.Contains("This system is controlled by Jerome Archer.", block);
        Assert.DoesNotContain("is Edmund Mahon's.", block);
    }

    [Fact]
    public void ASystemWithNoControllingPowerSaysSo()
    {
        Assert.Contains("This system has no controlling Power.", Block("Li Yong-Rui", 8, null));
    }

    [Fact]
    public void RankZeroAddsNoPerksLine()
    {
        Assert.DoesNotContain("Powerplay perks", Block("Li Yong-Rui", 0, "Li Yong-Rui"));
    }

    [Fact]
    public void APowerOutsideTheTableAddsNoPerksLine()
    {
        Assert.DoesNotContain("Powerplay perks", Block("Nobody Known", 8, "Nobody Known"));
    }
}
