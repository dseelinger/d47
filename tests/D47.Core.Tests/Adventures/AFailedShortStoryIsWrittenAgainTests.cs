using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>The chapter written after a failed short story takes the failed one's place in its chain and is told why it failed.</summary>
public sealed class AFailedShortStoryIsWrittenAgainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static RoundScriptedLlmProvider Provider() =>
        new(RoundScriptedLlmProvider.Saying(PlannedChapterWorld.ShortLines()), RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections));

    private static string Prompt(RoundScriptedLlmProvider provider) =>
        provider.Requests.First(request => request.ResponseSchema?.Name == "story_lines").Prompt.History[0].Text;

    private static Task<AdventureOutcome> Write(RoundScriptedLlmProvider provider, AdventureAsk ask) =>
        PlannedChapterWorld.Generator(provider, PlannedChapterWorld.Roomy()).GenerateAsync(ask with { CrimeRefused = true }, Now, CancellationToken.None);

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
        var provider = Provider();

        var ask = new AdventureAsk(
            Length: AdventureLength.Evening,
            Chapter: AdventureChapter.Of(all, "first"),
            Failed: AdventureChapter.Of(all, "second"));

        var outcome = await Write(provider, ask);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("first", outcome.Draft!.Follows);

        var prompt = Prompt(provider);
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
        var provider = Provider();

        var outcome = await Write(provider, new AdventureAsk(Length: AdventureLength.Evening, Chapter: AdventureChapter.Of([ended], "first")));

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var prompt = Prompt(provider);
        Assert.Contains("It ended in the ending \"Gone to ground\", one of its own: At \"The Survey\" the Commander chose to flee and did it. Write the next chapter from that ending.", prompt);
        Assert.DoesNotContain("Nobody follows.", prompt);
    }

    [Fact]
    public async Task AFailedFirstChapterIsWrittenAgainWithNothingBeforeIt()
    {
        var failed = Failed() with { Follows = null };
        var provider = Provider();

        var outcome = await Write(provider, new AdventureAsk(Length: AdventureLength.Evening, Failed: AdventureChapter.Of([failed], "second")));

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Null(outcome.Draft!.Follows);
        Assert.Contains("An earlier attempt at this chapter failed", Prompt(provider));
    }
}
