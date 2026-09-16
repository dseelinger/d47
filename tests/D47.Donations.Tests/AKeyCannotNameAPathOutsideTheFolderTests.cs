using D47.Donations.Store;
using Xunit;

namespace D47.Donations.Tests;

/// <summary>
/// The donor and the stamp come out of a key the store hands back and go into a file name, so every
/// component is checked against a fixed alphabet before it gets there.
/// </summary>
public class AKeyCannotNameAPathOutsideTheFolderTests
{
    private const string Donor = "0123456789abcdef0123456789abcdef";

    [Theory]
    // A traversal cannot survive the segment count.
    [InlineData("excerpts/../../elsewhere/20260914T100926Z-aa.md.gz")]
    [InlineData("excerpts/../20260914T100926Z-aa.md.gz")]
    // Nor can a donor that is not 32 lowercase hex characters.
    [InlineData("excerpts/0123456789ABCDEF0123456789abcdef/20260914T100926Z-aa.md.gz")]
    [InlineData("excerpts/short/20260914T100926Z-aa.md.gz")]
    [InlineData("excerpts/................................/20260914T100926Z-aa.md.gz")]
    // Nor a stamp that is not a basic-format UTC instant.
    [InlineData("excerpts/0123456789abcdef0123456789abcdef/....-aa.md.gz")]
    [InlineData("excerpts/0123456789abcdef0123456789abcdef/nodash.md.gz")]
    // Nor a suffix belonging to the other kind, nor a prefix belonging to neither.
    [InlineData("excerpts/0123456789abcdef0123456789abcdef/20260914T100926Z-aa.jsonl.gz")]
    [InlineData("elsewhere/0123456789abcdef0123456789abcdef/20260914T100926Z-aa.md.gz")]
    public void AKeyTheWorkerWouldNotHaveWrittenIsRefused(string key) =>
        Assert.False(DonationKey.TryParse(key, out _));

    [Fact]
    public void AnAcceptedKeyNamesAFileAndNeverAPath()
    {
        Assert.True(
            DonationKey.TryParse($"excerpts/{Donor}/20260914T100926Z-aabbccddeeff0011.md.gz", out var key));

        Assert.Equal(key.ZipName, Path.GetFileName(key.ZipName));
        Assert.Equal(key.EntryName, Path.GetFileName(key.EntryName));
        Assert.DoesNotContain("..", key.ZipName, StringComparison.Ordinal);
    }
}
