using D47.Core.Messages;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Messages;

/// <summary>Your story lists the lines waiting for an answer, newest first, and no other line.</summary>
public class YourStoryListsOnlyWaitingLinesTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    private static D47Message Line(int minutes, string? key, IReadOnlyList<MessageAnswer>? answers = null) =>
        new() { Key = $"line{minutes}", Sent = Noon.AddMinutes(minutes), AdventureKey = key, Subject = "Line", Body = "x", Answers = answers ?? [] };

    [Fact]
    public void WaitingLinesRunNewestFirstAndOtherLinesAreNotListed()
    {
        var ending = Line(5, StoryEnding.Key("tide"), [new MessageAnswer("a", "Stay")]);
        var question = Line(6, "solo-hop", [new MessageAnswer("b", "Go")]);
        var log = StoryLog.Of([Line(1, "solo-hop"), ending, question, Line(9, null)], message => message.Answers.Count > 0);

        Assert.Equal([question, ending], log.Waiting);
    }

    [Fact]
    public void NoMessagesListNothing()
    {
        Assert.Empty(StoryLog.Of([], _ => true).Waiting);
    }
}
