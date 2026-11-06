using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A story chapter goes through the plain-wording check: an objection is repaired, and a draft still objected to is written again.</summary>
public sealed class AStoryChaptersWordingIsCheckedBeforeItIsShownTests
{
    private const string Plain = """{"objections": []}""";

    private const string LineObjected = """{"objections": [{"part": "objective 2", "quote": "another page", "kind": "figurative", "reason": "it calls the Commander's goal a page."}]}""";

    private static RoundScriptedLlmProvider Scripted(params string[] replies) =>
        new([.. replies.Select(reply => RoundScriptedLlmProvider.Saying(reply))]) { ScriptsPlainChecks = true };

    [Fact]
    public async Task AnObjectionToAChapterLineIsRepairedBeforeTheChapterIsKept()
    {
        using var fixtures = new StoryFixtures(Scripted(Lines(), NothingObjected, LineObjected, Rewritten(2, "Land on Ossen's Lantern 2 a.", "ship"), NothingObjected, Plain));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Empty(fixtures.WritingAgain);
        Assert.Equal(6, fixtures.Provider.CallCount);
        Assert.Contains("it calls the Commander's goal a page.", fixtures.Provider.Requests[3].Prompt.History[0].Text);

        var chapter = Assert.Single(fixtures.Stories.Current("F1")!.Chapters);
        Assert.Contains("Land on Ossen's Lantern 2 a.", fixtures.Book.Store.Find("F1", chapter)!.Beats[1].Lines.Select(line => line.Text));
    }

    [Fact]
    public async Task AChapterStillObjectedToAfterItsRoundsIsWrittenAgainAndToldWhy()
    {
        var unchanged = Rewritten(2, "The ledger has another page, the two.", "ship");

        using var fixtures = new StoryFixtures(Scripted(
            Lines(), NothingObjected, LineObjected,
            unchanged, NothingObjected, LineObjected,
            unchanged, NothingObjected, LineObjected,
            Lines(), NothingObjected, Plain));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal([StoryDirector.WritingAgainLine(2, told: true)], fixtures.WritingAgain);
        Assert.StartsWith("The story is not written plainly: ", fixtures.Asks[1].LastDraftRefused);
        Assert.Equal(12, fixtures.Provider.CallCount);
    }
}
