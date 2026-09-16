using D47.Donations.Store;
using Xunit;

namespace D47.Donations.Tests;

public class ADonationKeySaysWhatItHoldsTests
{
    private const string Donor = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void AnExcerptKeyGivesItsKindDonorAndStamp()
    {
        Assert.True(
            DonationKey.TryParse($"excerpts/{Donor}/20260914T100926Z-aabbccddeeff0011.md.gz", out var key));

        Assert.Equal("excerpt", key.Kind);
        Assert.Equal(Donor, key.Donor);
        Assert.Equal("20260914T100926Z", key.Stamp);
        Assert.Equal("01234567", key.ShortDonor);
    }

    [Fact]
    public void AnExcerptDownloadsToAZipHoldingMarkdown()
    {
        Assert.True(
            DonationKey.TryParse($"excerpts/{Donor}/20260914T100926Z-aabbccddeeff0011.md.gz", out var key));

        Assert.Equal("20260914T100926Z-excerpt-01234567.zip", key.ZipName);
        Assert.Equal("20260914T100926Z-excerpt-01234567.md", key.EntryName);
    }

    [Fact]
    public void ACorpusDownloadsToAZipHoldingJsonLines()
    {
        Assert.True(
            DonationKey.TryParse($"corpus/{Donor}/20260914T100926Z-aabbccddeeff0011.jsonl.gz", out var key));

        Assert.Equal("corpus", key.Kind);
        Assert.Equal("20260914T100926Z-corpus-01234567.zip", key.ZipName);
        Assert.Equal("20260914T100926Z-corpus-01234567.jsonl", key.EntryName);
    }
}
