using Xunit;

namespace D47.App.Tests.Stories;

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
}
