using D47.Core.Adventures;
using D47.Core.Storage;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>Every objective except a choice carries a reason, said in the hand-off and given to the core before arrival.</summary>
[Trait("Category", "Integration")]
public sealed class ATravelBeatSaysWhyTheCommanderGoesTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private const string Reason = "A comms specialist there reads signals like the burst.";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-adventure-reason", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task AnObjectiveWithNoReasonGoesBackToTheWriterForItsWords()
    {
        var ask = Stories.PlannedChapterWorld.Ask();
        var plan = await Stories.PlannedChapterWorld.PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Stories.PlannedChapterWorld.Lines(plan.Objectives.Count, (number, objective) =>
            {
                if (number == 2)
                {
                    objective["reason"] = "  ";
                }
            })),
            RoundScriptedLlmProvider.Saying(Stories.PlannedChapterWorld.Repair(2)),
            RoundScriptedLlmProvider.Saying(Stories.PlannedChapterWorld.NoObjections));

        var outcome = await Stories.PlannedChapterWorld.Generator(provider).GenerateAsync(ask, Now, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("story_line_repairs", provider.Requests[1].ResponseSchema?.Name);

        var refusals = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("Objective 2", refusals);
        Assert.Contains("has no \"reason\"", refusals);
        Assert.Equal("The broker's ledger points the Commander onward to part two again.", outcome.Draft!.Beats[1].Reason);
    }

    [Fact]
    public void TheHandOffSaysTheReasonBeforeThePlace()
    {
        var beat = new AdventureBeat
        {
            Title = "Short Hops",
            Function = "midpoint",
            Reason = Reason,
            Trigger = new AdventureTrigger { Kind = TriggerKind.Dock, MarketId = 1, System = "HIP 97950", Station = "Sellings Holdings" },
            Line = "She heard it too.",
        };

        Assert.Equal($"{Reason} Next: dock at Sellings Holdings in HIP 97950.", beat.HandOff());
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
        Assert.Contains(
            "Now: The Anchorage (midpoint): waiting to dock at Maren Anchorage in Dyson's Hollow." + Environment.NewLine,
            AdventureContext.Describe(
                [WholeRoute(Accepted).Take(2).Aggregate(AdventureFold.Start(back), AdventureFold.Apply)], _ => null, Accepted.AddHours(1)));
    }

    private AdventureStore Store() => new(Path.Combine(_folder, "adventures.json"), new DiskFileSystem(), NullLogger<AdventureStore>.Instance);

    private Adventure Reread()
    {
        var store = Store();
        store.Poll();
        Assert.Empty(store.Problems);
        return Assert.Single(store.For("F1"));
    }
}
