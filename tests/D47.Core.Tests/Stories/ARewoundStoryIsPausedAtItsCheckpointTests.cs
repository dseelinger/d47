using D47.Core.Adventures;
using D47.Core.Storage;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

public sealed class ARewoundStoryIsPausedAtItsCheckpointTests
{
    private static readonly string Data = Path.Combine(MemoryInstall.FakeRoot, "rewind", "data");

    private readonly MemoryFileSystem _files = new();
    private readonly TaskCompletionSource<AdventureOutcome> _write = new();
    private readonly StoryStore _stories;
    private readonly AdventureStore _adventures;
    private readonly StoryChapterArchive _archive;
    private readonly StoryDirector _director;
    private readonly StoryBench _bench;
    private DateTimeOffset _clock = Now;

    public ARewoundStoryIsPausedAtItsCheckpointTests()
    {
        var mine = StorySlotFiles.In(Data);

        _stories = StoryStore.Open(mine.Stories, _files, NullLogger<StoryStore>.Instance);
        _adventures = new AdventureStore(mine.Adventures, _files, NullLogger<AdventureStore>.Instance);
        _archive = StoryChapterArchive.Open(_files, mine.Chapters, NullLogger<StoryChapterArchive>.Instance);

        var book = new AdventureBook(_adventures, NullLogger<AdventureBook>.Instance);

        book.CatchUp([]);
        _director = new StoryDirector(_stories, book, () => StoryFixtures.Catalog, (_, _, _) => _write.Task, () => null, _ => { }, NullLogger.Instance)
        {
            Archive = _archive,
            Clock = () => _clock,
        };
        _bench = new StoryBench(Data, _files, _stories, _adventures, _archive, NullLogger<StoryBench>.Instance) { Director = _director };
    }

    private static string Key(int chapter) => $"chapter-{chapter}";

    private static string CheckpointFolder(string slot, int chapter) =>
        Path.Combine(Data, StoryBench.Folder, slot, "checkpoints", chapter.ToString());

    /// <summary>Writes chapter <paramref name="number"/> the way the director does: its adventure, then the story, then the archive.</summary>
    private async Task WriteChapter(int number, DateTimeOffset at)
    {
        _clock = at;
        _adventures.Save("F1", AdventureFixtures.LanternRoute(at) with { Key = Key(number), StoryId = Id });

        if (number > 2)
        {
            var old = _adventures.Find("F1", Key(number - 2))!;

            _archive.Append(new ArchivedChapter("F1", Id, Now, old, [], []));
            _adventures.Remove("F1", Key(number - 2));
        }

        var story = _stories.Current("F1") ?? new Story { Id = Id, Title = Card.Title, PublicLayer = "-", PickedAt = Now, BeaconScanAt = Now };

        _stories.Save("F1", story with { Chapters = [.. story.Chapters.Take(number - 1), Key(number)] });
        await _bench.Settled();
    }

    [Fact]
    public async Task ThreeChaptersLeaveThreeCheckpoints()
    {
        await WriteChapter(1, Now.AddHours(1));
        await WriteChapter(2, Now.AddHours(2));
        await WriteChapter(3, Now.AddHours(3));

        var checkpoints = _bench.Checkpoints("F1");

        Assert.Equal([1, 2, 3], checkpoints.Select(checkpoint => checkpoint.Chapter));
        Assert.Equal(Now.AddHours(2), checkpoints[1].At);
        Assert.NotNull(_files.ReadText(Path.Combine(CheckpointFolder("mine", 3), "story.json")));
    }

    [Fact]
    public async Task RewindingToTheSecondRestoresItsFilesAndPausesTheStory()
    {
        await WriteChapter(1, Now.AddHours(1));
        await WriteChapter(2, Now.AddHours(2));
        await WriteChapter(3, Now.AddHours(3));
        Assert.Single(_archive.For("F1"));

        Assert.Null(_bench.Rewind(2, Now.AddHours(5)));

        var story = _stories.Current("F1")!;
        Assert.Equal(StoryState.Paused, story.State);
        Assert.Equal([Key(1), Key(2)], story.Chapters);
        Assert.Empty(_archive.For("F1"));
        Assert.DoesNotContain("chapter-1", _files.ReadText(StorySlotFiles.In(Data).Chapters) ?? string.Empty);
        Assert.NotNull(_adventures.Find("F1", Key(1)));
        Assert.Null(_adventures.Find("F1", Key(3)));
        Assert.True(_adventures.Find("F1", Key(2))!.IsAbandoned);
    }

