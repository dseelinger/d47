using D47.Core.Adventures;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>While the story is switched off no beat is said, no clue or nudge is owed, and the hidden layer stays out of every prompt.</summary>
public sealed class APausedStorySaysNothingTests
{
    [Trait("Category", "Integration")]
    [Fact]
    public void ABeatReachedWhilePausedIsNotSaidAndWaitsForTheNextVisit()
    {
        using var fixtures = PauseSupport.Picked(out var chapter);
        var callout = new AdventureCallout(fixtures.Book);

        Assert.Null(fixtures.Director.SetOn("F1", false, Now));

        var during = PauseSupport.FirstBeat(fixtures, chapter, Now.AddMinutes(1));
        Assert.Empty(PauseSupport.Said(callout, Now.AddMinutes(1), during));
        Assert.Empty(fixtures.Book.Standing("F1", chapter)!.Fired);

        Assert.Null(fixtures.Director.SetOn("F1", true, Now.AddMinutes(2)));
        Assert.Empty(PauseSupport.Said(callout, Now.AddMinutes(3)));

        var again = PauseSupport.FirstBeat(fixtures, chapter, Now.AddMinutes(4));
        var said = PauseSupport.Said(callout, Now.AddMinutes(4), again);

        Assert.Contains(said, announcement => announcement.Key == $"adventure.{chapter}.0");
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void ABeatReachedJustBeforeThePauseIsDroppedNotSaidLate()
    {
        using var fixtures = PauseSupport.Picked(out var chapter);
        var callout = new AdventureCallout(fixtures.Book);

        // The opening is said first, leaving only the beat waiting out its settle.
        Assert.Contains(callout.Examine(PauseSupport.At(Now)), announcement => announcement.Key.EndsWith(".opening", StringComparison.Ordinal));

        var reached = PauseSupport.FirstBeat(fixtures, chapter, Now.AddMinutes(1));
        Assert.DoesNotContain(
            callout.Examine(PauseSupport.At(Now.AddMinutes(1), reached)),
            announcement => announcement.Key == $"adventure.{chapter}.0");

        Assert.Null(fixtures.Director.SetOn("F1", false, Now.AddMinutes(1).AddSeconds(5)));

        Assert.Empty(callout.Examine(PauseSupport.At(Now.AddMinutes(2))));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void NoHiddenLayerNoClueAndNoNudgeWhileOff()
    {
        using var fixtures = PauseSupport.Picked(out _);

        Assert.NotNull(fixtures.Director.HiddenBrief("F1"));

        fixtures.Director.SetOn("F1", false, Now);

        Assert.Null(fixtures.Director.HiddenBrief("F1"));
        Assert.Null(fixtures.Director.ClueDue("F1", Now.AddDays(400)));
        Assert.Empty(fixtures.Book.Audible("F1", Now.AddDays(400)));

        fixtures.Director.SetOn("F1", true, Now.AddDays(1));

        Assert.NotNull(fixtures.Director.HiddenBrief("F1"));
        Assert.Single(fixtures.Book.Audible("F1", Now.AddDays(1)));
    }

    [Fact]
    public void TheClueClockStopsWhileOff()
    {
        var story = new Story { Id = Id, Title = "T", PublicLayer = "P", PickedAt = Now.AddDays(-1), BeaconScanAt = Now }
            .SwitchedOff(Now.AddDays(2))
            .SwitchedOn(Now.AddDays(5));

        Assert.Equal(TimeSpan.FromDays(4), story.SinceBeacon(Now.AddDays(7)));
        Assert.Null(StoryClues.Due(story, Now.AddDays(9)));
        Assert.Equal(new StoryClueDue(Id, 0), StoryClues.Due(story, Now.AddDays(10)));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void WithNoStoryThereIsNothingToSwitch()
    {
        using var fixtures = new StoryFixtures(new Conversation.RoundScriptedLlmProvider());

        Assert.Equal("No story is running.", fixtures.Director.SetOn("F1", false, Now));
    }
}
