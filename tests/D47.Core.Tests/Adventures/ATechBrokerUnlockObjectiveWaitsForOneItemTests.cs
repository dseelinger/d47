using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Adventures;

public class ATechBrokerUnlockObjectiveWaitsForOneItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private const string Booster = "Guardian FSD Booster";

    private const string Remaining = "Guardian Hull Reinforcement";

    private const string BoosterUnlock =
        """{"timestamp":"2026-10-01T00:00:00Z","event":"TechnologyBroker","BrokerType":"guardian","ItemsUnlocked":[{"Name":"Int_GuardianFSDBooster_Size1"},{"Name":"Int_GuardianFSDBooster_Size2"}]}""";

    private static AdventureTrigger Unlock(string filter) => new() { Kind = TriggerKind.Unlock, Filter = filter };

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed), line);
        return parsed!;
    }

    private static string UnlockEverythingGuardianBut(string kept) =>
        """{"timestamp":"2026-10-01T00:00:00Z","event":"TechnologyBroker","BrokerType":"guardian","ItemsUnlocked":["""
        + string.Join(",", TechBrokerCatalogue.Joined
            .Where(row => row.BrokerType == TechBrokerCatalogue.Guardian && row.Name != kept)
            .Select(row => $$"""{"Name":"{{row.Symbols[0]}}"}"""))
        + "]}";

    private static string Beats(string filter) => $$"""
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Broker", "function": "resolution", "kind": "unlock", "reason": "Someone there knows about the burst.", "filter": "{{filter}}", "line": "The broker opens the case."}
        ]}
        """;

    [Fact]
    public void TheBoosterLineMeetsAnUnlockForTheBoosterAndNotForAShockCannon()
    {
        var line = Event(BoosterUnlock);

        Assert.True(AdventureFold.Matches(Unlock(Booster), line));
        Assert.False(AdventureFold.Matches(Unlock("Guardian Shard Cannon (Fixed, Small)"), line));
    }

    [Fact]
    public void AnotherEventDoesNotMeetAnUnlock() =>
        Assert.False(AdventureFold.Matches(Unlock(Booster), Event("""{"timestamp":"2026-10-01T00:00:00Z","event":"Docked","MarketID":1}""")));

    [Fact]
    public void AnUnlockThatNamesNoRowIsNotResolved()
    {
        Assert.False(Unlock("Warp Drive").IsResolved);
        Assert.False(new AdventureTrigger { Kind = TriggerKind.Unlock }.IsResolved);
        Assert.True(Unlock(Booster).IsResolved);
        Assert.False(Unlock(Booster).IsCounted);
    }

    [Fact]
    public void AnUnlockIsDescribedWithItsBroker()
    {
        Assert.Equal("unlock the Guardian FSD Booster at a Guardian tech broker", Unlock(Booster).Describe());
        Assert.Equal("Next: unlock the Guardian FSD Booster at a Guardian tech broker.", Unlock(Booster).HandOff());
    }

    [Fact]
    public void AnUnknownRowIsRefusedByTheFileCheck()
    {
        Assert.Single(AdventureValidation.UnlockProblems("Objective 1", Unlock("Warp Drive"), null));
        Assert.Empty(AdventureValidation.UnlockProblems("Objective 1", Unlock(Booster), null));
    }
}
