using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// The Whisper model hashes are pinned in the repository rather than taken from whatever the host
/// says on the day (#124).
/// <para>
/// <b>What the pin is and is not.</b> The values were read from Hugging Face on 2026-08-28 and are
/// immutable from that moment, so they are not an independent attestation and do not make that
/// first read trustworthy. What they buy is that the file <em>changing</em> becomes visible: before
/// this, the expected hash and the bytes came from the same server, so anything able to serve
/// different bytes could serve the hash for them. It matters here because the file is loaded and
/// executed in-process by the native runtime.
/// </para>
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

    /// <summary>
    /// A SHA-256 is sixty-four hex characters. Cheap, and it catches the paste that lost a character
    /// or picked up the surrounding quotes — which would fail closed on every download, for every
    /// Commander, with a message about the file not being what d47 expects.
    /// </summary>
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
    /// Two models sharing a hash would mean one of them is pinned to the other's file, which is a
    /// paste error that every other check here passes.
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

    /// <summary>
    /// <b>The listing has to be asked for the blocks that carry the hash.</b> The plain listing
    /// returns one key per file and no <c>lfs</c> block at all, so the size read from it was always
    /// 0 and the hash always null — which meant no downloaded model was ever verified against
    /// anything, and every model was offered to the Commander as "0 MB". Found while pinning these.
    /// </summary>
    [Fact]
    public void TheMetadataUrlAsksForTheBlobs()
    {
        Assert.Contains("blobs=true", WhisperModels.MetadataUrl(), StringComparison.Ordinal);
    }
}
