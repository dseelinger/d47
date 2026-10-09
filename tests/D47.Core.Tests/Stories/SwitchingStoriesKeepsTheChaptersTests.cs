using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Switch and Abandon stop a story, keep its chapters in the archive, and leave the cores to the story that is current.</summary>
[Trait("Category", "Integration")]
public sealed class SwitchingStoriesKeepsTheChaptersTests
{
    private static StoryFixtures Fixtures() => new(new RoundScriptedLlmProvider(
        RoundScriptedLlmProvider.Saying(Spine),
        RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
        RoundScriptedLlmProvider.Saying(Spine),
        RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

    [Fact]
    public async Task SwitchingAbandonsTheOldStoryAndKeepsItsChapter()
    {
        using var fixtures = Fixtures();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        var first = Assert.Single(fixtures.Stories.Current("F1")!.Chapters);

        Assert.Null(await fixtures.Director.SwitchAsync("F1", Other.Id, Now.AddDays(1), CancellationToken.None));

        var old = fixtures.Stories.Find("F1", Id)!;
        Assert.Equal(StoryState.Abandoned, old.State);
        Assert.Equal([first], old.Chapters);
        Assert.DoesNotContain(fixtures.Book.Store.For("F1"), adventure => adventure.StoryId == Id);
        Assert.True(Assert.Single(fixtures.Director.Archive.For("F1")).Adventure.IsAbandoned);

        var current = fixtures.Stories.Current("F1")!;
        Assert.Equal(Other.Id, current.Id);
        Assert.Equal(Other.InYourWords, fixtures.Backstory);

        Assert.Equal(new CoreHold(HeldCores.All, Other.Title), current.CoreHold);
    }

    [Fact]
    public async Task AbandoningEndsTheStoryAndGivesTheCoresBack()
    {
        using var fixtures = Fixtures();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Null(fixtures.Director.Abandon("F1", Now.AddDays(1)));

        Assert.Null(fixtures.Stories.Current("F1"));
        Assert.Equal(StoryState.Ended, fixtures.Stories.Find("F1", Id)!.State);
        Assert.Empty(fixtures.Book.Store.For("F1"));
        Assert.Single(fixtures.Director.Archive.For("F1"));
        Assert.Equal(CoreHold.None, fixtures.Cores("F1").Hold);
    }

    [Fact]
    public async Task PickingWhileAStoryRunsIsRefused()
    {
        using var fixtures = Fixtures();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var refusal = await fixtures.Director.PickAsync("F1", Other.Id, Now, CancellationToken.None);

        Assert.Equal("The Test Story is your story. Switch to change it.", refusal);
        Assert.Equal(Id, fixtures.Stories.Current("F1")!.Id);
    }

    [Fact]
    public async Task ABeaconScanIsStampedOnTheRunningStory()
    {
        using var fixtures = Fixtures();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        fixtures.ScanBeacon("F1", BeaconAddress, Now.AddDays(2));

        Assert.Equal(Now.AddDays(2), fixtures.Stories.Current("F1")!.BeaconScanAt);
    }
}
