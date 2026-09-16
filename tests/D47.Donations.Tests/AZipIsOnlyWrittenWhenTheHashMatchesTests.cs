using System.IO.Compression;
using System.Security.Cryptography;
using D47.Donations.Store;
using Xunit;

namespace D47.Donations.Tests;

/// <summary>
/// The stored SHA-256 is over the decompressed payload, so it is the one check that proves the object
/// arrived whole and was unpacked exactly once.
/// </summary>
public class AZipIsOnlyWrittenWhenTheHashMatchesTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-donations-tests", Guid.NewGuid().ToString("n"));

    private readonly DownloadFolder _downloads;
    private readonly DonationKey _key;

    public AZipIsOnlyWrittenWhenTheHashMatchesTests()
    {
        _downloads = new DownloadFolder(_folder);

        Assert.True(DonationKey.TryParse(
            "excerpts/0123456789abcdef0123456789abcdef/20260914T100926Z-aabbccddeeff0011.md.gz",
            out var key));

        _key = key;
    }

    [Fact]
    public void AMatchingHashWritesOneZipHoldingThePayload()
    {
        var payload = "# an excerpt"u8.ToArray();

        var result = _downloads.Save(_key, payload, Convert.ToHexStringLower(SHA256.HashData(payload)));

        Assert.True(result.Written);
        Assert.Null(result.Problem);

        using var zip = ZipFile.OpenRead(Path.Combine(_folder, _key.ZipName));
        var entry = Assert.Single(zip.Entries);

        Assert.Equal(_key.EntryName, entry.FullName);

        using var held = new MemoryStream();
        using (var stream = entry.Open())
        {
            stream.CopyTo(held);
        }

        Assert.Equal(payload, held.ToArray());
    }

    [Fact]
    public void AMismatchedHashWritesNothingAndSaysSo()
    {
        var result = _downloads.Save(_key, "# an excerpt"u8.ToArray(), new string('0', 64));

        Assert.False(result.Written);
        Assert.False(File.Exists(Path.Combine(_folder, _key.ZipName)));
        Assert.Contains("mismatch", result.Problem!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnObjectCarryingNoHashIsNotTreatedAsAMatch()
    {
        var result = _downloads.Save(_key, "# an excerpt"u8.ToArray(), expectedSha256: null);

        Assert.False(result.Written);
        Assert.False(File.Exists(Path.Combine(_folder, _key.ZipName)));
    }

    [Fact]
    public void APayloadSurvivesBeingUnpackedOnce()
    {
        var payload = "# an excerpt, long enough to compress"u8.ToArray();

        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(payload);
        }

        Assert.Equal(payload, DownloadFolder.Decompress(compressed.ToArray()));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }
        catch (IOException)
        {
        }

        GC.SuppressFinalize(this);
    }
}
