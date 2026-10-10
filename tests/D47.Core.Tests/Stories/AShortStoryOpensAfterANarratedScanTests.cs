using D47.Core.Adventures;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A story shorter than a month opens after a beacon scan narrated at the pick: the clock starts at the pick, the cores
/// wake, chapter one is act one with no beacon, and one real scan wakes the Heretic. From a month up the scan is real.
/// </summary>
public sealed class AShortStoryOpensAfterANarratedScanTests
{
    private static readonly StoryCard WeekCard = Card with { Length = StoryPacing.OneWeek.Key };

    private static readonly StorySecret WithScan = Secret with { Scan = new("The data link completes.", StorySpeaker.Narrator) };

    private static StoryFixtures Week() => new(
        new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsElsewhere)),
        WithScan,
        WeekCard);

    [Fact]
    public async Task PickingAWeekStampsTheScanAtThePick()
    {
        using var fixtures = Week();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;

        Assert.Equal(Now, story.BeaconScanAt);
        Assert.True(story.BeaconNarrated);
        Assert.Null(StoryClues.Due(story, Now.AddDays(1).AddMinutes(-1)));
        Assert.Equal(new StoryClueDue(Id, 0), StoryClues.Due(story, Now.AddDays(1)));
    }

    [Fact]
    public async Task ThePickLeavesTheScanLineForTheTickOnce()
    {
        using var fixtures = Week();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var due = fixtures.Director.TakeNarratedScan("F1");

        Assert.Equal(new StoryScanDue(Id, Card.Title, WithScan.Scan), due);
        Assert.Null(fixtures.Director.TakeNarratedScan("F1"));
        Assert.Equal("archivist", fixtures.Director.CoreOf("F1")!.Id);
    }

    [Fact]
    public async Task AnAbandonedPickSaysNoScan()
    {
        using var fixtures = Week();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Null(fixtures.Director.Abandon("F1", Now.AddMinutes(1)));

        Assert.Null(fixtures.Director.TakeNarratedScan("F1"));
    }

    [Fact]
    public async Task ChapterOneIsActOneWithNoBeacon()
    {
        using var fixtures = Week();
        fixtures.Here = BeyondTheBeacon(10);
        fixtures.Director.Game = () => Flying(35, scoop: true);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var ask = Assert.Single(fixtures.Asks).Story!;

        Assert.Null(ask.Beacon);
        Assert.Null(ask.BeaconAway);
        Assert.Equal("Act one", ask.Stage);
        Assert.Equal(["openingImage", "catalyst", "breakIntoTwo"], ask.StageBeats!.Select(beat => beat.Key));
        Assert.Equal(StoryStage.BreakIntoTwo, StoryClues.Stage(fixtures.Stories.Current("F1")!));
    }

    [Fact]
    public async Task OneRealScanWakesTheHeretic()
    {
        using var fixtures = Week();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Equal(HeldCores.Heretic, fixtures.Stories.Current("F1")!.HeldCores);

        Assert.Equal(CoreWaking.Heretic, fixtures.ScanBeacon("F1", BeaconAddress, Now.AddHours(1)));

        var story = fixtures.Stories.Current("F1")!;

        Assert.Equal(HeldCores.None, story.HeldCores);
        Assert.Equal(Now, story.BeaconScanAt);
    }

    [Fact]
    public async Task TheHeldHereticAsksForAnyBeacon()
    {
        using var fixtures = Week();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var install = new MemoryInstall();
        var surface = TestSurface.For(install, personas: new PersonaHost(cores: fixtures.Cores("F1")));
        var refused = surface.Settings.Apply(PersonaCapability.PersonaKey, "heretic", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Rejected, refused.Status);
        Assert.Equal(
            "The Heretic is held back while The Test Story runs, until you scan a Guardian beacon. Pause or abandon the story to have it back now.",
            refused.Message);
    }

    [Fact]
    public async Task AMonthWaitsForARealScan()
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)),
            card: Card with { Length = StoryPacing.OneMonth.Key })
        {
            Here = BeyondTheBeacon(10),
        };

        fixtures.Director.Game = () => Flying(35, scoop: true);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;

        Assert.Null(story.BeaconScanAt);
        Assert.False(story.BeaconNarrated);
        Assert.Null(fixtures.Director.TakeNarratedScan("F1"));
        Assert.Equal(BeaconAddress, Assert.Single(fixtures.Asks).Story!.Beacon!.SystemAddress);
        Assert.Equal(TriggerKind.Beacon, fixtures.Book.Store.Find("F1", story.CurrentChapter!)!.Beats[^1].Trigger.Kind);
    }

    [Fact]
    public async Task TheNarratedFlagSurvivesARestart()
    {
        using var fixtures = Week();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var reopened = StoryStore.Open(fixtures.StoryPath, fixtures.Files, NullLogger<StoryStore>.Instance).Current("F1")!;

        Assert.True(reopened.BeaconNarrated);
        Assert.Equal(HeldCores.Heretic, reopened.HeldCores);
    }
}
