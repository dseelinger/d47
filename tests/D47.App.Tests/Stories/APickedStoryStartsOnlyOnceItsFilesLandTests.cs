using D47.Core.Stories;
using Xunit;

namespace D47.App.Tests.Stories;

public sealed class APickedStoryStartsOnlyOnceItsFilesLandTests
{
    [Fact]
    public async Task TheStoryIsNotReadyWhileTheHiddenLayerIsHeldAndIsReadyOnceEveryFileIsWritten()
    {
        using var release = new StoryRelease();
        StoryReleaseFixture.ServeAll(release);
        var gate = new TaskCompletionSource();
        release.Hold = (StoryCatalog.SealedExtension, gate.Task);
        var downloader = release.Downloader();
        var landed = 0;
        downloader.Landed += () => Interlocked.Increment(ref landed);

        var fetch = downloader.FetchStory(StoryReleaseFixture.Id);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.False(fetch.IsCompleted);
        Assert.False(downloader.IsOnDisk(StoryReleaseFixture.Id));
        Assert.Equal(0, landed);

        gate.SetResult();

        Assert.True(await fetch);
        Assert.True(downloader.IsOnDisk(StoryReleaseFixture.Id));
        Assert.Equal(1, landed);

        foreach (var picture in StoryReleaseFixture.Pictures)
        {
            Assert.True(File.Exists(Path.Combine(release.Folder, picture)), picture);
        }

        Assert.Empty(Directory.GetFiles(release.Folder, "*.part"));
    }

    [Fact]
    public async Task APictureTheReleaseDoesNotHaveIsSkipped()
    {
        using var release = new StoryRelease();
        release.ServeSealed(StoryReleaseFixture.Secret);
        release.Serve("the-test-story.ren.jpg", [1]);
        var downloader = release.Downloader();

        Assert.True(await downloader.FetchStory(StoryReleaseFixture.Id));
        Assert.True(downloader.IsOnDisk(StoryReleaseFixture.Id));
        Assert.Empty(Directory.GetFiles(release.Folder, "*.part"));
    }

    [Fact]
    public async Task APictureThatFailsToDownloadFailsTheStoryAndLeavesNoHiddenLayerOnDisk()
    {
        using var release = new StoryRelease();
        StoryReleaseFixture.ServeAll(release);
        release.Fail("the-test-story.cray.for-man.jpg");
        var downloader = release.Downloader();

        Assert.False(await downloader.FetchStory(StoryReleaseFixture.Id));
        Assert.False(downloader.IsOnDisk(StoryReleaseFixture.Id));
        Assert.Empty(Directory.GetFiles(release.Folder, "*.part"));
    }

    [Fact]
    public async Task TheSameStoryPickedTwiceIsFetchedOnce()
    {
        using var release = new StoryRelease();
        StoryReleaseFixture.ServeAll(release);
        var gate = new TaskCompletionSource();
        release.Hold = (StoryCatalog.SealedExtension, gate.Task);
        var downloader = release.Downloader();

        var first = downloader.FetchStory(StoryReleaseFixture.Id);
        var second = downloader.FetchStory(StoryReleaseFixture.Id);
        gate.SetResult();

        Assert.True(await first);
        Assert.True(await second);
        Assert.Equal(1, release.Asked.Count(file => file == StoryReleaseFixture.Id + StoryCatalog.SealedExtension));
    }

    [Fact]
    public async Task AnIdThatIsNotAFileNameIsNeverRequested()
    {
        using var release = new StoryRelease();
        var downloader = release.Downloader();

        Assert.False(await downloader.FetchStory("../outside"));
        Assert.Empty(release.Asked);
    }
}
