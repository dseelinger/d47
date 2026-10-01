using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class ACardNamesTheLevelItWasWrittenForTests
{
    [Theory]
    [InlineData("new", "New commander", "For a new commander: no engineering done yet.")]
    [InlineData("midrange", "Mid-range commander", "For a mid-range commander: some ship engineering done.")]
    [InlineData("endgame", "Endgame commander", "For an endgame commander: most ship and on-foot engineers unlocked, and at least one ship, suit and weapon fully engineered.")]
    public void EachLevelHasANameAndAGuideline(string level, string name, string guideline)
    {
        var card = StoryFixtures.Card with { Level = level };

        Assert.Equal(name, card.LevelName);
        Assert.Equal(guideline, card.LevelGuideline);
        Assert.Contains(guideline, card.Describe());
    }

    [Fact]
    public void EveryShippedStoryNamesALevel() =>
        Assert.All(StoryCatalog.Default.Cards, card => Assert.Contains(card.Level, StoryCard.Levels));
}
