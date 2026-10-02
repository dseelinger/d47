using D47.App.Panel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Stories;

public sealed class TurningStoryDownloadsOffFetchesNothingTests
{
    [Fact]
    public async Task NeitherTheListNorAStoryNorARunningStoryIsAskedFor()
    {
        using var release = new StoryRelease();
        StoryReleaseFixture.ServeAll(release);
        var downloader = release.Downloader(allowed: false);

        await downloader.AskForList();
        await downloader.FetchMissing([StoryReleaseFixture.Id]);

        Assert.False(await downloader.FetchStory(StoryReleaseFixture.Id));
        Assert.Empty(release.Asked);
        Assert.False(downloader.Enabled);
    }

    [Fact]
    public async Task TurningItOnLaterInTheSessionStillAsksForTheList()
    {
        using var release = new StoryRelease();
        StoryReleaseFixture.ServeAll(release);
        var on = false;
        var downloader = new StoryDownloader(release.Folder, () => on, NullLogger.Instance, release);

        await downloader.AskForList();
        on = true;
        await downloader.AskForList();

        Assert.Single(release.Asked);
    }
}