    [Fact]
    public async Task RewindingToACheckpointTakenBeforeAStepLeavesThatBeatUnfired()
    {
        await WriteChapter(1, Now.AddHours(1));
        await WriteChapter(2, Now.AddHours(2));

        Assert.Null(_bench.Step("F1", Key(2), Now.AddHours(2.5)));
        Assert.NotEmpty(_adventures.Find("F1", Key(2))!.Stepped);

        Assert.Null(_bench.Rewind(2, Now.AddHours(5)));

        Assert.Empty(_adventures.Find("F1", Key(2))!.Stepped);
        Assert.True(_director.Book.NeedsCatchUp);

        _director.Book.CatchUp([]);

        Assert.Equal(0, _director.Book.Standing("F1", Key(2))!.Current);
    }

    [Fact]
    public async Task SteppingTheLastBeatOfAChapterLetsTheDirectorWriteTheNext()
    {
        await WriteChapter(1, Now.AddHours(1));
        var chapter = _adventures.Find("F1", Key(1))!;
        Assert.Null(_adventures.Save("F1", chapter with { Beats = [chapter.Beats[0]] }));

        Assert.Null(_bench.Step("F1", Key(1), Now.AddHours(2)));
        Assert.True(_director.Book.Standing("F1", Key(1))!.IsDone);

        var writing = _director.Tick("F1", Now.AddHours(3));

        Assert.True(SpinWait.SpinUntil(() => _director.IsWriting("F1"), TimeSpan.FromSeconds(5)));
        Assert.True(_director.IsWriting("F1"));

        _write.SetResult(new AdventureOutcome(null, null, "No.", []));
        await writing!;
    }

    [Fact]
    public async Task ABeaconBeatCannotBeSteppedYet()
    {
        await WriteChapter(1, Now.AddHours(1));
        var chapter = _adventures.Find("F1", Key(1))!;
        Assert.Null(_adventures.Save("F1", chapter with
        {
            Beats = [AdventureFixtures.Beat("The Beacon", "resolution", new AdventureTrigger { Kind = TriggerKind.Beacon, SystemAddress = StoryFixtures.BeaconAddress, System = StoryFixtures.Beacon }, "Scan it.")],
        }));
        var before = _files.ReadText(StorySlotFiles.In(Data).Adventures);

        Assert.Equal("The beacon scan cannot be stepped yet.", _bench.Step("F1", Key(1), Now.AddHours(2)));
        Assert.Equal(before, _files.ReadText(StorySlotFiles.In(Data).Adventures));
    }

    [Fact]
    public async Task AStepWhileAChapterIsWrittenIsRefusedAndChangesNothing()
    {
        await WriteChapter(1, Now.AddHours(1));

        var picking = _director.PickAsync("F2", Id, Now, CancellationToken.None);
        Assert.True(_director.IsWriting("F2"));

        var adventures = _files.ReadText(StorySlotFiles.In(Data).Adventures);

        Assert.Equal(StoryBench.Writing, _bench.Step("F2", Key(1), Now.AddHours(3)));
        Assert.Equal(adventures, _files.ReadText(StorySlotFiles.In(Data).Adventures));

        _write.SetResult(new AdventureOutcome(null, null, "No.", []));
        await picking;
    }

    [Fact]
    public async Task ResumeBeginsTheRestoredChapterAgainAtTheResumeTime()
    {
        await WriteChapter(1, Now.AddHours(1));
        await WriteChapter(2, Now.AddHours(2));
        await WriteChapter(3, Now.AddHours(3));
        Assert.Null(_bench.Rewind(2, Now.AddHours(5)));

        var resumedAt = Now.AddHours(6);

        Assert.Null(await _director.ResumeAsync("F1", resumedAt, CancellationToken.None));

        Assert.Equal(StoryState.Running, _stories.Current("F1")!.State);
        Assert.Equal(resumedAt, _adventures.Find("F1", Key(2))!.AcceptedAt);
        Assert.True(_adventures.Find("F1", Key(2))!.IsActive);
    }

