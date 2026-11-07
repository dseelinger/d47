using D47.Core.Adventures;
using D47.Core.Storage;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A switch or No story deletes the bench checkpoints, and the story made active again takes a new one.</summary>
public sealed class AShelvedStoryClearsTheBenchCheckpointsTests
{
    private static readonly string Data = Path.Combine(MemoryInstall.FakeRoot, "shelve-bench", "data");

    private readonly MemoryFileSystem _files = new();
    private readonly StoryStore _stories;
    private readonly AdventureStore _adventures;
    private readonly StoryDirector _director;
    private readonly StoryBench _bench;

    public AShelvedStoryClearsTheBenchCheckpointsTests()
    {
        _stories = StoryStore.Open(Path.Combine(Data, "story.json"), _files, NullLogger<StoryStore>.Instance);
        _adventures = new AdventureStore(Path.Combine(Data, "adventures.json"), _files, NullLogger<AdventureStore>.Instance);

        var archive = StoryChapterArchive.Open(_files, Path.Combine(Data, "story-chapters.jsonl"), NullLogger<StoryChapterArchive>.Instance);
        var book = new AdventureBook(_adventures, NullLogger<AdventureBook>.Instance);

        book.CatchUp([]);
        _director = new StoryDirector(
            _stories, book, () => StoryFixtures.Catalog, (_, _, _) => new TaskCompletionSource<AdventureOutcome>().Task, () => null, _ => { }, NullLogger.Instance)
        {
            Archive = archive,
            Clock = () => Now,
        };
        _bench = new StoryBench(Data, _files, _stories, _adventures, archive, NullLogger<StoryBench>.Instance) { Director = _director };
    }

    private static string MetaOf(int chapter) =>
        Path.Combine(Data, StoryBench.Folder, "mine", "checkpoints", chapter.ToString(System.Globalization.CultureInfo.InvariantCulture), "checkpoint.json");

    private async Task WriteChapter(int number)
    {
        _adventures.Save("F1", AdventureFixtures.LanternRoute(Now) with { Key = $"chapter-{number}", StoryId = Id });

        var story = _stories.Current("F1") ?? new Story { Id = Id, Title = Card.Title, PublicLayer = "-", PickedAt = Now, BeaconScanAt = Now };

        _stories.Save("F1", story with { Chapters = [.. story.Chapters, $"chapter-{number}"] });
        await Wait.Until(() => _bench.Checkpoints("F1").Any(checkpoint => checkpoint.Chapter == number), $"chapter {number} never got a checkpoint");
    }

    [Fact]
    public async Task NoStoryDeletesTheCheckpointsAndComingBackTakesOneAgain()
    {
        await WriteChapter(1);
        await WriteChapter(2);
        Assert.Equal([1, 2], _bench.Checkpoints("F1").Select(checkpoint => checkpoint.Chapter));

        Assert.Null(_director.ShelveCurrent("F1", Now.AddHours(1)));
        await Wait.Until(() => _files.ReadText(MetaOf(1)) is null && _files.ReadText(MetaOf(2)) is null, "the checkpoints were never deleted");

        Assert.Null(_files.ReadText(MetaOf(1)));
        Assert.Null(_files.ReadText(MetaOf(2)));

        Assert.Null(await _director.PickAsync("F1", Id, Now.AddHours(2), CancellationToken.None));
        await Wait.Until(() => _bench.Checkpoints("F1").Select(checkpoint => checkpoint.Chapter).SequenceEqual([2]), "the checkpoint was never taken again");

        Assert.Equal([2], _bench.Checkpoints("F1").Select(checkpoint => checkpoint.Chapter));
        Assert.Null(_files.ReadText(MetaOf(1)));
    }
}
