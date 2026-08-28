using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// The two halves of the Commander's own account of themselves, and which calls carry which
/// (Phase 43, "The sheet always, the story sometimes").
/// </summary>
public class CommanderStoryTests
{
    [Fact]
    public void TheSheetComesFirstAndTheStoryFollows()
    {
        var text = CommanderStory.Compose("Sheet.", "Story.", withStory: true);

        Assert.Equal("Sheet.\n\nStory.", text);
    }

    [Fact]
    public void WithoutTheStoryOnlyTheSheetGoes()
    {
        Assert.Equal("Sheet.", CommanderStory.Compose("Sheet.", "Story.", withStory: false));
    }

    /// <summary>
    /// A Commander who has written a story and no sheet still has a story. The split is about
    /// cost, not about making the story conditional on filling in a second box.
    /// </summary>
    [Fact]
    public void AStoryWithNoSheetStillGoesWhenAskedFor()
    {
        Assert.Equal("Story.", CommanderStory.Compose(null, "Story.", withStory: true));
        Assert.Null(CommanderStory.Compose(null, "Story.", withStory: false));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "   ")]
    public void NothingWrittenIsNullRatherThanAHeadingOverNothing(string? sheet, string? story)
    {
        Assert.Null(CommanderStory.Compose(sheet, story, withStory: true));
    }

    /// <summary>
    /// Chosen by the index and nothing else. The same index always gives the same answer, which
    /// is what lets a recorded session replay to the calls it made live.
    /// </summary>
    [Fact]
    public void WhichCallCarriesTheStoryIsDeterministic()
    {
        for (var index = 0; index < 3 * CommanderStory.StoryEvery; index++)
        {
            Assert.Equal(CommanderStory.TellsStory(index), CommanderStory.TellsStory(index));
        }
    }

    /// <summary>
    /// Exactly one call in <see cref="CommanderStory.StoryEvery"/>, so "occasionally" is a number
    /// and the per-call cost is one that can be written down.
    /// </summary>
    [Fact]
    public void TheStoryGoesOneCallInEvery()
    {
        var told = Enumerable.Range(0, CommanderStory.StoryEvery).Count(i => CommanderStory.TellsStory(i));

        Assert.Equal(1, told);

        // And the first remark of a session is one of them — the one where the most is unknown
        // about who is flying.
        Assert.True(CommanderStory.TellsStory(0));
    }

    /// <summary>
    /// A line that was never one of a numbered set has no index to be chosen by, and the sheet
    /// is the default.
    /// </summary>
    [Fact]
    public void ALineWithNoIndexNeverCarriesTheStory()
    {
        Assert.False(CommanderStory.TellsStory(null));
    }
}
