using D47.Core.Adventures;
using D47.Core.Knowledge;
using D47.Core.Journal;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A story chapter may go anywhere only with 250,000,000 credits at the last load or a Caspian Explorer, in a story of three months or more.</summary>
public sealed class ALongHaulNeedsTheCreditsTests
{
    private static readonly NotablePlace Colonia = new("The Far Spire", "Landmark", "Fixture Far", 99, new StarPosition(20100, 0, 0));

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
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Lines())),
            card: Card with { Length = length });
        fixtures.Director.Game = () => Loaded(credits, ship);

        await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.Equal(expected, Assert.Single(fixtures.Asks).Reach);
        Assert.Equal(expected == AdventureReach.Anywhere, fixtures.Asks[0].Story!.LongHaul);
    }

    [Fact]
    public async Task AHopToColoniaPassesOnALongHaul()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Lines()), RoundScriptedLlmProvider.Saying(NothingObjected));

        var outcome = await PlannedChapterWorld.Generator(provider)
            .GenerateAsync(new AdventureAsk(AdventureReach.Anywhere, AdventureLength.Short, Story: Story(longHaul: true)), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("Fixture Far", outcome.Draft!.Sight);
    }

    [Fact]
    public async Task BelowTheThresholdColoniaIsRefused()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Lines()));

        var outcome = await PlannedChapterWorld.Generator(provider)
            .GenerateAsync(new AdventureAsk(AdventureReach.Session, AdventureLength.Short, Story: Story(longHaul: false)), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("could not be planned", outcome.Refusal);
        Assert.Equal(0, provider.CallCount);
    }

    private static AdventureStory Story(bool longHaul) =>
        PlannedChapterWorld.Story with { LongHaul = longHaul, Sights = [Colonia] };
}