    [Fact]
    public async Task TheBeaconClockReadsAsItDidAtTheCheckpoint()
    {
        var picked = new Story { Id = Id, Title = Card.Title, PublicLayer = "-", PickedAt = Now, BeaconScanAt = Now };

        _stories.Save("F1", picked with { PausedFor = TimeSpan.FromMinutes(7) });
        await WriteChapter(1, Now.AddHours(1));
        await WriteChapter(2, Now.AddHours(2));
        await WriteChapter(3, Now.AddHours(3));

        var expected = _stories.Current("F1")!.SinceBeacon(Now.AddHours(2));
        var rewoundAt = Now.AddHours(9);

        Assert.Null(_bench.Rewind(2, rewoundAt));

        Assert.Equal(expected, _stories.Current("F1")!.SinceBeacon(rewoundAt));
        Assert.Equal(expected, _stories.Current("F1")!.SinceBeacon(rewoundAt.AddHours(4)));
    }

    [Fact]
    public async Task ARewindWhileAChapterIsWrittenIsRefusedAndChangesNothing()
    {
        await WriteChapter(1, Now.AddHours(1));
        await WriteChapter(2, Now.AddHours(2));

        var picking = _director.PickAsync("F2", Id, Now, CancellationToken.None);
        Assert.True(_director.IsWriting("F2"));
        var before = _files.ReadText(StorySlotFiles.In(Data).Stories);

        Assert.Equal(StoryBench.Writing, _bench.Rewind(1, Now.AddHours(3)));

        Assert.Equal(before, _files.ReadText(StorySlotFiles.In(Data).Stories));
        Assert.Equal(StoryState.Running, _stories.Current("F1")!.State);

        _write.SetResult(new AdventureOutcome(null, null, "No.", []));
        await picking;
    }

    [Fact]
    public async Task ANewChapterWrittenPastARewindReplacesTheLaterCheckpoints()
    {
        await WriteChapter(1, Now.AddHours(1));
        await WriteChapter(2, Now.AddHours(2));
        await WriteChapter(3, Now.AddHours(3));
        Assert.Null(_bench.Rewind(1, Now.AddHours(5)));
        Assert.Equal([1, 2, 3], _bench.Checkpoints("F1").Select(checkpoint => checkpoint.Chapter));

        _stories.Update("F1", Id, story => story.Resumed(Now.AddHours(6)));
        _adventures.Save("F1", AdventureFixtures.LanternRoute(Now.AddHours(6)) with { Key = "alternative-2", StoryId = Id });
        _stories.Update("F1", Id, story => story with { Chapters = [Key(1), "alternative-2"] });
        await _bench.Settled();

        Assert.Equal([1, 2], _bench.Checkpoints("F1").Select(checkpoint => checkpoint.Chapter));
        Assert.Contains("alternative-2", _files.ReadText(Path.Combine(CheckpointFolder("mine", 2), "story.json")));
    }

    [Fact]
    public async Task ASlotKeepsItsOwnCheckpointsAndASwitchTakesNone()
    {
        await WriteChapter(1, Now.AddHours(1));

        Assert.Null(_bench.Create("test"));
        Assert.Null(_bench.SwitchTo("test", "F1", Now.AddHours(2)));
        await _bench.Settled();

        Assert.Empty(_bench.Checkpoints("F1"));
        Assert.Empty(_files.Enumerate(Path.Combine(Data, StoryBench.Folder, "test"), "checkpoint.json", recursive: true));

        Assert.Null(_bench.SwitchTo(null, "F1", Now.AddHours(3)));
        await _bench.Settled();

        Assert.Equal([1], _bench.Checkpoints("F1").Select(checkpoint => checkpoint.Chapter));
    }

    [Fact]
    public async Task ARewindToAChapterThatHasNoCheckpointIsRefused()
    {
        await WriteChapter(1, Now.AddHours(1));

        Assert.NotNull(_bench.Rewind(4, Now.AddHours(2)));
        Assert.Equal(StoryState.Running, _stories.Current("F1")!.State);
    }
}
