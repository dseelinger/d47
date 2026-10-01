using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>When the fourth finale chapter is done and its clue given, no chapter is written and the story is finished.</summary>
public sealed class AStoryFinishesAfterItsFinaleTests
{
    [Fact]
    public async Task TheFourthFinaleChapterIsTheLast()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var last = fixtures.Stories.Current("F1")!.CurrentChapter!;

        fixtures.Stories.Update("F1", Id, story => story with
        {
            Chapters = ["one", "two", "three", "four", last],
            FinaleFrom = 2,
            CluesGiven = 17,
            BeaconScanAt = Now,
        });

        Assert.Equal(4, fixtures.Stories.Current("F1")!.FinaleChapter);

        fixtures.Finish("F1", last, Now);

        // The last finale clue is still owed, so the story waits for it.
        Assert.Null(fixtures.Director.Tick("F1", Now.AddDays(400)));
        Assert.True(fixtures.Stories.Current("F1")!.IsCurrent);

        var due = fixtures.Director.ClueDue("F1", Now.AddDays(400));

        Assert.Equal(new StoryClueDue(Id, 17), due);
        Assert.Equal((Card.Title, Secret.Finale[3].Text), fixtures.Director.Clue("F1", due!));
        fixtures.Director.ClueGiven("F1", due!);

        Assert.Null(fixtures.Director.Tick("F1", Now.AddDays(400)));

        var finished = fixtures.Stories.Find("F1", Id)!;

        Assert.Equal(StoryState.Finished, finished.State);
        Assert.False(finished.IsCurrent);
        Assert.Equal(HeldCores.None, finished.HeldCores);
        Assert.Null(fixtures.Stories.Current("F1"));
        Assert.Null(fixtures.Director.Tick("F1", Now.AddDays(401)));
        Assert.Single(fixtures.Asks);
    }
}
