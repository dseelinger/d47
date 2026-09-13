using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A word swapped for one it means the same as still scores as the phrase said.</summary>
public class SpellingsThatMeanTheSameThingScoreEquivalentTests
{
    [Fact]
    public void SetFocusOnEliteIsEquivalentToSetFocusToElite() =>
        Assert.Equal(PhraseMatch.Equivalent, PhraseScore.Score("set focus on elite", "set focus to elite"));

    [Fact]
    public void PuttingTheGearDownsIsEquivalentToPuttingTheGearDown() =>
        Assert.Equal(PhraseMatch.Equivalent, PhraseScore.Score("put the gear downs", "put the gear down"));

    [Theory]
    [InlineData("lights on")]
    [InlineData("lights off")]
    public void LightsOfIsNotEquivalentToLightsOnOrOff(string phrase) =>
        Assert.NotEqual(PhraseMatch.Equivalent, PhraseScore.Score("lights of", phrase));
}
