using D47.Core.Adventures;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A story chapter may go anywhere only with 250,000,000 credits at the last load or a Caspian Explorer, in a story of three months or more.</summary>
public sealed class ALongHaulNeedsTheCreditsTests
{
    private const string BeatsToColonia = """
        {"opening": "Far.", "reply": "Here.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "Where The Freight Went", "function": "turn", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Colonia", "line": "Twenty-two thousand light years."},
          {"title": "The Bounties", "function": "resolution", "kind": "bounty", "count": 3, "line": "Paid."}
        ]}
        """;

    [Trait("Category", "Integration")]
    [Theory]
    [InlineData(300_000_000, "sidewinder", "1-year", AdventureReach.Anywhere)]
    [InlineData(250_000_000, "sidewinder", "3-months", AdventureReach.Anywhere)]
    [InlineData(249_999_999, "sidewinder", "1-year", AdventureReach.Session)]
    [InlineData(1_000_000, "explorer_nx", "1-year", AdventureReach.Anywhere)]
    [InlineData(300_000_000, "sidewinder", "1-month", AdventureReach.Session)]
    public async Task TheReachFollowsTheCreditsTheHullAndTheLength(long credits, string ship, string length, AdventureReach expected)
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)),
            card: Card with { Length = length });
        fixtures.Director.Game = () => Loaded(credits, ship);

        await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.Equal(expected, Assert.Single(fixtures.Asks).Reach);
        Assert.Equal(expected == AdventureReach.Anywhere, fixtures.Asks[0].Story!.LongHaul);
    }

    [Fact]
    public async Task AHopToColoniaPassesOnALongHaul()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToColonia));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(AdventureReach.Anywhere, AdventureLength.Short, Story: Story(longHaul: true)), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains("this chapter may go anywhere in the galaxy, as far as Colonia or Sagittarius A*", provider.Requests[0].Prompt.History[0].Text);
    }

    [Fact]
    public async Task BelowTheThresholdTheWriterIsNotToldAndColoniaIsRefused()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToColonia), RoundScriptedLlmProvider.Saying(BeatsToColonia));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(AdventureReach.Session, AdventureLength.Short, Story: Story(longHaul: false)), Now, CancellationToken.None);

        Assert.Contains("Objective 2 (Where The Freight Went) is 21886 light years from the previous stop", outcome.Refusal);
        Assert.DoesNotContain("Colonia or Sagittarius", provider.Requests[0].Prompt.History[0].Text);
    }

    private static AdventureStory Story(bool longHaul) =>
        new(Id, Card.Title, Card.Describe(), Secret.Secret, 1, 10, 5, LongHaul: longHaul);
}
