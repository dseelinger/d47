using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A story chapter goes through the plain-wording check: an objection is repaired, and a draft still objected to is written again.</summary>
public sealed class AStoryChaptersWordingIsCheckedBeforeItIsShownTests
{
    private const string Plain = """{"objections": []}""";

    private const string LineObjected = """{"objections": [{"part": "objective 2", "quote": "To one name", "kind": "figurative", "reason": "it calls the Commander's goal a name."}]}""";

    private const string PlainAnchorage = """
        {"replacements": [
          {"objective": 2, "title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Dock at Maren Anchorage."}
        ]}
        """;

    private static RoundScriptedLlmProvider Scripted(params string[] replies) =>
        new([.. replies.Select(reply => RoundScriptedLlmProvider.Saying(reply))]) { ScriptsPlainChecks = true };

    [Fact]
    public async Task AnObjectionToAChapterLineIsRepairedBeforeTheChapterIsKept()
    {
        using var fixtures = new StoryFixtures(Scripted(Spine, Plain, BeatsToTheBeacon, NothingObjected, LineObjected, PlainAnchorage, NothingObjected, Plain));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Empty(fixtures.WritingAgain);
        Assert.Equal(8, fixtures.Provider.CallCount);
        Assert.Contains("it calls the Commander's goal a name.", fixtures.Provider.Requests[5].Prompt.History[0].Text);

        var chapter = Assert.Single(fixtures.Stories.Current("F1")!.Chapters);
        Assert.Contains("Dock at Maren Anchorage.", fixtures.Book.Store.Find("F1", chapter)!.Beats[1].Lines.Select(line => line.Text));
    }

    [Fact]
    public async Task AChapterStillObjectedToAfterItsRoundsIsWrittenAgainAndToldWhy()
    {
        var unchanged = Repairs.Replacing(BeatsToTheBeacon, 2);

        using var fixtures = new StoryFixtures(Scripted(
            Spine, Plain, BeatsToTheBeacon,
            NothingObjected, LineObjected, unchanged,
            NothingObjected, LineObjected, unchanged,
            NothingObjected, LineObjected,
            BeatsToTheBeacon.Replace("To one name.", "Dock at Maren Anchorage.", StringComparison.Ordinal), NothingObjected, Plain));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal([StoryDirector.WritingAgainLine(2, told: true)], fixtures.WritingAgain);
        Assert.StartsWith("The story is not written plainly: ", fixtures.Asks[1].LastDraftRefused);
        Assert.Equal(14, fixtures.Provider.CallCount);
    }
}
