using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>Every objective except a choice carries a reason, and the core never presents the next objective as an order.</summary>
public sealed class EveryObjectiveStatesWhyTheCommanderGoesTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private const string Reason = "The third sheet's receipt was stamped in Dyson's Hollow, where the freight was last logged.";

    private const string Spine = """
        {"name": "The Unrecoverable Column", "premise": "A ledger will not balance.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The column balances."}
        """;

    private const string GoodBeats = $$"""
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "The freight's last beacon ping came from here.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "A comms specialist there reads signals like the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "reason": "{{Reason}}", "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    private const string RankWithoutReason = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "The freight's last beacon ping came from here.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "A comms specialist there reads signals like the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    [Fact]
    public async Task ACountedObjectiveWithoutAReasonIsRefused()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(RankWithoutReason),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(GoodBeats, 3)),
            RoundScriptedLlmProvider.Saying(Stories.StoryFixtures.NothingObjected));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var refusals = provider.Requests[2].Prompt.History[0].Text.Split("cannot stand")[1];
        Assert.Contains("Objective 3 (The Column Will Not Balance) has no \"reason\"", refusals);
        Assert.DoesNotContain("Objective 1 (The Lantern)", refusals);
    }

    [Fact]
    public async Task ACountedObjectivesReasonSurvivesResolvingAndIsInItsHandOff()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(GoodBeats),
            RoundScriptedLlmProvider.Saying(Stories.StoryFixtures.NothingObjected));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var rank = outcome.Draft!.Beats[2];
        Assert.Equal(Reason, rank.Reason);
        Assert.EndsWith(Reason, rank.HandOff());
    }

    [Fact]
    public void TheRewordingBriefKeepsTheReasonAndNeverPresentsAnOrder()
    {
        var brief = FlavourBriefs.For(
            new Announcement(AdventureCallout.KeyPrefix + "the-lantern-route", "The log is closed. Next: dock at Maren Anchorage. A reader there knows the burst."),
            personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.Contains("with the reason and its source when the text gives one", brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("never present the next objective as an order you were given", brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("\"I'm told\"", brief.Instruction, StringComparison.Ordinal);
    }
}
