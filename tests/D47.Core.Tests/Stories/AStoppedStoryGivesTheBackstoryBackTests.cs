using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Abandon and finishing put the Backstory back to what the Commander had before their first story.</summary>
[Trait("Category", "Integration")]
public sealed class AStoppedStoryGivesTheBackstoryBackTests
{
    private static StoryFixtures Fixtures(int stories = 1) => new(new RoundScriptedLlmProvider(
        [.. Enumerable.Range(0, stories).SelectMany(_ => new[]
        {
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
            RoundScriptedLlmProvider.Saying(NothingObjected),
        })]));

    [Fact]
    public async Task AbandoningRestoresTheEarlierBackstory()
    {
        using var fixtures = Fixtures();
        fixtures.Backstory = "X";

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Equal(Card.InYourWords, fixtures.Backstory);

        Assert.Null(fixtures.Director.Abandon("F1", Now.AddDays(1)));
        Assert.Equal("X", fixtures.Backstory);
    }

    [Fact]
    public async Task EditsMadeWhileTheStoryRanAreDiscarded()
    {
        using var fixtures = Fixtures();
        fixtures.Backstory = "X";

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Backstory = "Y";

        Assert.Null(fixtures.Director.Abandon("F1", Now.AddDays(1)));
        Assert.Equal("X", fixtures.Backstory);
    }

    [Fact]
    public async Task NoEarlierBackstoryRestoresEmpty()
    {
        using var fixtures = Fixtures();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Null(fixtures.Director.Abandon("F1", Now.AddDays(1)));

        Assert.Equal("", fixtures.Backstory);
    }

    [Fact]
    public async Task SwitchingCarriesTheEarlierBackstoryToTheNewStory()
    {
        using var fixtures = Fixtures(2);
        fixtures.Backstory = "X";

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Null(await fixtures.Director.SwitchAsync("F1", Other.Id, Now.AddDays(1), CancellationToken.None));
        Assert.Equal(Other.InYourWords, fixtures.Backstory);

        Assert.Null(fixtures.Director.Abandon("F1", Now.AddDays(2)));
        Assert.Equal("X", fixtures.Backstory);
    }

    [Fact]
    public async Task AStoryPickedBeforeTheBackstoryWasKeptRestoresEmpty()
    {
        using var fixtures = Fixtures();
        fixtures.Backstory = "X";

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Stories.Update("F1", Id, story => story with { BackstoryBefore = null });

        Assert.Null(fixtures.Director.Abandon("F1", Now.AddDays(1)));
        Assert.Equal("", fixtures.Backstory);
    }

    [Fact]
    public async Task ABackstoryThatIsAStoryCardsWordsCountsAsNone()
    {
        using var fixtures = Fixtures();
        fixtures.Backstory = Other.InYourWords;

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Null(fixtures.Director.Abandon("F1", Now.AddDays(1)));

        Assert.Equal("", fixtures.Backstory);
    }

    [Fact]
    public async Task FinishingRestoresTheEarlierBackstory()
    {
        using var fixtures = Fixtures();
        fixtures.Backstory = "X";

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        var last = fixtures.Stories.Current("F1")!.CurrentChapter!;

        fixtures.Stories.Update("F1", Id, story => story with
        {
            Chapters = ["one", "two", "three", "four", last],
            FinaleFrom = 2,
            CluesGiven = 18,
            BeaconScanAt = Now,
        });
        fixtures.Finish("F1", last, Now);

        var archiving = fixtures.Director.Tick("F1", Now.AddDays(400));
        Assert.NotNull(archiving);
        Assert.Null(await archiving);

        Assert.Equal(StoryState.Finished, fixtures.Stories.Find("F1", Id)!.State);
        Assert.Equal("X", fixtures.Backstory);
    }
}
