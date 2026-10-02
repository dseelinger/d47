using D47.Core.Stories;
using Xunit;

namespace D47.App.Tests.Stories;

public sealed class TheStoryListFetchesTheCastPicturesItNamesTests
{
    private static readonly StoryCard Card = StoryFixture.Story with
    {
        CastPictures = ["the-test-story.ren", "the-test-story.cray.for-man", "the-test-story.cray.for-woman", "the-test-story.missing", "../escape"],
    };

    [Fact]
    public async Task OnlyTheNamedPicturesAreFetchedAndNotTheHiddenLayer()
    {
        using var release = new StoryRelease();
        release.ServeIndex(Card);
        release.Serve("the-test-story.ren.jpg", [1]);
        release.Serve("the-test-story.cray.for-man.jpg", [2]);
        release.Serve("the-test-story.cray.for-woman.jpg", [3]);
        var downloader = release.Downloader();
        var landed = 0;
        downloader.Landed += () => Interlocked.Increment(ref landed);

        await downloader.AskForList();

        Assert.Equal(1, landed);
        Assert.True(File.Exists(Path.Combine(release.Folder, "the-test-story.ren.jpg")));
        Assert.True(File.Exists(Path.Combine(release.Folder, "the-test-story.cray.for-man.jpg")));
        Assert.True(File.Exists(Path.Combine(release.Folder, "the-test-story.cray.for-woman.jpg")));
        Assert.False(File.Exists(Path.Combine(release.Folder, "the-test-story.missing.jpg")));
        Assert.DoesNotContain("the-test-story.sealed", release.Asked);
        Assert.DoesNotContain(release.Asked, file => file.Contains("escape", StringComparison.Ordinal));
    }

    [Fact]
    public async Task APictureAlreadyOnDiskIsNotAskedForAgain()
    {
        using var release = new StoryRelease();
        release.ServeIndex(Card);
        release.Serve("the-test-story.ren.jpg", [1]);
        await File.WriteAllBytesAsync(Path.Combine(release.Folder, "the-test-story.ren.jpg"), [9], TestContext.Current.CancellationToken);

        await release.Downloader().AskForList();

        Assert.DoesNotContain("the-test-story.ren.jpg", release.Asked);
    }

    [Fact]
    public async Task WithDownloadsOffNothingIsAskedFor()
    {
        using var release = new StoryRelease();
        release.ServeIndex(Card);

        await release.Downloader(allowed: false).AskForList();

        Assert.Empty(release.Asked);
    }
}
