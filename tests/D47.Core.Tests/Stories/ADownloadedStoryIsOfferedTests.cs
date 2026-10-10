using D47.Core.Stories;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class ADownloadedStoryIsOfferedTests
{
    [Fact]
    public void TheCardIsListedAndItsHiddenEntryIsRead()
    {
        var folder = new DownloadedStoryFolder();
        folder.WriteIndex(StoryFixtures.Card);
        folder.WriteSealed(StoryFixtures.Secret);

        var catalog = StoryCatalog.Load(folder.Files, folder.Path);

        Assert.Equal(StoryFixtures.Card.Id, Assert.Single(catalog.Cards).Id);
        Assert.Equal(StoryFixtures.Secret.Secret, catalog.Secret(StoryFixtures.Card.Id)?.Secret);
    }

    [Fact]
    public void AMissingFolderOrIndexMeansNoStories()
    {
        Assert.Empty(StoryCatalog.Load(new MemoryFileSystem(), @"C:\d47-test\no-such-folder").Cards);

        var folder = new DownloadedStoryFolder();
        Assert.Empty(StoryCatalog.Load(folder.Files, folder.Path).Cards);

        folder.Files.WriteText(Path.Combine(folder.Path, StoryCatalog.IndexFile), "{ not json");
        Assert.Empty(StoryCatalog.Load(folder.Files, folder.Path).Cards);
    }

    [Fact]
    public void ASealedFileWithNoCardIsIgnored()
    {
        var folder = new DownloadedStoryFolder();
        folder.WriteIndex(StoryFixtures.Card);
        folder.WriteSealed(StoryFixtures.Secret with { Id = "orphan" });

        var catalog = StoryCatalog.Load(folder.Files, folder.Path);

        Assert.Null(catalog.Secret("orphan"));
        Assert.Empty(catalog.Secrets);
    }
}
