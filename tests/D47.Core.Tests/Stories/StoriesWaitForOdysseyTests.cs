using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Persona;
using Xunit;
using static D47.Core.Tests.Stories.OdysseySupport;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>After a LoadGame without Odyssey no story is picked, no chapter is written and no clue is owed until one with it.</summary>
public sealed class StoriesWaitForOdysseyTests
{
    private static StoryFixtures Fixtures() => new(new RoundScriptedLlmProvider(
        RoundScriptedLlmProvider.Saying(Spine),
        RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
        RoundScriptedLlmProvider.Saying(NextSpine),
        RoundScriptedLlmProvider.Saying(NextBeats)));

    [Fact]
    public void TheSessionSummaryReadsTheFlagFromLoadGame()
    {
        Assert.Null(SessionSummary.Empty.Odyssey);
        Assert.False(SessionSummary.Empty.Apply(LoadGame(Now, odyssey: false)).Odyssey);
        Assert.True(SessionSummary.Empty.Apply(LoadGame(Now, odyssey: true)).Odyssey);
        Assert.Null(SessionSummary.Empty.Apply(LoadGame(Now, odyssey: null)).Odyssey);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task PickIsRefusedWithTheReasonWithoutOdyssey()
    {
        using var fixtures = Fixtures();

        fixtures.Director.Observe(LoadGame(Now.AddHours(-1), odyssey: false), "F1");

        Assert.True(fixtures.Director.WithoutOdyssey("F1"));
        Assert.Equal(StoryDirector.NeedsOdyssey, await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Null(fixtures.Stories.Current("F1"));
        Assert.Null(fixtures.Backstory);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task PickWorksBeforeAnyLoadGameIsSeen()
    {
        using var fixtures = Fixtures();

        Assert.False(fixtures.Director.WithoutOdyssey("F1"));
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ARunningStoryWritesNoChapterUntilOdysseyIsBack()
    {
        using var fixtures = Fixtures();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var chapter = fixtures.Stories.Current("F1")!.CurrentChapter!;

        fixtures.Director.Observe(LoadGame(Now.AddHours(1), odyssey: false), "F1");
        fixtures.Finish("F1", chapter, Now.AddHours(1));

        Assert.Null(fixtures.Director.Tick("F1", Now.AddHours(2)));
        Assert.Equal(StoryDirector.NeedsOdyssey, await fixtures.Director.WriteNextAsync("F1", Now.AddHours(2), CancellationToken.None));
        Assert.False(fixtures.Director.WriteFailed("F1"));

        fixtures.Director.Observe(LoadGame(Now.AddDays(1), odyssey: true), "F1");

        var write = fixtures.Director.Tick("F1", Now.AddDays(1));

        Assert.NotNull(write);
        Assert.Null(await write);
        Assert.Equal(2, fixtures.Stories.Current("F1")!.Chapters.Count);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task NoClueIsOwedWithoutOdyssey()
    {
        using var fixtures = Fixtures();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        fixtures.Director.Observe(BeaconFixture.JumpTo(Beacon, BeaconAddress), "F1");
        fixtures.Director.Observe(BeaconFixture.DataPoint(), "F1");

        var scan = fixtures.Stories.Current("F1")!.BeaconScanAt!.Value;

        Assert.Equal(new StoryClueDue(Id, 0), fixtures.Director.ClueDue("F1", scan.AddDays(7)));

        fixtures.Director.Observe(LoadGame(scan.AddDays(7), odyssey: false), "F1");

        Assert.Null(fixtures.Director.ClueDue("F1", scan.AddDays(30)));
        Assert.Null(fixtures.Director.Clue("F1", new StoryClueDue(Id, 0)));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AStorySwitchedOffStaysOffWhenOdysseyReturns()
    {
        using var fixtures = Fixtures();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Null(fixtures.Director.SetOn("F1", on: false, Now.AddHours(1)));

        fixtures.Director.Observe(LoadGame(Now.AddHours(2), odyssey: false), "F1");
        fixtures.Director.Observe(LoadGame(Now.AddDays(1), odyssey: true), "F1");

        Assert.False(fixtures.Director.WithoutOdyssey("F1"));
        Assert.True(fixtures.Director.IsOff("F1"));
    }
}
