using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A story chapter's brief names the genre's three elements and the size of the chapter.</summary>
public sealed class AChapterIsWrittenToItsGenreTests
{
    [Trait("Category", "Integration")]
    [Fact]
    public async Task ABuddyLoveBriefNamesItsThreeElements()
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Lines())),
            card: Card with { Genre = "Buddy Love" });

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var brief = fixtures.Provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains("The story's genre is Buddy Love, and every chapter keeps its three elements in play: an incomplete hero, a counterpart, a complication.", brief);
        Assert.Contains("A chapter may offer hiring a crew member (a \"crew\" objective) as part of it, but never requires it.", brief);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheAskStatesTheChapterSizeAndTheBriefNamesTheGenresElements()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Lines())));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var brief = fixtures.Provider.Requests[0].Prompt.History[0].Text;

        Assert.Equal("one to three play sessions", fixtures.Asks[0].Story!.ChapterSize);
        Assert.Contains("Whydunit, and every chapter keeps its three elements in play: a detective, a secret, a dark turn.", brief);
    }

    [Fact]
    public void EveryGenreHasThreeElements()
    {
        Assert.Equal(StoryCard.Genres.Order(), ChapterFit.Elements.Keys.Order());
        Assert.All(ChapterFit.Elements.Values, elements => Assert.Equal(3, elements.Count));
    }
}
