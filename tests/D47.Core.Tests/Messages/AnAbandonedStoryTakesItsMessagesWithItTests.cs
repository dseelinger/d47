using D47.Core.Storage;
using D47.Core.Adventures;
using D47.Core.Messages;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Messages;

[Trait("Category", "Integration")]
public class AnAbandonedStoryTakesItsMessagesWithItTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-story-message-orphans", Guid.NewGuid().ToString("N"));

    private readonly AdventureStore _adventures;
    private readonly MessageStore _messages;
    private readonly StoryStore _stories = StoryStore.InMemory();

    public AnAbandonedStoryTakesItsMessagesWithItTests()
    {
        Directory.CreateDirectory(_folder);
        _adventures = new AdventureStore(Path.Combine(_folder, "adventures.json"), NullLogger<AdventureStore>.Instance);
        _messages = new MessageStore(Path.Combine(_folder, "messages.json"), new MemoryFileSystem(), NullLogger<MessageStore>.Instance);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static string Id => StoryFixtures.Id;

    private int Sweep() =>
        _messages.RemoveOrphans(key => MessageOwnership.Owned(key, _adventures, _stories, () => StoryFixtures.Catalog));

    private void Pick(StoryState state, params string[] chapters) =>
        _stories.Save("F1", new Story
        {
            Id = Id,
            Title = "The Test Story",
            PublicLayer = "x",
            Chapters = chapters,
            State = state,
            PickedAt = Noon,
        });

    private void PostEverything(string chapter)
    {
        _messages.Post("narrator", "beat", "x", Noon, chapter);
        _messages.Post("narrator", "clue", "x", Noon, $"{StoryClueCallout.KeyPrefix}{Id}.0");
        _messages.Post("narrator", "end", "x", Noon, StoryEnding.Key(Id));
        _messages.Post("narrator", "scan", "x", Noon, StoryLines.Key(Id));
    }

    private void SaveChapter(string key) =>
        Assert.Null(_adventures.Save("F1", AdventureFixtures.LanternRoute() with { Key = key, StoryId = Id }));

    [Theory]
    [InlineData(StoryState.Ended)]
    [InlineData(StoryState.Abandoned)]
    public void AnAbandonedOrSwitchedStoryLosesItsMessagesAndNothingElse(StoryState state)
    {
        SaveChapter("chapter-1");
        _adventures.Save("F1", AdventureFixtures.LanternRoute());
        Pick(StoryState.Running, "chapter-1");
        PostEverything("chapter-1");
        _messages.Post("narrator", "own", "x", Noon, "the-lantern-route");
        _messages.Post("narrator", "unkeyed", "x", Noon);

        Assert.Equal(0, Sweep());

        Pick(state, "chapter-1");

        Assert.Equal(4, Sweep());
        Assert.Equal(["own", "unkeyed"], _messages.All.Select(message => message.Subject).Order());
    }

    [Fact]
    public void AFinishedStoryKeepsItsMessages()
    {
        SaveChapter("chapter-1");
        Pick(StoryState.Finished, "chapter-1");
        PostEverything("chapter-1");

        Assert.Equal(0, Sweep());
        Assert.Equal(4, _messages.All.Count);
    }

    [Fact]
    public void ARunningStoryKeepsAListedChapterThatIsNoLongerOnFile()
    {
        Pick(StoryState.Running, "chapter-4");
        _messages.Post("narrator", "beat", "x", Noon, "chapter-4");

        Assert.Equal(0, Sweep());
    }

    [Fact]
    public void ARunningStoryLosesTheMessagesOfAChapterItNoLongerListsOnceAnotherPickReplacesIt()
    {
        SaveChapter("chapter-1");
        SaveChapter("chapter-2");
        Pick(StoryState.Running, "chapter-2");
        _messages.Post("narrator", "old", "x", Noon, "chapter-1");
        _messages.Post("narrator", "new", "x", Noon, "chapter-2");

        Assert.Equal(1, Sweep());
        Assert.Equal("new", Assert.Single(_messages.All).Subject);
    }

    [Fact]
    public void AStoryOutsideTheCatalogLosesItsMessages()
    {
        _stories.Save("F1", new Story { Id = "retired", Title = "Retired", PublicLayer = "x", Chapters = ["gone"], PickedAt = Noon });
        _messages.Post("narrator", "clue", "x", Noon, $"{StoryClueCallout.KeyPrefix}retired.0");
        _messages.Post("narrator", "beat", "x", Noon, "gone");

        Assert.Equal(2, Sweep());
        Assert.Empty(_messages.All);
    }

    [Fact]
    public void FourAbandonedChaptersAndTheirMessagesAreGoneAtStartup()
    {
        string[] chapters = ["my-own-mayday", "my-own-mayday-2", "my-own-mayday-3", "my-own-mayday-4"];

        foreach (var chapter in chapters)
        {
            SaveChapter(chapter);
            _messages.Post("covas", "beat", "x", Noon, chapter);
        }

        Pick(StoryState.Ended, "my-own-mayday-4");

        Assert.Equal(4, Sweep());
        Assert.Empty(_messages.All);
    }
}
