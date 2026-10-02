using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A story chapter's brief names the genre's three elements and the size of the chapter.</summary>
public sealed class AChapterIsWrittenToItsGenreTests
{
    [Fact]
    public async Task ABuddyLoveBriefNamesItsThreeElements()
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)),
            card: Card with { Genre = "Buddy Love" });

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var brief = fixtures.Provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains("The story's genre is Buddy Love, and every chapter keeps its three elements in play: an incomplete hero, a counterpart, a complication.", brief);
        Assert.Contains("A chapter may offer hiring a crew member (a \"crew\" beat) as part of it, but never requires it.", brief);
    }

    [Fact]
    public async Task TheBriefStatesTheChapterSizeAndCarriesOnAnUnfinishedUndertaking()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var brief = fixtures.Provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains("Size this chapter so the Commander can finish it in one to three play sessions.", brief);
        Assert.Contains("when the chapter before left one unfinished, carry it on in this one.", brief);
        Assert.Contains("Whydunit, and every chapter keeps its three elements in play: a detective, a secret, a dark turn.", brief);
    }

    [Fact]
    public async Task AStoryShorterThanThreeMonthsHasOneSessionChapters()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(NextSpine), RoundScriptedLlmProvider.Saying(NextBeats)), card: Card with { Length = "2-weeks" });

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Contains("Size this chapter so the Commander can finish it in one play session.", fixtures.Provider.Requests[0].Prompt.History[0].Text);
    }

    [Fact]
    public void EveryGenreHasThreeElements()
    {
        Assert.Equal(StoryCard.Genres.Order(), ChapterFit.Elements.Keys.Order());
        Assert.All(ChapterFit.Elements.Values, elements => Assert.Equal(3, elements.Count));
    }
}
