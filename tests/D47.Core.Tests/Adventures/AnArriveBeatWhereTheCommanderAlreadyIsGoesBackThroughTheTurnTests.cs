using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>An arrive beat fires only on a jump into its system, so the dry run refuses one in the system the Commander is already in at that point.</summary>
public sealed class AnArriveBeatWhereTheCommanderAlreadyIsGoesBackThroughTheTurnTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private const string Spine = """
        {"name": "The Unrecoverable Column", "premise": "A ledger will not balance.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The column balances."}
        """;

    private const string GoodBeats = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    [Fact]
    public async Task AFirstBeatArrivingWhereTheCommanderStartsIsRefused()
    {
        const string arriveHere = """
            {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
              {"title": "Home Ledger", "function": "setup", "kind": "arrive", "system": "Oppi", "line": "Start here."},
              {"title": "The Anchorage", "function": "turn", "kind": "dock", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
              {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
            ]}
            """;

        var retry = await RetryAfter(arriveHere);

        Assert.Contains("Beat 1 (Home Ledger) arrives at Oppi, but the Commander is already there at that point;", retry);
        Assert.DoesNotContain("Beat 2 (The Anchorage)", retry.Split("cannot stand")[1]);
    }

    [Fact]
    public async Task ALaterBeatArrivingWhereTheLastStopWasIsRefused()
    {
        const string landThenArrive = """
            {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
              {"title": "The Consignee", "function": "setup", "kind": "land", "system": "Ossen's Lantern", "body": "Ossen's Lantern 2 a", "line": "Dust."},
              {"title": "The Lantern", "function": "turn", "kind": "arrive", "system": "Ossen's Lantern", "line": "Scoop here."},
              {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
            ]}
            """;

        var retry = await RetryAfter(landThenArrive);

        Assert.Contains("Beat 2 (The Lantern) arrives at Ossen's Lantern, but the Commander is already there at that point;", retry);
        Assert.DoesNotContain("Beat 1 (The Consignee)", retry.Split("cannot stand")[1]);
    }

    /// <summary>The refusal pass's instruction after the first draft of beats, once the retry has succeeded.</summary>
    private static async Task<string> RetryAfter(string firstDraft)
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(firstDraft),
            RoundScriptedLlmProvider.Saying(GoodBeats));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(3, provider.CallCount);

        return provider.Requests[2].Prompt.History[0].Text;
    }
}
