using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Persona;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Days the story spends paused after the beacon scan do not count toward a clue's day.</summary>
public sealed class PausedDaysDoNotCountTests
{
    private static readonly Story Scanned = new()
    {
        Id = Id,
        Title = Card.Title,
        PublicLayer = Card.Describe(),
        PickedAt = Now.AddDays(-1),
        BeaconScanAt = Now,
    };

    [Fact]
    public void ADayPausedIsADayTheClueStillWaits()
    {
        var paused = Scanned.Paused(Now.AddDays(2));

        Assert.Equal(TimeSpan.FromDays(2), paused.SinceBeacon(Now.AddDays(5)));
        Assert.Null(StoryClues.Due(paused, Now.AddDays(30)));

        var resumed = paused.Resumed(Now.AddDays(5));

        Assert.Equal(TimeSpan.FromDays(3), resumed.PausedFor);
        Assert.Null(StoryClues.Due(resumed, Now.AddDays(10) - TimeSpan.FromMinutes(1)));
        Assert.Equal(new StoryClueDue(Id, 0), StoryClues.Due(resumed, Now.AddDays(10)));
    }

    [Fact]
    public void APauseBeforeTheScanDoesNotCount()
    {
        var pausedEarly = Scanned with { BeaconScanAt = null };
        pausedEarly = pausedEarly.Paused(Now.AddDays(-1)) with { BeaconScanAt = Now };

        var resumed = pausedEarly.Resumed(Now.AddDays(1));

        Assert.Equal(TimeSpan.FromDays(1), resumed.PausedFor);
        Assert.Equal(TimeSpan.FromDays(6), resumed.SinceBeacon(Now.AddDays(7)));
    }

    [Fact]
    public async Task AnAbandonedChapterStopsTheClockUntilResume()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        fixtures.Director.Observe(BeaconFixture.JumpTo(Beacon, BeaconAddress), "F1");
        fixtures.Director.Observe(BeaconFixture.DataPoint(), "F1");

        var scan = fixtures.Stories.Current("F1")!.BeaconScanAt!.Value;
        var chapter = fixtures.Stories.Current("F1")!.CurrentChapter!;

        fixtures.Book.Abandon("F1", chapter, scan.AddDays(1));
        Assert.Null(fixtures.Director.Tick("F1", scan.AddDays(1)));
        Assert.Equal(StoryState.Paused, fixtures.Stories.Current("F1")!.State);

        Assert.Null(await fixtures.Director.ResumeAsync("F1", scan.AddDays(11), CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;
        Assert.Equal(StoryState.Running, story.State);
        Assert.Equal(TimeSpan.FromDays(10), story.PausedFor);

        Assert.Null(fixtures.Director.ClueDue("F1", scan.AddDays(16)));
        Assert.Equal(new StoryClueDue(Id, 0), fixtures.Director.ClueDue("F1", scan.AddDays(17)));
    }
}
