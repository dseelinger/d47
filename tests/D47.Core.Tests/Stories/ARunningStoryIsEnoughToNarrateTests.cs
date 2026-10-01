using D47.Core.Callouts;
using D47.Core.Journal;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A running, switched-on stock story gives the Narrator something to tell with no character sheet, About me or scenario.</summary>
public sealed class ARunningStoryIsEnoughToNarrateTests
{
    private static readonly TimeSpan Gap = TimeSpan.FromMinutes(30);

    [Fact]
    public void APickedStoryIsRunningUntilItIsSwitchedOff()
    {
        using var fixtures = PauseSupport.Picked(out _);

        Assert.True(fixtures.Director.IsRunning("F1"));

        fixtures.Director.SetOn("F1", false, Now);

        Assert.False(fixtures.Director.IsRunning("F1"));
        Assert.False(fixtures.Director.IsRunning("F2"));
    }

    [Fact]
    public void WithOnlyARunningStoryTheNarratorNarratesALull()
    {
        using var fixtures = PauseSupport.Picked(out _);

        var narrator = new NarratorCallout(new NearbyFight())
        {
            Interval = Gap,
            Longest = Gap,
            HasStory = () => false,
            StoryRunning = () => fixtures.Director.IsRunning("F1"),
        };

        _ = narrator.Examine(At(Now)).ToArray();

        Assert.Equal(NarratorCallout.Key, Assert.Single(narrator.Examine(At(Now + Gap * 3))).Key);
    }

    [Fact]
    public void WithTheStorySwitchedOffThereIsNothingToNarrate()
    {
        using var fixtures = PauseSupport.Picked(out _);
        fixtures.Director.SetOn("F1", false, Now);

        var narrator = new NarratorCallout(new NearbyFight())
        {
            Interval = Gap,
            Longest = Gap,
            StoryRunning = () => fixtures.Director.IsRunning("F1"),
        };

        _ = narrator.Examine(At(Now)).ToArray();

        Assert.Empty(narrator.Examine(At(Now + Gap * 3)));
    }

    private static CalloutContext At(DateTimeOffset now) =>
        new(now, false, null, GameStatus.Unknown with { Flags = StatusFlags.Docked | StatusFlags.InMainShip }, NavRoute.None, [], null);
}
