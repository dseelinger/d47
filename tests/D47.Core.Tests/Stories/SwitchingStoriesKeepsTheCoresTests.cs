using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Switch and Abandon stop a story, keep what it told, and leave awake cores awake.</summary>
public sealed class SwitchingStoriesKeepsTheCoresTests
{
    private static StoryFixtures Fixtures() => new(new RoundScriptedLlmProvider(
        RoundScriptedLlmProvider.Saying(Spine),
        RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
        RoundScriptedLlmProvider.Saying(Spine),
        RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

    private static GuardianCores Awake()
    {
        var cores = GuardianCores.Asleep();
        cores.Apply(AdventureFixtures.Jump(BeaconAddress, Now));
        cores.Apply(AdventureFixtures.Event($$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"DataScanned", "Type":"$Datascan_AncientBeacon;" }"""));
        Assert.True(cores.CoresAwake);
        return cores;
    }

    [Fact]
    public async Task SwitchingAbandonsTheOldStoryAndKeepsItsChapter()
    {
        var cores = Awake();
        using var fixtures = Fixtures();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        var first = Assert.Single(fixtures.Stories.Current("F1")!.Chapters);

        Assert.Null(await fixtures.Director.SwitchAsync("F1", Other.Id, Now.AddDays(1), CancellationToken.None));

        var old = fixtures.Stories.Find("F1", Id)!;
        Assert.Equal(StoryState.Abandoned, old.State);
        Assert.Equal([first], old.Chapters);
        Assert.True(fixtures.Book.Store.Find("F1", first)!.IsAbandoned);

        var current = fixtures.Stories.Current("F1")!;
        Assert.Equal(Other.Id, current.Id);
        Assert.Equal(Other.InYourWords, fixtures.Backstory);

        Assert.True(cores.CoresAwake);
    }

    [Fact]
    public async Task AbandoningEndsTheStoryAndLeavesTheCores()
    {
        var cores = Awake();
        using var fixtures = Fixtures();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Null(fixtures.Director.Abandon("F1", Now.AddDays(1)));

        Assert.Null(fixtures.Stories.Current("F1"));
        Assert.Equal(StoryState.Ended, fixtures.Stories.Find("F1", Id)!.State);
        Assert.Single(fixtures.Book.Store.For("F1"));
        Assert.True(cores.CoresAwake);
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

        fixtures.Director.Observe(AdventureFixtures.Jump(BeaconAddress, Now.AddDays(2)), "F1");
        fixtures.Director.Observe(
            AdventureFixtures.Event($$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now.AddDays(2))}}", "event":"DataScanned", "Type":"$Datascan_AncientBeacon;" }"""),
            "F1");

        Assert.Equal(Now.AddDays(2), fixtures.Stories.Current("F1")!.BeaconScanAt);
    }
}
