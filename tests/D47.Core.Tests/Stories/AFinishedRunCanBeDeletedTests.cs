using D47.Core.Adventures;
using D47.Core.Storage;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

public sealed class AFinishedRunCanBeDeletedTests
{
    private const string Commander = "F1";

    private static readonly string Data = Path.Combine(MemoryInstall.FakeRoot, "delete-run", "data");

    private readonly MemoryFileSystem _files = new();
    private readonly StoryStore _stories;
    private readonly StoryChapterArchive _archive;
    private readonly StoryRunRecord _records;
    private readonly StoryDirector _director;

    public AFinishedRunCanBeDeletedTests()
    {
        _stories = StoryStore.Open(Path.Combine(Data, "story.json"), _files, NullLogger<StoryStore>.Instance);
        _archive = StoryChapterArchive.Open(_files, Path.Combine(Data, "story-chapters.jsonl"), NullLogger<StoryChapterArchive>.Instance);
        _records = new StoryRunRecord(_files, Path.Combine(Data, "story-runs"), _stories, () => Commander, null, new ReversibleProtector(), NullLogger.Instance);

        var book = new AdventureBook(new AdventureStore(Path.Combine(Data, "adventures.json"), _files, NullLogger<AdventureStore>.Instance), NullLogger<AdventureBook>.Instance);

        book.CatchUp([]);
        _director = new StoryDirector(_stories, book, () => StoryFixtures.Catalog, (_, _, _) => new TaskCompletionSource<AdventureOutcome>().Task, () => null, _ => { }, NullLogger.Instance)
        {
            Archive = _archive,
            Records = _records,
            Clock = () => Now,
        };
    }

    private static Story Run(DateTimeOffset at, StoryState state) => new()
    {
        Id = Id,
        Title = Card.Title,
        PublicLayer = "-",
        PickedAt = at,
        State = state,
        StoppedAt = state is StoryState.Running or StoryState.Paused or StoryState.Shelved ? null : at.AddDays(1),
        Chapters = ["chapter-1"],
    };

    private Story Keep(DateTimeOffset at, StoryState state)
    {
        var run = Run(at, state);

        _stories.Save(Commander, run);
        _files.WriteText(Path.Combine(Data, "story-runs", StoryRunRecord.FolderName(Commander, Id, at), StoryRunRecord.LinesFile), "{}\n");
        _files.WriteBytes(Path.Combine(Data, "story-runs", StoryRunRecord.FolderName(Commander, Id, at), "clip.wav"), new byte[10]);
        _archive.Append(new ArchivedChapter(Commander, Id, at, AdventureFixtures.LanternRoute(at) with { Key = "chapter-1", StoryId = Id }, [], []));

        return run;
    }

    private bool HasFolder(DateTimeOffset at) =>
        _files.Enumerate(Path.Combine(Data, "story-runs", StoryRunRecord.FolderName(Commander, Id, at)), "*").Count > 0;

    [Theory]
    [InlineData(StoryState.Finished)]
    [InlineData(StoryState.Abandoned)]
    [InlineData(StoryState.Ended)]
    public void DeletingARunRemovesItsRecordFolderAndArchivedChapters(StoryState state)
    {
        var run = Keep(Now, state);

        Assert.Null(_director.DeleteRun(Commander, run));

        Assert.Empty(_stories.For(Commander));
        Assert.False(HasFolder(Now));
        Assert.Empty(_archive.For(Commander));
    }

    [Fact]
    public void AnotherRunOfTheSameStoryIsLeftUntouched()
    {
        var other = Keep(Now.AddDays(-30), StoryState.Finished);
        var run = Keep(Now, StoryState.Abandoned);

        Assert.Null(_director.DeleteRun(Commander, run));

        Assert.Equal(other.PickedAt, Assert.Single(_stories.For(Commander)).PickedAt);
        Assert.True(HasFolder(other.PickedAt));
        Assert.Equal(other.PickedAt, Assert.Single(_archive.For(Commander)).PickedAt);
    }

    [Fact]
    public void TheActiveRunIsRefusedAndKept()
    {
        var run = Keep(Now, StoryState.Running);

        Assert.Equal(StoryDirector.ActiveRunRefusal, _director.DeleteRun(Commander, run));
        Assert.Single(_stories.For(Commander));
        Assert.True(HasFolder(Now));
    }

    [Fact]
    public void AShelvedRunIsRefusedUntilItIsAbandoned()
    {
        var run = Keep(Now, StoryState.Shelved);

        Assert.Equal(StoryDirector.ShelvedRunRefusal, _director.DeleteRun(Commander, run));
        Assert.Single(_stories.For(Commander));

        Assert.Null(_director.AbandonShelved(Commander, run, Now.AddDays(2)));
        Assert.Equal(StoryState.Abandoned, Assert.Single(_stories.For(Commander)).State);

        Assert.Null(_director.DeleteRun(Commander, _stories.For(Commander)[0]));
        Assert.Empty(_stories.For(Commander));
    }

    [Fact]
    public void OnlyAShelvedRunCanBeAbandonedThisWay()
    {
        var run = Keep(Now, StoryState.Finished);

        Assert.NotNull(_director.AbandonShelved(Commander, run, Now));
        Assert.Equal(StoryState.Finished, Assert.Single(_stories.For(Commander)).State);
    }
}
