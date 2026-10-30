using System.Globalization;
using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Callouts;

public sealed class ACalloutKeyReadsBackWhatWasWrittenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);

    private static AdventureMoment Moment(string key, int beat, int? step = null) =>
        new("F1", new Adventure { Key = key, Name = "Name" }, beat, Now, Step: step);

    [Fact]
    public void AClueKeyParsesBackAndKeepsADotInTheStoryId()
    {
        var key = StoryClueCallout.Key(new StoryClueDue("story.v2", 7));

        Assert.Equal("story.clue.story.v2.7", key);
        Assert.Equal(new StoryClueDue("story.v2", 7), StoryClueCallout.Parse(key));
    }

    [Fact]
    public void AReactionKeyParsesBackWithAndWithoutItsSpeaker()
    {
        var due = new StoryReactionDue("story.v2", 3);

        Assert.Equal((due, (string?)null), StoryReactionCallout.Parse(StoryReactionCallout.Key(due)));
        Assert.Equal((due, (string?)"cast1"), StoryReactionCallout.Parse(StoryReactionCallout.Key(due, "cast1")));
    }

    [Fact]
    public void ABeatKeyParsesBackThroughReachedAndSpoken()
    {
        var key = Moment("mine.one", 4).Key;

        Assert.Equal("adventure.mine.one.4", key);
        Assert.Equal(("mine.one", 4), AdventureCallout.Reached(key));
        Assert.Equal(("mine.one", 4, 0), AdventureCallout.Spoken(key));
    }

    [Fact]
    public void AnOpeningKeyParsesBackAsBeatMinusOne()
    {
        var key = Moment("mine.one", -1).Key;

        Assert.Equal("adventure.mine.one.opening", key);
        Assert.Equal(("mine.one", -1), AdventureCallout.Reached(key));
    }

    [Fact]
    public void ALaterLineKeyParsesBackThroughSpoken()
    {
        var key = Moment("mine.one", 4).LineKey(2);

        Assert.Equal("adventure-line.mine.one.4.2", key);
        Assert.Equal(("mine.one", 4, 2), AdventureCallout.Spoken(key));
    }

    [Fact]
    public void AStepKeyAndAnOfferKeyAreWrittenAsSaved()
    {
        Assert.Equal("adventure-step.mine.one.4.1", Moment("mine.one", 4, step: 1).Key);

        var offer = AdventureCallout.OfferKey("mine.one", 4);

        Assert.Equal("adventure-offer.mine.one.4", offer);
        Assert.Equal(("mine.one", 4), AdventureCallout.Offer(offer));
    }

    [Fact]
    public void KeysSavedBeforeStillParse()
    {
        Assert.Equal(new StoryClueDue("s.1", 0), StoryClueCallout.Parse("story.clue.s.1.0"));
        Assert.Equal(("a.b", 12), AdventureCallout.Reached("adventure.a.b.12"));
        Assert.Equal(("a.b", 2, 5), AdventureCallout.Spoken("adventure-line.a.b.2.5"));
        Assert.Equal(("a.b", 2), AdventureCallout.Offer("adventure-offer.a.b.2"));
    }

    [Fact]
    public void EachCallerKeepsItsOwnNumberStyle()
    {
        Assert.Null(StoryClueCallout.Parse("story.clue.s. 3"));
        Assert.Null(StoryClueCallout.Parse("story.clue.s.-1"));
        Assert.Equal(("s", 3), AdventureCallout.Offer("adventure-offer.s. 3"));
        Assert.Equal(("s", -1), AdventureCallout.Reached("adventure.s.-1"));
    }

    [Fact]
    public void AKeyThatDoesNotParseIsNull()
    {
        Assert.Null(CalloutKey.Split(null, "p.", NumberStyles.None));
        Assert.Null(CalloutKey.Split("q.a.1", "p.", NumberStyles.None));
        Assert.Null(CalloutKey.Split("p..1", "p.", NumberStyles.None));
        Assert.Null(CalloutKey.Split("p.a.x", "p.", NumberStyles.None));
        Assert.Null(AdventureCallout.Spoken("adventure-line.a.2"));
        Assert.Equal("p.a.1.2", CalloutKey.Join("p.", "a", 1, 2));
        Assert.Equal("p.a", CalloutKey.Join("p.", "a"));
    }
}
