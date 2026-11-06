using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A second or later act-one chapter is not given the act-one beats an earlier chapter had.</summary>
public sealed class ALaterActOneChapterSkipsTheEarlierBeatsTests
{
    [Theory]
    [InlineData(true, new[] { "openingImage", "themeStated", "setUp", "catalyst", "debate", "breakIntoTwo" })]
    [InlineData(false, new[] { "openingImage", "themeStated", "setUp", "catalyst", "debate" })]
    public void TheFirstChapterOnTheFullSheetGetsEveryActOneBeat(bool inReach, string[] expected) =>
        Assert.Equal(expected, StoryClues.BeatKeys(StoryPacing.OneYear, StoryStage.ActOne, inReach, null));

    [Theory]
    [InlineData(true, new[] { "debate", "breakIntoTwo" })]
    [InlineData(false, new[] { "debate" })]
    public void ALaterChapterOnTheFullSheetGetsTheBeatsAfterTheCatalyst(bool inReach, string[] expected) =>
        Assert.Equal(expected, StoryClues.BeatKeys(StoryPacing.OneYear, StoryStage.ActOne, inReach, null, laterActOne: true));

    [Theory]
    [InlineData(true, new[] { "openingImage", "catalyst", "breakIntoTwo" })]
    [InlineData(false, new[] { "openingImage", "catalyst" })]
    public void TheFirstChapterOnTheShortSheetGetsEveryActOneBeat(bool inReach, string[] expected) =>
        Assert.Equal(expected, StoryClues.BeatKeys(StoryPacing.ThreeDays, StoryStage.ActOne, inReach, null));

    [Theory]
    [InlineData(true, new[] { "breakIntoTwo" })]
    [InlineData(false, new[] { "catalyst" })]
    public void ALaterChapterOnTheShortSheetGetsTheBeatsAfterTheOpeningOrTheLastOne(bool inReach, string[] expected) =>
        Assert.Equal(expected, StoryClues.BeatKeys(StoryPacing.ThreeDays, StoryStage.ActOne, inReach, null, laterActOne: true));

    [Fact]
    public void LaterStagesAreUnchanged() =>
        Assert.Equal(
            StoryClues.BeatKeys(StoryPacing.OneYear, StoryStage.Midpoint, true, null),
            StoryClues.BeatKeys(StoryPacing.OneYear, StoryStage.Midpoint, true, null, laterActOne: true));
}
