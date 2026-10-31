using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// The story waking on the tick: a picked story's opening and narrated scan are started together, and the cores each
/// wakes are handed on only once the line they wait behind has been said.
/// </summary>
public sealed class AStoryWakesItsCoresAfterItsLinesTests
{
    private static readonly IReadOnlyList<StoryLine> Opening = [new("The comms crackle.", StorySpeaker.Narrator)];

    private static readonly StorySecret OpensAndScans = Secret with
    {
        Opening = Opening,
        Scan = new("The data link completes.", StorySpeaker.Narrator),
    };

    private static StoryFixtures Week() => new(
        new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsElsewhere)),
        OpensAndScans,
        Card with { Length = StoryPacing.OneWeek.Key });

    private static StoryFixtures ThreeMonthsRemembered()
    {
        var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(Spine),
                RoundScriptedLlmProvider.Saying(BeatsElsewhere),
                RoundScriptedLlmProvider.Saying(NothingObjected)),
            Secret with { Opening = Opening },
            Card with { Length = StoryPacing.ThreeMonths.Key });

        fixtures.Stories.Save("F1", new Story
        {
            Id = Other.Id,
            Title = Other.Title,
            PublicLayer = Other.Describe(),
            Length = StoryPacing.OneYear.Key,
            State = StoryState.Finished,
            PickedAt = Now.AddDays(-200),
            BeaconScanAt = Now.AddDays(-199),
            BeaconSystems = [BeaconAddress],
            StoppedAt = Now.AddDays(-1),
        });

        return fixtures;
    }

    private static StoryWaking Waking(StoryFixtures fixtures, FakeVoice voice) =>
        new(fixtures.Director, fixtures.Stories, fixtures.Book, CommanderF1(), voice);

    private static GameStateStore CommanderF1()
    {
        var store = new GameStateStore();

        Assert.True(JournalEvent.TryParse(
            """{ "timestamp":"2026-09-30T11:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
            NullLogger.Instance,
            out var commander));
        store.Apply(commander!);

        return store;
    }

    [Fact]
    public async Task AnOpeningAndANarratedScanDueOnOneTickAreStartedTogether()
    {
        using var fixtures = Week();
        var voice = new FakeVoice();
        var waking = Waking(fixtures, voice);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        waking.Tick([], Now);

        var (opening, scan) = Assert.Single(voice.Started);

        Assert.Equal(Opening, opening!.Lines);
        Assert.Equal(OpensAndScans.Scan, scan!.Line);
    }

    [Fact]
    public async Task TheCoresANarratedScanWakesWaitUntilItsLineHasBeenSaid()
    {
        using var fixtures = Week();
        var voice = new FakeVoice();
        var waking = Waking(fixtures, voice);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        waking.Tick([], Now);
        waking.Tick([], Now.AddSeconds(5));

        Assert.True(voice.IsNarratingScan);
        Assert.Empty(voice.Woke);

        voice.IsSayingOpening = false;
        voice.IsNarratingScan = false;
        waking.Tick([], Now.AddSeconds(10));

        var (woken, core) = Assert.Single(voice.Woke);

        Assert.Equal(CoreWaking.Cores, woken);
        Assert.Equal("archivist", core!.Id);
    }

    [Fact]
    public async Task ARememberedScanBoardsItsCoreOnlyOnceTheOpeningTakenWithItHasBeenSaid()
    {
        using var fixtures = ThreeMonthsRemembered();
        var voice = new FakeVoice();
        var waking = Waking(fixtures, voice);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        waking.Tick([], Now);

        var (opening, scan) = Assert.Single(voice.Started);

        Assert.Equal(Opening, opening!.Lines);
        Assert.Null(scan);
        Assert.True(voice.IsSayingOpening);
        Assert.Empty(voice.Woke);

        voice.IsSayingOpening = false;
        waking.Tick([], Now.AddSeconds(5));

        var (woken, core) = Assert.Single(voice.Woke);

        Assert.Equal(CoreWaking.Remembered, woken);
        Assert.Equal("archivist", core!.Id);
    }

    /// <summary>Records what the waking starts and wakes; a start marks its lines as being said, as the narration does.</summary>
    private sealed class FakeVoice : IStoryWakingVoice
    {
        public List<(StoryOpeningDue? Opening, StoryScanDue? Scan)> Started { get; } = [];

        public List<(CoreWaking Waking, D47.Core.Persona.Persona? Core)> Woke { get; } = [];

        public bool IsNarratingScan { get; set; }

        public bool IsSayingOpening { get; set; }

        public void NarrateStart(StoryOpeningDue? opening, StoryScanDue? scan, string? commander)
        {
            Started.Add((opening, scan));
            IsSayingOpening |= opening is { Lines.Count: > 0 };
            IsNarratingScan |= scan?.Line is not null;
        }

        public void PostEndingIfDue(string? commander)
        {
        }

        public void CoresWoke(CoreWaking waking, D47.Core.Persona.Persona? storyCore) => Woke.Add((waking, storyCore));

        public void ReadmitIfTheHoldChanged()
        {
        }

        public void SayAside(string line)
        {
        }
    }
}
