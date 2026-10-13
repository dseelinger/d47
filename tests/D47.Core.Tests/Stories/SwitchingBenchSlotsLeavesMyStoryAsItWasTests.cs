using System.Text.Json;
using D47.Core.Adventures;
using D47.Core.Storage;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

public sealed class SwitchingBenchSlotsLeavesMyStoryAsItWasTests
{
    private static readonly string Data = Path.Combine(MemoryInstall.FakeRoot, "bench", "data");

    private readonly MemoryFileSystem _files = new();
    private readonly TaskCompletionSource<AdventureOutcome> _write = new();
    private readonly StoryStore _stories;
    private readonly AdventureStore _adventures;
    private readonly StoryChapterArchive _archive;
    private readonly AdventureBook _book;
    private readonly StoryDirector _director;
    private readonly StoryBench _bench;
    private string? _backstory;

    public SwitchingBenchSlotsLeavesMyStoryAsItWasTests()
    {
        var mine = StorySlotFiles.In(Data);

        _stories = StoryStore.Open(mine.Stories, _files, NullLogger<StoryStore>.Instance);
        _adventures = new AdventureStore(mine.Adventures, _files, NullLogger<AdventureStore>.Instance);
        _archive = StoryChapterArchive.Open(_files, mine.Chapters, NullLogger<StoryChapterArchive>.Instance);
        _book = new AdventureBook(_adventures, NullLogger<AdventureBook>.Instance);
        _book.CatchUp([]);
        _director = new StoryDirector(
            _stories,
            _book,
            () => StoryFixtures.Catalog,
            (_, _, _) => _write.Task,
            () => null,
            backstory => _backstory = backstory,
            NullLogger.Instance)
        {
            Archive = _archive,
        };
        _bench = Bench();
    }

    private StoryBench Bench() => new(Data, _files, _stories, _adventures, _archive, NullLogger<StoryBench>.Instance)
    {
        Director = _director,
        SetBackstory = backstory => _backstory = backstory,
    };

    private static Story Running(string id) => new() { Id = id, Title = id, PublicLayer = "-", PickedAt = Now.AddDays(-10) };

    private static string Written(Story? story) => JsonSerializer.Serialize(story);

    [Fact]
    public void ARoundTripAddsOneClosedOffSpanAndNothingElse()
    {
        var running = Running(Id) with { CluesGiven = 2, Chapters = ["chapter-1"] };
        var off = Running(Other.Id) with { OffSpans = [new StoryOffSpan(Now.AddDays(-2), null)] };
        var ended = Running("an-old-story") with { State = StoryState.Ended, StoppedAt = Now.AddDays(-5) };

        _stories.Save("F1", running);
        _stories.Save("F1", ended);
        _stories.Save("F2", off);

        Assert.Null(_bench.Create("test"));
        Assert.Null(_bench.SwitchTo("test", "F1", Now));

        Assert.Empty(_stories.All());
        _stories.Save("F1", Running("a-bench-story"));

        Assert.Null(_bench.SwitchTo(null, "F1", Now.AddHours(3)));

        Assert.Equal(Written(running with { OffSpans = [new StoryOffSpan(Now, Now.AddHours(3))] }), Written(_stories.Find("F1", Id)));
        Assert.Equal(Written(ended), Written(_stories.Find("F1", "an-old-story")));
        Assert.Equal(Written(off), Written(_stories.Find("F2", Other.Id)));
        Assert.Null(_stories.Find("F1", "a-bench-story"));
    }

    [Fact]
    public void ReturningToABenchSlotSwitchesItsStoryBackOn()
    {
        Assert.Null(_bench.Create("test"));
        Assert.Null(_bench.SwitchTo("test", "F1", Now));
        _stories.Save("F1", Running(Id));

        Assert.Null(_bench.SwitchTo(null, "F1", Now.AddHours(1)));
        Assert.Null(_bench.SwitchTo("test", "F1", Now.AddHours(2)));

        var story = _stories.Current("F1")!;
        Assert.False(story.IsOff);
        Assert.Equal([new StoryOffSpan(Now.AddHours(1), Now.AddHours(2))], story.OffSpans);
    }

