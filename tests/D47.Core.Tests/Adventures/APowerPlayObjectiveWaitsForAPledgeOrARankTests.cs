using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Adventures;

public class APowerPlayObjectiveWaitsForAPledgeOrARankTests
{
    private const string Join =
        """{"timestamp":"2026-10-01T00:00:00Z","event":"PowerplayJoin","Power":"Edmund Mahon"}""";

    private const string Rank6 =
        """{"timestamp":"2026-10-01T00:00:00Z","event":"PowerplayRank","Power":"Edmund Mahon","Rank":6}""";

    private const string Left =
        """{"timestamp":"2026-10-01T00:00:00Z","event":"PowerplayLeave","Power":"Edmund Mahon"}""";

    private const string PledgedLoad =
        """{"timestamp":"2026-10-01T00:00:00Z","event":"Powerplay","Power":"Edmund Mahon","Rank":2,"Merits":100,"TimePledged":1000}""";

    private static AdventureTrigger Pledge(string? power) => new() { Kind = TriggerKind.Pledge, Filter = power };

    private static AdventureTrigger PowerRank(int rank) => new() { Kind = TriggerKind.PowerRank, Rank = rank };

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed), line);
        return parsed!;
    }

    [Fact]
    public void AJoinMeetsAPledgeToThatPowerOrToAnyPowerAndNotToAnother()
    {
        var line = Event(Join);

        Assert.True(AdventureFold.Matches(Pledge("Edmund Mahon"), line));
        Assert.True(AdventureFold.Matches(Pledge(null), line));
        Assert.False(AdventureFold.Matches(Pledge("Aisling Duval"), line));
    }

    [Fact]
    public void ARankAtLeastTheAskedRankMeetsAPowerRank()
    {
        var line = Event(Rank6);

        Assert.True(AdventureFold.Matches(PowerRank(5), line));
        Assert.True(AdventureFold.Matches(PowerRank(6), line));
        Assert.False(AdventureFold.Matches(PowerRank(7), line));
        Assert.False(AdventureFold.Matches(PowerRank(5), Event(Join)));
    }

    [Fact]
    public void BothKindsAreDescribedAndResolved()
    {
        Assert.Equal("pledge to Edmund Mahon", Pledge("Edmund Mahon").Describe());
        Assert.Equal("pledge to a Power", Pledge(null).Describe());
        Assert.Equal("reach PowerPlay rank 6", PowerRank(6).Describe());
        Assert.True(Pledge(null).IsResolved);
        Assert.False(Pledge("Nobody").IsResolved);
        Assert.False(new AdventureTrigger { Kind = TriggerKind.PowerRank }.IsResolved);
        Assert.False(Pledge(null).IsCounted);
        Assert.False(PowerRank(6).IsCounted);
    }

    [Fact]
    public void BothKindsAreInTheClosedVocabularyAndTheRefusablePhrases()
    {
        Assert.Contains("pledge", AdventureValidation.Kinds);
        Assert.Contains("powerrank", AdventureValidation.Kinds);
        Assert.True(AdventureValidation.TryKind("powerrank", out var kind));
        Assert.Equal(TriggerKind.PowerRank, kind);
        Assert.Equal("pledge to a Power", RefusedActivities.Phrase(TriggerKind.Pledge, null));
        Assert.Equal("rise through a Power's ranks", RefusedActivities.Phrase(TriggerKind.PowerRank, null));
    }

    [Fact]
    public void APowerNotAmongTheTwelveIsRefusedByTheFileCheck()
    {
        Assert.Single(AdventureValidation.PledgeProblems("Objective 1", Pledge("Zachary Hudson")));
        Assert.Empty(AdventureValidation.PledgeProblems("Objective 1", Pledge("edmund mahon")));
        Assert.Empty(AdventureValidation.PledgeProblems("Objective 1", Pledge(null)));
        Assert.Single(AdventureValidation.PowerRankProblems("Objective 1", new AdventureTrigger { Kind = TriggerKind.PowerRank }));
    }
}
