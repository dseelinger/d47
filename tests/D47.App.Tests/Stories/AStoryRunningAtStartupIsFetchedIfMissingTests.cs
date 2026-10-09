using Xunit;

namespace D47.App.Tests.Stories;

[Trait("Category", "Integration")]
public sealed class AStoryRunningAtStartupIsFetchedIfMissingTests
{
    [Fact]
    public async Task AStoryWithNoHiddenLayerOnDiskIsFetched()
    {
        using var release = new StoryRelease();
        StoryReleaseFixture.ServeAll(release);
        var downloader = release.Downloader();

        await downloader.FetchMissing([StoryReleaseFixture.Id]);

        Assert.True(downloader.IsOnDisk(StoryReleaseFixture.Id));
    }

    [Fact]
    public async Task AStoryAlreadyOnDiskIsNotAskedFor()
    {
        using var release = new StoryRelease();
        StoryReleaseFixture.ServeAll(release);
        var downloader = release.Downloader();
        await downloader.FetchMissing([StoryReleaseFixture.Id]);
        var before = release.Asked.Count;

        await downloader.FetchMissing([StoryReleaseFixture.Id]);

        Assert.Equal(before, release.Asked.Count);
    }

    [Fact]
    public async Task APictureAddedToTheReleaseLaterIsFetchedForAStoryOnDisk()
    {
        using var release = new StoryRelease();
        StoryReleaseFixture.ServeAll(release);
        var downloader = release.Downloader();
        await downloader.FetchMissing([StoryReleaseFixture.Id]);
        var added = Path.Combine(release.Folder, StoryReleaseFixture.Pictures[0]);
        File.Delete(added);

        await downloader.FetchMissing([StoryReleaseFixture.Id]);

        Assert.True(File.Exists(added));
    }
}
