using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

[Trait("Category", "Integration")]
public sealed class ADownloadedStoryIsOfferedTests
{
    [Fact]
    public void TheCardIsListedAndItsHiddenEntryIsRead()
    {
        using var folder = new DownloadedStoryFolder();
        folder.WriteIndex(StoryFixtures.Card);
        folder.WriteSealed(StoryFixtures.Secret);

        var catalog = StoryCatalog.Load(folder.Path);

        Assert.Equal(StoryFixtures.Card.Id, Assert.Single(catalog.Cards).Id);
        Assert.Equal(StoryFixtures.Secret.Secret, catalog.Secret(StoryFixtures.Card.Id)?.Secret);
    }

    [Fact]
    public void AMissingFolderOrIndexMeansNoStories()
    {
        Assert.Empty(StoryCatalog.Load(Path.Combine(Path.GetTempPath(), "d47-no-such-" + Guid.NewGuid().ToString("N"))).Cards);

        using var folder = new DownloadedStoryFolder();
        Assert.Empty(StoryCatalog.Load(folder.Path).Cards);

        File.WriteAllText(Path.Combine(folder.Path, StoryCatalog.IndexFile), "{ not json");
        Assert.Empty(StoryCatalog.Load(folder.Path).Cards);
    }

    [Fact]
    public void ASealedFileWithNoCardIsIgnored()
    {
        using var folder = new DownloadedStoryFolder();
        folder.WriteIndex(StoryFixtures.Card);
        folder.WriteSealed(StoryFixtures.Secret with { Id = "orphan" });

        var catalog = StoryCatalog.Load(folder.Path);

        Assert.Null(catalog.Secret("orphan"));
        Assert.Empty(catalog.Secrets);
    }
}
