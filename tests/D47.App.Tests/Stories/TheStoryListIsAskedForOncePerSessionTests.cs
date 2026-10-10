using D47.Core.Stories;
using D47.Core.Storage;
using Xunit;

namespace D47.App.Tests.Stories;

[Trait("Category", "Integration")]
public sealed class TheStoryListIsAskedForOncePerSessionTests
{
    [Fact]
    public async Task TheListIsFetchedOnceHoweverOftenThePageOpens()
    {
        using var release = new StoryRelease();
        StoryReleaseFixture.ServeAll(release);
        var downloader = release.Downloader();
        var landed = 0;
        downloader.Landed += () => Interlocked.Increment(ref landed);

        await downloader.AskForList();
        await downloader.AskForList();
        await downloader.AskForList();

        Assert.Equal(1, release.Asked.Count(file => file == StoryCatalog.IndexFile));
        Assert.Equal(1, landed);
        Assert.Equal(StoryFixture.Story.Id, Assert.Single(StoryCatalog.Load(new DiskFileSystem(), release.Folder).Cards).Id);
        Assert.Empty(Directory.GetFiles(release.Folder, "*.part"));
    }

    [Fact]
    public async Task AListThatIsNotAListLeavesTheCopyOnDisk()
    {
        using var release = new StoryRelease();
        release.ServeIndex(StoryFixture.Story);
        await release.Downloader().AskForList();

        release.Serve(StoryCatalog.IndexFile, "not a list"u8.ToArray());
        await release.Downloader().AskForList();

        Assert.Equal(StoryFixture.Story.Id, Assert.Single(StoryCatalog.Load(new DiskFileSystem(), release.Folder).Cards).Id);
        Assert.Empty(Directory.GetFiles(release.Folder, "*.part"));
    }
}
