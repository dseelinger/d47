using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A finished chapter reads "End of chapter.", and only the last chapter of a finished story reads "The end.".</summary>
public sealed class AFinishedChapterSaysWhichEndItIsTests
{
    private static Story Picked(StoryState state) => new()
    {
        Id = "relay-sheet",
        Title = "The Relay Sheet",
        PublicLayer = "A courier's file goes missing.",
        Chapters = ["relay-1", "relay-2", "relay-3"],
        State = state,
    };

    [Fact]
    public void TheLastChapterOfAFinishedStoryIsTheEnd() =>
        Assert.Equal("The end.", ChapterEnd.Line(Picked(StoryState.Finished), "relay-3"));

    [Fact]
    public void AnEarlierChapterOfAFinishedStoryIsTheEndOfAChapter() =>
        Assert.Equal("End of chapter.", ChapterEnd.Line(Picked(StoryState.Finished), "relay-2"));

    [Theory]
    [InlineData(StoryState.Running)]
    [InlineData(StoryState.Paused)]
    [InlineData(StoryState.Ended)]
    [InlineData(StoryState.Abandoned)]
    public void TheLastChapterOfAStoryThatDidNotFinishIsTheEndOfAChapter(StoryState state) =>
        Assert.Equal("End of chapter.", ChapterEnd.Line(Picked(state), "relay-3"));

    [Fact]
    public void AShortStoryChapterIsTheEndOfAChapter() =>
        Assert.Equal("End of chapter.", ChapterEnd.Line(null, "short-1"));
}
