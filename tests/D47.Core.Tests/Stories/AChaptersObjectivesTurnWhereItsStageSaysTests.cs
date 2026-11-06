using D47.Core.Adventures;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A planned chapter's functions: setup first, resolution last, the turn at round(0.6 × (count − 1)) + 1 named for the stage, setback after it.</summary>
public sealed class AChaptersObjectivesTurnWhereItsStageSaysTests
{
    public static TheoryData<int, StoryStage> Counts()
    {
        var data = new TheoryData<int, StoryStage>();

        foreach (var count in Enumerable.Range(3, 6))
        {
            foreach (var stage in Enum.GetValues<StoryStage>())
            {
                data.Add(count, stage);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Counts))]
    public void EachCountAndStageHasOneTurnBetweenSetupAndResolution(int count, StoryStage stage)
    {
        var functions = ChapterFunctions.For(count, stage);
        var turn = (int)Math.Round(0.6 * (count - 1), MidpointRounding.AwayFromZero);

        Assert.Equal(count, functions.Count);
        Assert.Equal(ChapterFunctions.Setup, functions[0]);
        Assert.Equal(ChapterFunctions.Resolution, functions[^1]);
        Assert.Equal(ChapterFunctions.TurnAt(stage), functions[turn]);
        Assert.True(AdventureStanding.IsTurning(functions[turn]));
        Assert.Equal(turn + 1 < count - 1 ? ChapterFunctions.Setback : ChapterFunctions.Resolution, functions[turn + 1]);
        Assert.All(functions.Where((_, index) => index > 0 && index < turn), function => Assert.Equal(ChapterFunctions.Complication, function));
        Assert.All(functions.Where((_, index) => index > turn + 1 && index < count - 1), function => Assert.Equal(ChapterFunctions.Complication, function));
    }

    [Theory]
    [InlineData(StoryStage.Midpoint, "midpoint")]
    [InlineData(StoryStage.AllIsLost, "all is lost")]
    [InlineData(StoryStage.FunAndGames, "turn")]
    [InlineData(StoryStage.BadGuysCloseIn, "turn")]
    public void TheTurnIsNamedForTheStage(StoryStage stage, string turn) =>
        Assert.Equal(turn, ChapterFunctions.TurnAt(stage));

    [Fact]
    public void FiveObjectivesFollowTheEveningSheet() =>
        Assert.Equal("setup, complication, turn, setback, resolution", string.Join(", ", ChapterFunctions.For(5, StoryStage.FunAndGames)));

    [Fact]
    public void ThreeObjectivesFollowTheShortSheet() =>
        Assert.Equal("setup, turn, resolution", string.Join(", ", ChapterFunctions.For(3, StoryStage.FunAndGames)));
}
