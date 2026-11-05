using D47.Core.Messages;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Messages;

/// <summary>Your story lists lines waiting for an answer, then every line no stock story run keeps, both newest first.</summary>
public class YourStoryListsWaitingThenOtherLinesTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    private static readonly HashSet<string> RunKeys = [StoryLines.Key("tide"), StoryEnding.Key("tide"), "chapter-one"];

    private static D47Message Line(int minutes, string? key, IReadOnlyList<MessageAnswer>? answers = null) =>
        new() { Key = $"line{minutes}", Sent = Noon.AddMinutes(minutes), AdventureKey = key, Subject = "Line", Body = "x", Answers = answers ?? [] };

    private static StoryLog Log(IEnumerable<D47Message> messages, Func<D47Message, bool>? waiting = null) =>
        StoryLog.Of(messages, message => message.AdventureKey is { } key && RunKeys.Contains(key), waiting ?? (_ => false));

    [Fact]
    public void LinesARunKeepsAreLeftForItsPage()
    {
        var log = Log([Line(1, StoryLines.Key("tide")), Line(2, "chapter-one"), Line(3, "solo-hop")]);

        Assert.Equal(["line3"], log.Other.Select(message => message.Key));
    }

    [Fact]
    public void ShortStoryUntiedAndUnresolvedLinesRunNewestFirst()
    {
        var log = Log([Line(1, "solo-hop"), Line(9, null), Line(5, StoryLines.Key("a-story-gone"))]);

        Assert.Equal(["line9", "line5", "line1"], log.Other.Select(message => message.Key));
    }

    [Fact]
    public void AWaitingLineIsListedFirstAndOnlyThere()
    {
        var ending = Line(5, StoryEnding.Key("tide"), [new MessageAnswer("a", "Stay")]);
        var question = Line(6, "solo-hop", [new MessageAnswer("b", "Go")]);
        var log = Log([Line(1, "solo-hop"), ending, question], message => message.Answers.Count > 0);

        Assert.Equal([question, ending], log.Waiting);
        Assert.Equal(["line1"], log.Other.Select(message => message.Key));
    }

    [Fact]
    public void NoMessagesListNothing()
    {
        var log = Log([]);

        Assert.Empty(log.Waiting);
        Assert.Empty(log.Other);
    }
}
