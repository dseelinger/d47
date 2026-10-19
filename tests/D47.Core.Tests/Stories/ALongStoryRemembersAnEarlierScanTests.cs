using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A story of a month or longer, picked by a Commander whose beacon scan an earlier story recorded, opens with the scan
/// behind them: no trip, the cores not held back for it, and its core aboard once the opening has been said.
/// </summary>
public sealed class ALongStoryRemembersAnEarlierScanTests
{
    private const string RememberedSentence =
        "The Commander scanned a Guardian beacon in an earlier story, before this one opened. Where act one's beats lead "
        + "toward a beacon, that trip is already behind them: tell it as the past that brought the core aboard.";

    private static readonly StoryCard ThreeMonths = Card with { Length = StoryPacing.ThreeMonths.Key };

    /// <summary>Fixtures whose provider writes chapter one <paramref name="picks"/> times, with <paramref name="beats"/>, which by default have no beacon objective.</summary>
    private static StoryFixtures Fixtures(int picks = 1, StoryCard? card = null, string beats = BeatsElsewhere) => new(
        new RoundScriptedLlmProvider([.. Enumerable.Range(0, picks).SelectMany(_ => new[]
        {
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(beats),
        })]),
        card: card ?? ThreeMonths);

    /// <summary>A stopped run of <see cref="Other"/>, a year long, that scanned a beacon in each of <paramref name="systems"/>.</summary>
    private static Story OnRecord(StoryState state, params long[] systems) => new()
    {
        Id = Other.Id,
        Title = Other.Title,
        PublicLayer = Other.Describe(),
        Length = StoryPacing.OneYear.Key,
        State = state,
        PickedAt = Now.AddDays(-200),
        BeaconScanAt = Now.AddDays(-199),
        BeaconSystems = systems,
        StoppedAt = Now.AddDays(-1),
    };

    [Theory]
    [InlineData(StoryState.Finished)]
    [InlineData(StoryState.Abandoned)]
    public async Task OneRealSystemOnRecordOpensWithTheScanBehind(StoryState state)
    {
        using var fixtures = Fixtures();
        fixtures.Stories.Save("F1", OnRecord(state, BeaconAddress));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;
        var ask = fixtures.Asks[0].Story!;

        Assert.Equal(Now, story.BeaconScanAt);
        Assert.Equal(1, story.BeaconsRemembered);
        Assert.False(story.BeaconNarrated);
        Assert.Equal(HeldCores.Heretic, story.HeldCores);
        Assert.True(story.CoreHold.Narrated);
        Assert.Equal(StoryClues.StageName(StoryStage.ActOne), ask.Stage);
        Assert.Null(ask.Beacon);
        Assert.Null(ask.BeaconAway);
        Assert.True(ask.Remembered);
    }

    [Fact]
    public async Task TwoRealSystemsOnRecordHoldNothing()
    {
        using var fixtures = Fixtures();
        fixtures.Stories.Save("F1", OnRecord(StoryState.Finished, BeaconAddress, SecondBeaconAddress));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;

        Assert.Equal(2, story.BeaconsRemembered);
        Assert.Equal(HeldCores.None, story.HeldCores);
    }

    [Fact]
    public async Task ARealScanAfterARememberedOneWakesTheHeretic()
    {
        using var fixtures = Fixtures();
        fixtures.Stories.Save("F1", OnRecord(StoryState.Finished, BeaconAddress));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal(CoreWaking.Heretic, fixtures.ScanBeacon("F1", BeaconAddress, Now.AddHours(1)));
        Assert.Equal(HeldCores.None, fixtures.Stories.Current("F1")!.HeldCores);
    }

