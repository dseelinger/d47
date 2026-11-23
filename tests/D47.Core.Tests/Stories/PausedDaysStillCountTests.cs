using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Persona;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Days the story spends paused after the beacon scan count toward a clue's day.</summary>
public sealed class PausedDaysStillCountTests
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
    public void ADayPausedIsADayTowardTheClue()
    {
        var paused = Scanned.Paused(Now.AddDays(2));

        Assert.Equal(TimeSpan.FromDays(5), paused.SinceBeacon(Now.AddDays(5)));

        var resumed = paused.Resumed(Now.AddDays(5));

        Assert.Equal(TimeSpan.FromDays(3), resumed.PausedFor);
        Assert.Null(StoryClues.Due(resumed, Now.AddDays(7) - TimeSpan.FromMinutes(1)));
        Assert.Equal(new StoryClueDue(Id, 0), StoryClues.Due(resumed, Now.AddDays(7)));
    }

    [Fact]
    public void APauseBeforeTheScanIsNotPausedTimeSinceTheScan()
    {
        var pausedEarly = Scanned with { BeaconScanAt = null };
        pausedEarly = pausedEarly.Paused(Now.AddDays(-1)) with { BeaconScanAt = Now };

        var resumed = pausedEarly.Resumed(Now.AddDays(1));

        Assert.Equal(TimeSpan.FromDays(1), resumed.PausedFor);
        Assert.Equal(TimeSpan.FromDays(7), resumed.SinceBeacon(Now.AddDays(7)));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AnAbandonedChapterDoesNotStopTheClock()
    {
        using var fixtures = StoryFixtures.ScriptedWith(Lines());

        await fixtures.PickAndScanTheBeaconAsync(Now);

        var scan = fixtures.Stories.Current("F1")!.BeaconScanAt!.Value;
        var chapter = fixtures.Stories.Current("F1")!.CurrentChapter!;

        fixtures.Book.Abandon("F1", chapter, scan.AddDays(1));
        Assert.Null(fixtures.Director.Tick("F1", scan.AddDays(1)));
        Assert.Equal(StoryState.Paused, fixtures.Stories.Current("F1")!.State);

        Assert.Null(await fixtures.Director.ResumeAsync("F1", scan.AddDays(11), CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;
        Assert.Equal(StoryState.Running, story.State);
        Assert.Equal(TimeSpan.FromDays(10), story.PausedFor);

        Assert.Equal(TimeSpan.FromDays(11), story.SinceBeacon(scan.AddDays(11)));
        Assert.Equal(new StoryClueDue(Id, 0), fixtures.Director.ClueDue("F1", scan.AddDays(7)));
    }
}
