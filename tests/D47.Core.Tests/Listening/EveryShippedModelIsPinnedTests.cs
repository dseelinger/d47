using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// The Whisper model hashes are pinned in the repository rather than taken from whatever the host says
/// on the day.
/// </summary>
public class EveryShippedModelIsPinnedTests
{
    /// <summary>
    /// A model arriving unpinned is allowed by the code and refused here, so that shipping one is a
    /// decision somebody took rather than an omission nobody noticed.
    /// </summary>
    [Fact]
    public void EveryModelCarriesAPinnedHash()
    {
        var unpinned = WhisperModels.All
            .Where(model => string.IsNullOrWhiteSpace(model.Sha256))
            .Select(model => model.Id)
            .ToArray();

        Assert.True(unpinned.Length == 0, "Models with no pinned hash: " + string.Join(", ", unpinned));
    }

    /// <summary>A SHA-256 is sixty-four hex characters.</summary>
    [Fact]
    public void EachPinnedHashIsAWellFormedSha256()
    {
        foreach (var model in WhisperModels.All)
        {
            var hash = model.Sha256!;

            Assert.True(hash.Length == 64, $"{model.Id}: {hash.Length} characters, not 64");
            Assert.True(
                hash.All(Uri.IsHexDigit),
                $"{model.Id}: {hash} is not hex");
            Assert.Equal(hash.ToLowerInvariant(), hash);
        }
    }

    /// <summary>
    /// Two models sharing a hash would mean one of them is pinned to the other's file, which is a paste
    /// error that every other check here passes.
    /// </summary>
    [Fact]
    public void NoTwoModelsArePinnedToTheSameFile()
    {
        var shared = WhisperModels.All
            .GroupBy(model => model.Sha256, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" and ", group.Select(model => model.Id)))
            .ToArray();

        Assert.True(shared.Length == 0, "Models pinned to the same hash: " + string.Join("; ", shared));
    }

    /// <summary>The listing has to be asked for the blocks that carry the hash.</summary>
    [Fact]
    public void TheMetadataUrlAsksForTheBlobs()
    {
        Assert.Contains("blobs=true", WhisperModels.MetadataUrl(), StringComparison.Ordinal);
    }
}
