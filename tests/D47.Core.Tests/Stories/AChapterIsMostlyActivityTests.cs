using D47.Core.Adventures;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>From chapter two on, no more than two of a story chapter's beats are arrive, dock, land or scan.</summary>
public sealed class AChapterIsMostlyActivityTests
{
    private const string ThreeTravelBeats = """
        {"opening": "Again.", "reply": "Here.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Back."},
          {"title": "The Anchorage", "function": "catalyst", "kind": "dock", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Home."},
          {"title": "The Bounties", "function": "midpoint", "kind": "bounty", "count": 3, "line": "Paid."},
          {"title": "The Lantern Again", "function": "all is lost", "kind": "arrive", "system": "Ossen's Lantern", "line": "Back again."},
          {"title": "The Ore", "function": "finale", "kind": "mine", "count": 20, "line": "Refined."}
        ]}
        """;

    [Fact]
    public async Task AChapterAfterTheFirstWithThreeTravelBeatsIsRefused()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(ThreeTravelBeats), RoundScriptedLlmProvider.Saying(ThreeTravelBeats));

        var outcome = await Generate(provider, chapter: 2);

        Assert.Contains("The chapter has 3 travel beats (arrive, dock, land or scan); from chapter two on, no more than 2 may be.", outcome.Refusal);
        Assert.Contains("no more than two of the chapter's beats may be", provider.Requests[1].Prompt.History[0].Text);
    }

    [Fact]
    public async Task ChapterOneMayTravel()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(ThreeTravelBeats));

        var outcome = await Generate(provider, chapter: 1);

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    private static Task<AdventureOutcome> Generate(RoundScriptedLlmProvider provider, int chapter) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy()).GenerateAsync(
            new AdventureAsk(AdventureReach.Session, Story: new AdventureStory(Id, Card.Title, Card.Describe(), Secret.Secret, chapter, 10, 5)),
            Now,
            CancellationToken.None);
}