    [Fact]
    public void TheStoresReadTheIncomingSlotsFilesAndSaySo()
    {
        _adventures.Save("F1", AdventureFixtures.LanternRoute(Now));
        _archive.Append(new ArchivedChapter("F1", Id, Now, AdventureFixtures.LanternRoute(Now), [], []));
        _stories.Save("F1", Running(Id));

        var changed = new List<string>();
        _stories.Changed += () => changed.Add("stories");
        _adventures.Changed += () => changed.Add("adventures");
        _archive.Changed += () => changed.Add("archive");

        Assert.Null(_bench.Create("test"));
        Assert.Null(_bench.SwitchTo("test", "F1", Now));

        Assert.Empty(_adventures.For("F1"));
        Assert.Empty(_archive.For("F1"));
        Assert.Null(_stories.Current("F1"));
        Assert.Contains("stories", changed);
        Assert.Contains("adventures", changed);
        Assert.Contains("archive", changed);

        _adventures.Save("F1", AdventureFixtures.LanternRoute(Now) with { Key = "a-bench-adventure" });
        Assert.Null(_bench.SwitchTo(null, "F1", Now.AddHours(1)));

        Assert.Equal(["the-lantern-route"], _adventures.For("F1").Select(adventure => adventure.Key));
        Assert.Single(_archive.For("F1"));
        Assert.NotNull(_stories.Current("F1"));
    }

    [Fact]
    public void ASwitchOwesAFreshWalkOverTheJournals()
    {
        Assert.False(_book.NeedsCatchUp);
        Assert.Null(_bench.Create("test"));

        Assert.Null(_bench.SwitchTo("test", "F1", Now));

        Assert.True(_book.NeedsCatchUp);
    }

    [Fact]
    public void TheBackstoryFollowsTheIncomingStory()
    {
        Assert.Null(_bench.Create("test"));
        Assert.Null(_bench.SwitchTo("test", "F1", Now));
        _stories.Save("F1", Running(Id));

        Assert.Null(_bench.SwitchTo(null, "F1", Now.AddHours(1)));
        Assert.Null(_backstory);

        Assert.Null(_bench.SwitchTo("test", "F1", Now.AddHours(2)));
        Assert.Equal(Card.InYourWords, _backstory);
    }

    [Fact]
    public async Task ASwitchWhileAChapterIsWrittenIsRefusedAndChangesNothing()
    {
        var picking = _director.PickAsync("F1", Id, Now, CancellationToken.None);
        Assert.True(_director.IsWriting("F1"));
        Assert.Null(_bench.Create("test"));

        Assert.Equal(StoryBench.Writing, _bench.SwitchTo("test", "F1", Now.AddMinutes(1)));

        Assert.Null(_bench.Active);
        Assert.False(_stories.Current("F1")!.IsOff);

        _write.SetResult(new AdventureOutcome(null, null, "No.", []));
        await picking;
    }

    [Fact]
    public void TheTickLeavesTheStoriesAloneWhileTheFilesAreSwapped()
    {
        _stories.Save("F1", Running(Id) with { Chapters = ["not-on-file"] });

        Assert.True(_director.HoldForSwitch());
        Assert.Null(_director.Tick("F1", Now));
        Assert.Equal(StoryState.Running, _stories.Current("F1")!.State);

        _director.ReleaseSwitch();
        Assert.Null(_director.Tick("F1", Now));
        Assert.Equal(StoryState.Paused, _stories.Current("F1")!.State);
    }

    [Fact]
    public void TheActiveSlotIsKeptForTheNextStartOnlyWhileTheBenchIsOn()
    {
        Assert.Null(_bench.Create("test"));
        Assert.Null(_bench.SwitchTo("test", "F1", Now));

        Assert.Equal(StorySlotFiles.In(Data), StoryBench.FilesFor(Data, enabled: false, _files));
        Assert.Equal(StorySlotFiles.In(Path.Combine(Data, StoryBench.Folder, "test")), StoryBench.FilesFor(Data, enabled: true, _files));
        Assert.Equal("test", Bench().Active);
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("a\\b")]
    [InlineData("My story")]
    [InlineData("Mine")]
    [InlineData("con")]
    [InlineData("test")]
    public void ASlotNameThatIsNotAPlainNewFolderIsRefused(string name)
    {
        Assert.Null(_bench.Create("test"));

        Assert.NotNull(_bench.Create(name));
        Assert.Equal(["test"], _bench.Slots);
    }
}