    [Fact]
    public async Task NarratedShortStoriesAloneLeaveTheTripToFly()
    {
        using var fixtures = Fixtures(beats: BeatsToTheBeacon);
        fixtures.Stories.Save("F1", OnRecord(StoryState.Finished) with
        {
            Length = StoryPacing.OneWeek.Key,
            BeaconNarrated = true,
            BeaconScanAt = Now.AddDays(-200),
        });

        Assert.False(fixtures.Director.Remembers("F1", ThreeMonths));
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;

        Assert.Null(story.BeaconScanAt);
        Assert.Equal(0, story.BeaconsRemembered);
        Assert.Equal(HeldCores.All, story.HeldCores);
        Assert.False(fixtures.Asks[0].Story!.Remembered);
        Assert.NotNull(fixtures.Asks[0].Story!.Beacon);
        Assert.Null(fixtures.Director.TakeRememberedScan("F1"));
        Assert.Contains("The beacon calls.", story.PublicLayer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARememberedStoryPickedAgainStaysOnRecord()
    {
        using var fixtures = Fixtures(picks: 3);
        fixtures.Stories.Save("F1", OnRecord(StoryState.Finished, BeaconAddress));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Null(fixtures.Director.Abandon("F1", Now.AddDays(1)));
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now.AddDays(2), CancellationToken.None));

        Assert.Equal(1, fixtures.Stories.Current("F1")!.BeaconsRemembered);

        // The picked-again record, read back from the file, is the only other story on record when the first is picked again.
        Assert.Null(fixtures.Director.Abandon("F1", Now.AddDays(3)));
        fixtures.Stories.Reopen(fixtures.StoryPath);
        Assert.True(fixtures.Director.Remembers("F1", Other));
        Assert.Null(await fixtures.Director.PickAsync("F1", Other.Id, Now.AddDays(4), CancellationToken.None));

        Assert.Equal(1, fixtures.Stories.Current("F1")!.BeaconsRemembered);
    }

    [Fact]
    public async Task ThePickLeavesTheCoreForTheTickOnceAndNoScanLine()
    {
        using var fixtures = Fixtures();
        fixtures.Stories.Save("F1", OnRecord(StoryState.Finished, BeaconAddress));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal(new StoryRememberedDue(Id, Card.Title), fixtures.Director.TakeRememberedScan("F1"));
        Assert.Null(fixtures.Director.TakeRememberedScan("F1"));
        Assert.Null(fixtures.Director.TakeNarratedScan("F1"));
        Assert.Equal("archivist", fixtures.Director.CoreOf("F1")!.Id);
    }

    [Fact]
    public async Task AnAbandonedPickBoardsNoCore()
    {
        using var fixtures = Fixtures();
        fixtures.Stories.Save("F1", OnRecord(StoryState.Finished, BeaconAddress));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Null(fixtures.Director.Abandon("F1", Now.AddMinutes(1)));

        Assert.Null(fixtures.Director.TakeRememberedScan("F1"));
    }

    [Fact]
    public void TheCoreBoardsAfterTheOpeningHasBeenSaid()
    {
        const string opening = "story.opening";
        var wakings = new WakingAfterTheBeat();
        var saying = true;

        wakings.Add(CoreWaking.Remembered, PersonaCatalog.Archivist, opening, Now);

        Assert.Empty(wakings.Due(owed => owed == opening && saying, Now.AddSeconds(5)));

        saying = false;

        Assert.Equal([(CoreWaking.Remembered, PersonaCatalog.Archivist)], wakings.Due(owed => owed == opening && saying, Now.AddSeconds(6)));
    }

    [Fact]
    public void TheRememberedLineNamesTheCoreAboard()
    {
        Assert.Equal(
            "The Guardian cores from your last beacon scan are still in the ship's systems. "
            + "Archivist has come aboard for this story. You can choose another core in Settings.",
            GuardianCores.Line(CoreWaking.Remembered, PersonaCatalog.Archivist));
    }

    [Fact]
    public async Task ThePublicLayerLeavesOutTheBeaconAndThePromptRemembers()
    {
        using var fixtures = Fixtures();
        fixtures.Stories.Save("F1", OnRecord(StoryState.Finished, BeaconAddress));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.DoesNotContain("The beacon calls.", fixtures.Stories.Current("F1")!.PublicLayer, StringComparison.Ordinal);
        Assert.All(fixtures.Provider.Requests, request => Assert.Contains(RememberedSentence, request.Prompt.History[0].Text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AShortStoryStillNarratesItsScan()
    {
        using var fixtures = Fixtures(card: Card with { Length = StoryPacing.OneWeek.Key });
        fixtures.Stories.Save("F1", OnRecord(StoryState.Finished, BeaconAddress));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;

        Assert.True(story.BeaconNarrated);
        Assert.Equal(0, story.BeaconsRemembered);
        Assert.NotNull(fixtures.Director.TakeNarratedScan("F1"));
        Assert.Null(fixtures.Director.TakeRememberedScan("F1"));
    }
}
