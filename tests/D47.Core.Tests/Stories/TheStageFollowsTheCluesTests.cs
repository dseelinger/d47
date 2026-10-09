using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A story's stage follows the clues given, and the chapter writer gets that stage's beat-sheet lines and none later.</summary>
public sealed class TheStageFollowsTheCluesTests
{
    private static readonly Story Scanned = new()
    {
        Id = Id,
        Title = Card.Title,
        PublicLayer = Card.Describe(),
        PickedAt = Now,
        BeaconScanAt = Now,
    };

    [Theory]
    [InlineData(0, StoryStage.BreakIntoTwo)]
    [InlineData(1, StoryStage.FunAndGames)]
    [InlineData(8, StoryStage.FunAndGames)]
    [InlineData(9, StoryStage.Midpoint)]
    [InlineData(10, StoryStage.BadGuysCloseIn)]
    [InlineData(12, StoryStage.BadGuysCloseIn)]
    [InlineData(13, StoryStage.AllIsLost)]
    [InlineData(14, StoryStage.DarkNightOfTheSoul)]
    public void TheStageIsTheCluesGiven(int given, StoryStage stage) =>
        Assert.Equal(stage, StoryClues.Stage(Scanned with { CluesGiven = given }));

    [Fact]
    public void BeforeTheScanTheStoryIsInActOne() =>
        Assert.Equal(StoryStage.ActOne, StoryClues.Stage(Scanned with { BeaconScanAt = null }));

    [Fact]
    public void EachStageGetsItsOwnLines()
    {
        Assert.Equal(["breakIntoTwo", "bStory"], Keys(StoryStage.BreakIntoTwo));
        Assert.Equal(["bStory", "funAndGames"], Keys(StoryStage.FunAndGames));
        Assert.Equal(["midpoint"], Keys(StoryStage.Midpoint));
        Assert.Equal(["badGuysCloseIn"], Keys(StoryStage.BadGuysCloseIn));
        Assert.Equal(["allIsLost"], Keys(StoryStage.AllIsLost));
        Assert.Equal(["darkNightOfTheSoul"], Keys(StoryStage.DarkNightOfTheSoul));
        Assert.Equal(["openingImage", "themeStated", "setUp", "catalyst", "debate", "breakIntoTwo"], Keys(StoryStage.ActOne));
        Assert.Equal(["openingImage", "themeStated", "setUp", "catalyst", "debate"], Keys(StoryStage.ActOne, inReach: false));
        Assert.Equal(["breakIntoThree", "finale"], Keys(StoryStage.Finale, finale: 1));
        Assert.Equal(["breakIntoThree", "finale", "finalImage"], Keys(StoryStage.Finale, finale: 4));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheWriterIsToldTheStageAndOnlyItsLines()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
            RoundScriptedLlmProvider.Saying(NextSpine),
            RoundScriptedLlmProvider.Saying(NextBeats)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Finish("F1", fixtures.Stories.Current("F1")!.CurrentChapter!, Now);
        fixtures.Stories.Update("F1", Id, story => story with { CluesGiven = 9 });

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(200), CancellationToken.None));

        var brief = fixtures.Provider.Requests[2].Prompt.History[0].Text;

        Assert.Contains("The story stands at Midpoint.", brief);
        Assert.Contains($"- midpoint: {Secret.Beats.Midpoint}", brief);

        foreach (var later in new[] { Secret.Beats.BadGuysCloseIn, Secret.Beats.AllIsLost, Secret.Beats.DarkNightOfTheSoul, Secret.Beats.BreakIntoThree, Secret.Beats.Finale, Secret.Beats.FinalImage })
        {
            Assert.DoesNotContain(later!, brief);
        }

        Assert.DoesNotContain("months or years", brief);
    }

    private static string[] Keys(StoryStage stage, bool inReach = true, int? finale = null) =>
        [.. StoryClues.Beats(Secret.Beats, StoryPacing.OneYear, stage, inReach, finale).Select(beat => beat.Key)];
}
