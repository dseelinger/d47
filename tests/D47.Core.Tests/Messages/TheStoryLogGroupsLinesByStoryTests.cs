using D47.Core.Messages;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Messages;

public class TheStoryLogGroupsLinesByStoryTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, string> Names = new()
    {
        ["story.tide"] = "The Long Tide",
        ["story.ember"] = "Ember Run",
        ["chapter-one"] = "The Long Tide",
        ["solo-hop"] = "A Quick Hop",
    };

    private static D47Message Line(int minutes, string? key, string subject = "Line", IReadOnlyList<MessageAnswer>? answers = null) =>
        new() { Key = $"{subject}{minutes}", Sent = Noon.AddMinutes(minutes), AdventureKey = key, Subject = subject, Body = "x", Answers = answers ?? [] };

    private static StoryLog Log(IEnumerable<D47Message> messages, Func<D47Message, bool>? waiting = null) =>
        StoryLog.Of(messages, key => Names.GetValueOrDefault(key), waiting ?? (_ => false));

    [Fact]
    public void AStoryLineEndingClueAndReactionKeyAllLandInTheStockStory()
    {
        var log = Log([
            Line(1, StoryLines.Key("tide")),
            Line(2, StoryEnding.Key("tide")),
            Line(3, "story.clue.tide.4"),
            Line(4, StoryReactionCallout.Key(new StoryReactionDue("tide", 2), "ren"))]);

        var group = Assert.Single(log.Groups);
        Assert.Equal("The Long Tide", group.Name);
        Assert.Equal(4, group.Messages.Count);
    }

    [Fact]
    public void AChapterOfAStockStoryJoinsThatStoryAndAShortStoryIsItsOwnGroup()
    {
        var log = Log([Line(1, "chapter-one"), Line(2, "story.clue.tide.0"), Line(3, "solo-hop")]);

        Assert.Equal(["A Quick Hop", "The Long Tide"], log.Groups.Select(group => group.Name));
        Assert.Equal(2, log.Groups[1].Messages.Count);
    }

    [Fact]
    public void GroupsRunByTheirNewestLineAndLinesRunNewestFirst()
    {
        var log = Log([
            Line(1, "story.clue.tide.0"),
            Line(5, "story.clue.ember.0"),
            Line(9, "story.clue.tide.1")]);

        Assert.Equal(["The Long Tide", "Ember Run"], log.Groups.Select(group => group.Name));
        Assert.Equal([9, 1], log.Groups[0].Messages.Select(message => (int)(message.Sent - Noon).TotalMinutes));
    }

    [Fact]
    public void AKeyThatNoLongerResolvesIsEarlierStoriesAndNoKeyIsLast()
    {
        var log = Log([Line(9, null), Line(8, "gone"), Line(1, "solo-hop")]);

        Assert.Equal([StoryLog.EarlierStories, "A Quick Hop", StoryLog.NotTiedToAStory], log.Groups.Select(group => group.Name));
    }

    [Fact]
    public void LinesWaitingForAnAnswerComeOutOfTheirGroup()
    {
        var ending = Line(5, StoryEnding.Key("tide"), "Ending", [new MessageAnswer("a", "Stay")]);
        var log = Log([Line(1, StoryLines.Key("tide")), ending], message => message.Answers.Count > 0);

        Assert.Equal(ending, Assert.Single(log.Waiting));
        Assert.Single(Assert.Single(log.Groups).Messages);
    }

    [Fact]
    public void ThereAreNoGroupsWithoutMessages()
    {
        var log = Log([]);

        Assert.Empty(log.Waiting);
        Assert.Empty(log.Groups);
    }
}
