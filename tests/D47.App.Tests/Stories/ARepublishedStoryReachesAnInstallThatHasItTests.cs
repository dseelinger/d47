using System.Security.Cryptography;
using D47.Core.Stories;
using Xunit;

namespace D47.App.Tests.Stories;

/// <summary>
/// A story on disk is fetched again when the list's card carries a <c>sealedSha256</c> that its file does not match,
/// and only then (#1600).
/// </summary>
[Trait("Category", "Integration")]
public sealed class ARepublishedStoryReachesAnInstallThatHasItTests
{
    private static readonly string Id = StoryReleaseFixture.Id;

    private static readonly string SealedFile = Id + StoryCatalog.SealedExtension;

    private static readonly byte[] Old = StoryRelease.Sealed(StoryReleaseFixture.Secret with { Secret = "The first telling." });

    private static readonly byte[] New = StoryRelease.Sealed(StoryReleaseFixture.Secret);

    [Fact]
    public async Task AStoryWhoseCardHashDiffersIsReplaced()
    {
        using var release = Holding(Hash(New));
        var landed = 0;
        var downloader = release.Downloader();
        downloader.Landed += () => Interlocked.Increment(ref landed);

        await downloader.AskForList();

        Assert.Equal(New, File.ReadAllBytes(Path.Combine(release.Folder, SealedFile)));
        Assert.Contains(SealedFile, release.Asked);
        Assert.DoesNotContain(release.Asked, file => file.EndsWith(".jpg", StringComparison.Ordinal));
        Assert.True(landed >= 1);
    }

    [Fact]
    public async Task AStoryWhoseHashMatchesIsNotFetched()
    {
        using var release = Holding(Hash(Old));

        await release.Downloader().AskForList();

        Assert.DoesNotContain(SealedFile, release.Asked);
        Assert.Equal(Old, File.ReadAllBytes(Path.Combine(release.Folder, SealedFile)));
    }

    [Fact]
    public async Task AStoryWhoseCardHasNoHashIsNotFetched()
    {
        using var release = Holding(null);

        await release.Downloader().AskForList();

        Assert.DoesNotContain(SealedFile, release.Asked);
        Assert.Equal(Old, File.ReadAllBytes(Path.Combine(release.Folder, SealedFile)));
    }

    [Fact]
    public async Task AFailedFetchLeavesTheOldFileInPlace()
    {
        using var release = Holding(Hash(New));
        release.Fail(SealedFile);

        await release.Downloader().AskForList();

        Assert.Contains(SealedFile, release.Asked);
        Assert.Equal(Old, File.ReadAllBytes(Path.Combine(release.Folder, SealedFile)));
        Assert.False(File.Exists(Path.Combine(release.Folder, SealedFile + ".part")));
    }

    /// <summary>A release serving the new story under a card with <paramref name="hash"/>, and an install holding the old one and every picture.</summary>
    private static StoryRelease Holding(string? hash)
    {
        var release = new StoryRelease();
        release.ServeIndex(StoryFixture.Story with { SealedSha256 = hash });
        release.Serve(SealedFile, New);

        File.WriteAllBytes(Path.Combine(release.Folder, SealedFile), Old);

        foreach (var picture in StoryReleaseFixture.Pictures)
        {
            release.Serve(picture, [1, 2, 3]);
            File.WriteAllBytes(Path.Combine(release.Folder, picture), [1, 2, 3]);
        }

        return release;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
