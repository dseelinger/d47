using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Adventures;

public class APowerPlayObjectiveWaitsForAPledgeOrARankTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

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

    private static string Beats(string kind, string fields) => $$"""
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Oath", "function": "resolution", "kind": "{{kind}}", {{fields}} "line": "The Power takes note."}
        ]}
        """;

    private static async Task<(AdventureOutcome Outcome, RoundScriptedLlmProvider Provider)> Draft(string beats, params string[] journal)
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(AdventureGeneratorTests.Spine),
            RoundScriptedLlmProvider.Saying(beats),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(beats, 2)),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(beats, 2)));
        var generator = AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), 1, [.. journal]);

        return (await generator.GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None), provider);
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
    public async Task AnUnpledgedCommanderIsOfferedAPledgeAndNotARank()
    {
        var (outcome, provider) = await Draft(Beats("pledge", "\"filter\": \"edmund mahon\","), Left);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var prompt = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("\"pledge\":", prompt);
        Assert.DoesNotContain("\"powerrank\":", prompt);
        Assert.Equal("Edmund Mahon", Assert.Single(outcome.Draft!.Beats, beat => beat.Trigger.Kind == TriggerKind.Pledge).Trigger.Filter);
    }

    [Fact]
    public async Task APledgedCommanderIsOfferedARankAndNotAPledge()
    {
        var (outcome, provider) = await Draft(Beats("powerrank", "\"rank\": 5,"), PledgedLoad);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var prompt = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("\"powerrank\":", prompt);
        Assert.DoesNotContain("\"pledge\":", prompt);
        Assert.Equal(5, Assert.Single(outcome.Draft!.Beats, beat => beat.Trigger.Kind == TriggerKind.PowerRank).Trigger.Rank);
    }

    [Fact]
    public async Task ACommanderWhoseJournalSaysNothingIsOfferedNeither()
    {
        var (_, provider) = await Draft(Beats("arrive", "\"system\": \"Ossen's Lantern\","));

        var prompt = provider.Requests[1].Prompt.History[0].Text;
        Assert.DoesNotContain("\"pledge\":", prompt);
        Assert.DoesNotContain("\"powerrank\":", prompt);
    }

    [Fact]
    public async Task ADraftNamingAPowerThatDoesNotExistIsRefused()
    {
        var (outcome, _) = await Draft(Beats("pledge", "\"filter\": \"Zachary Hudson\","), Left);

        Assert.False(outcome.Succeeded);
        Assert.Contains("names the Power \"Zachary Hudson\", which is not one of the twelve Powers", outcome.Refusal);
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
