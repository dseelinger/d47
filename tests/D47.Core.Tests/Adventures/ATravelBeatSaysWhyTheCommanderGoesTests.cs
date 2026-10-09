using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>Every arrive, dock, land and scan beat carries a reason, said in the hand-off and given to the core before arrival.</summary>
[Trait("Category", "Integration")]
public sealed class ATravelBeatSaysWhyTheCommanderGoesTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private const string Reason = "A comms specialist there reads signals like the burst.";

    private const string Spine = """
        {"name": "The Unrecoverable Column", "premise": "A ledger will not balance.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The column balances."}
        """;

    private const string GoodBeats = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "The freight's last beacon ping came from here.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "A comms specialist there reads signals like the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-adventure-reason", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task ATravelBeatWithNoReasonGoesBackThroughTheTurn()
    {
        const string noReason = """
            {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
              {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "The freight's last beacon ping came from here.", "system": "Ossen's Lantern", "line": "Scoop here."},
              {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "  ", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
              {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
            ]}
            """;

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(noReason),
            RoundScriptedLlmProvider.Saying(GoodBeats));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(3, provider.CallCount);

        var first = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("\"reason\": string|null", first);
        Assert.Contains("the Commander is told about that person and never meets them", first);
        Assert.Contains("A line never gives the Commander a task.", first);

        var refusals = provider.Requests[2].Prompt.History[0].Text.Split("cannot stand")[1];
        Assert.Contains("Objective 2 (The Anchorage) is a travel objective with no \"reason\";", refusals);
        Assert.DoesNotContain("Objective 1 (The Lantern)", refusals);
        Assert.DoesNotContain("Objective 3", refusals);

        Assert.Equal(Reason, outcome.Draft!.Beats[1].Reason);
        Assert.Null(outcome.Draft.Beats[2].Reason);
    }

    [Fact]
    public void TheHandOffSaysTheReasonAfterThePlace()
    {
        var beat = new AdventureBeat
        {
            Title = "Short Hops",
            Function = "midpoint",
            Reason = Reason,
            Trigger = new AdventureTrigger { Kind = TriggerKind.Dock, MarketId = 1, System = "HIP 97950", Station = "Sellings Holdings" },
            Line = "She heard it too.",
        };

        Assert.Equal($"Next: dock at Sellings Holdings in HIP 97950. {Reason}", beat.HandOff());
    }

    [Fact]
    public void TheCoreIsGivenTheReasonButNotTheArrivalLine()
    {
        var route = LanternRoute(Accepted);
        var adventure = route with { Beats = [.. route.Beats.Select((beat, index) => index == 2 ? beat with { Reason = Reason } : beat)] };
        var standing = WholeRoute(Accepted).Take(2).Aggregate(AdventureFold.Start(adventure), AdventureFold.Apply);

        var block = AdventureContext.Describe([standing], _ => null, Accepted.AddHours(1))!;

        Assert.Contains($"Now: The Anchorage (midpoint): waiting to dock at Maren Anchorage in Dyson's Hollow. Reason: {Reason}", block);
        Assert.DoesNotContain("To one name.", block);
        Assert.Contains("Asked why the Commander is going there, answer from that reason and invent none.", AdventureContext.Label);
    }

    [Fact]
    public void TheReasonSurvivesSaveAndLoad()
    {
        var route = LanternRoute(Accepted);
        Assert.Null(Store().Save("F1", route with { Beats = [.. route.Beats.Select((beat, index) => index == 2 ? beat with { Reason = Reason } : beat)] }));

        var back = Reread();

        Assert.Equal(Reason, back.Beats[2].Reason);
        Assert.Null(back.Beats[0].Reason);
    }

    [Fact]
    public void ABeatStoredWithoutAReasonHandsOffAsBefore()
    {
        var store = Store();
        Assert.Null(store.Save("F1", LanternRoute(Accepted)));
        Assert.DoesNotContain("\"reason\"", File.ReadAllText(store.Path));

        var back = Reread();

        Assert.Null(back.Beats[2].Reason);
        Assert.Equal("Next: dock at Maren Anchorage in Dyson's Hollow.", back.Beats[2].HandOff());
        Assert.EndsWith(
            "Now: The Anchorage (midpoint): waiting to dock at Maren Anchorage in Dyson's Hollow.",
            AdventureContext.Describe(
                [WholeRoute(Accepted).Take(2).Aggregate(AdventureFold.Start(back), AdventureFold.Apply)], _ => null, Accepted.AddHours(1)));
    }

    private AdventureStore Store() => new(Path.Combine(_folder, "adventures.json"), NullLogger<AdventureStore>.Instance);

    private Adventure Reread()
    {
        var store = Store();
        store.Poll();
        Assert.Empty(store.Problems);
        return Assert.Single(store.For("F1"));
    }
}
