using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>The finale begins with the first chapter written after all fourteen clues and day 360, and its clue is due as it begins.</summary>
public sealed class TheFinaleWaitsForAYearTests
{
    private static readonly Story AllGiven = new()
    {
        Id = Id,
        Title = Card.Title,
        PublicLayer = Card.Describe(),
        PickedAt = Now,
        BeaconScanAt = Now,
        CluesGiven = 14,
    };

    [Fact]
    public void TheFinaleWaitsForDay360EvenWithEveryClueGiven()
    {
        Assert.False(StoryClues.FinaleDue(AllGiven, Now.AddDays(359)));
        Assert.True(StoryClues.FinaleDue(AllGiven, Now.AddDays(360)));
    }

    [Fact]
    public void TheFinaleWaitsForEveryClue() =>
        Assert.False(StoryClues.FinaleDue(AllGiven with { CluesGiven = 13 }, Now.AddDays(400)));

    [Fact]
    public async Task TheFirstChapterAfterTheYearOpensTheFinale()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
            RoundScriptedLlmProvider.Saying(NextSpine),
            RoundScriptedLlmProvider.Saying(NextBeats)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Finish("F1", fixtures.Stories.Current("F1")!.CurrentChapter!, Now);
        fixtures.Stories.Update("F1", Id, story => story with { CluesGiven = 14, ClueSession = 0, ClueChapter = 1 });

        var later = Now.AddDays(361);

        Assert.Null(fixtures.Director.ClueDue("F1", later));
        Assert.Null(await fixtures.Director.WriteNextAsync("F1", later, CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;

        Assert.Equal(2, story.FinaleFrom);
        Assert.Equal(1, story.FinaleChapter);
        Assert.Equal(1, fixtures.Asks[^1].Story!.FinaleChapter);
        Assert.Contains("this is finale chapter 1 of 4.", fixtures.Provider.Requests[2].Prompt.History[0].Text);

        // Due as the chapter begins, with no new session.
        var due = fixtures.Director.ClueDue("F1", later);

        Assert.Equal(new StoryClueDue(Id, 14), due);
        Assert.Equal((Card.Title, Secret.Finale[0].Text), fixtures.Director.Clue("F1", due!));

        fixtures.Director.ClueGiven("F1", due!);

        Assert.Null(fixtures.Director.ClueDue("F1", later));
        Assert.Contains(Secret.Finale[0].Text, fixtures.Director.HiddenBrief("F1")!);
    }
}
