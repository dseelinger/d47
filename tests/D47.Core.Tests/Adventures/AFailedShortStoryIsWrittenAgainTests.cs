using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>The chapter written after a failed short story takes the failed one's place in its chain and is told why it failed.</summary>
public sealed class AFailedShortStoryIsWrittenAgainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private const string Spine = """
        {"name": "The Second Column", "premise": "The ledger has a sequel.", "want": "To find who signed.",
         "stake": "Whether a name is a debt.", "turn": "The name was hers.", "ending": "It is paid."}
        """;

    private const string Beats = """
        {"opening": "Somebody is paying again.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "The Column", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    private static Adventure First() => LanternRoute(Accepted) with
    {
        Key = "first",
        Name = "The First Chapter",
        Spine = new AdventureSpine { Premise = "First premise.", Want = "First want.", Turn = "First turn.", Ending = "First ending." },
    };

    private static Adventure Failed() => LanternRoute(Accepted) with
    {
        Key = "second",
        Name = "The Second Chapter",
        Follows = "first",
        Spine = new AdventureSpine { Premise = "Second premise.", Want = "Second want." },
        Ending = new AdventureEnding(AdventureEnding.Failed, Accepted.AddMinutes(4), "At \"The Lantern\" the Commander did what ruled it out: collect one bounty."),
    };

    [Fact]
    public async Task TheNextChapterFollowsWhatCameBeforeTheFailedOneAndCarriesWhyItFailed()
    {
        var all = new[] { First(), Failed() };
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Beats));

        var ask = new AdventureAsk(
            Length: AdventureLength.Short,
            Chapter: AdventureChapter.Of(all, "first"),
            Failed: AdventureChapter.Of(all, "second"));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(ask, Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("first", outcome.Draft!.Follows);

        var prompt = provider.Requests[0].Prompt.History[0].Text;
        Assert.Contains("An earlier attempt at this chapter failed", prompt);
        Assert.Contains("Title: The Second Chapter", prompt);
        Assert.Contains("Why it failed: At \"The Lantern\" the Commander did what ruled it out: collect one bounty.", prompt);
        Assert.Contains("Write a chapter that takes its place", prompt);
        Assert.DoesNotContain("Write the next chapter, not a retelling", prompt);
    }

    [Fact]
    public async Task TheNextChapterAfterAnOptionsEndingIsWrittenFromThatEnding()
    {
        var route = First();
        var ended = route with
        {
            Beats =
            [
                route.Beats[0],
                route.Beats[1] with
                {
                    Steps =
                    [
                        new BeatStep
                        {
                            Trigger = new AdventureTrigger { Kind = TriggerKind.Choice },
                            Options = [new BeatOption { Label = "fight" }, new BeatOption { Label = "flee", Ending = "Gone to ground", EndingLine = "Nobody follows." }],
                        },
                    ],
                },
            ],
            Ending = new AdventureEnding("Gone to ground", Accepted.AddMinutes(4), "At \"The Survey\" the Commander chose to flee and did it."),
        };
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Beats));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short, Chapter: AdventureChapter.Of([ended], "first")), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var prompt = provider.Requests[0].Prompt.History[0].Text;
        Assert.Contains("It ended in the ending \"Gone to ground\", one of its own: At \"The Survey\" the Commander chose to flee and did it. Write the next chapter from that ending.", prompt);
        Assert.Contains("What was said when it ended: Nobody follows.", prompt);
    }

    [Fact]
    public async Task AFailedFirstChapterIsWrittenAgainWithNothingBeforeIt()
    {
        var failed = Failed() with { Follows = null };
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Beats));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short, Failed: AdventureChapter.Of([failed], "second")), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Null(outcome.Draft!.Follows);
        Assert.Contains("An earlier attempt at this chapter failed", provider.Requests[0].Prompt.History[0].Text);
    }
}
