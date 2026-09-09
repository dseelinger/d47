using D47.App.Updates;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The names of the assets the updater fetches are a published interface and cannot be changed.
/// </summary>
public class TheUpdateAssetNamesAreAContractTests
{
    /// <summary>
    /// Asserted as literals rather than against the constants, which would agree with any change and
    /// prove nothing — the rule <c>TheModelOnTheWireTests</c> already sets for pinned values: changing
    /// one should be a decision somebody makes, not a change a test follows silently.
    /// </summary>
    [Fact]
    public void TheArchiveIsCalledExactlyWhatEveryInstalledBuildLooksFor()
    {
        Assert.Equal("d47.zip", UpdateChecker.ArchiveAsset);
    }

    [Fact]
    public void AndSoIsItsChecksum()
    {
        Assert.Equal("d47.zip.sha256", UpdateChecker.ChecksumAsset);
    }

    /// <summary>
    /// The checksum is the archive's name with the suffix, and a Commander reading a release page
    /// should find them beside each other.
    /// </summary>
    [Fact]
    public void AndTheTwoAgreeWithEachOther()
    {
        Assert.Equal($"{UpdateChecker.ArchiveAsset}.sha256", UpdateChecker.ChecksumAsset);
    }

    /// <summary>
    /// Carries no version, which is the property that makes it findable by a build that has no idea
    /// what version it is looking for.
    /// </summary>
    [Fact]
    public void AndCarriesNoVersion()
    {
        // A version would be appended, which is what "d47-0.79.0.zip" looks like.
        Assert.DoesNotContain('-', UpdateChecker.ArchiveAsset);
        Assert.Equal("d47", System.IO.Path.GetFileNameWithoutExtension(UpdateChecker.ArchiveAsset));
    }
}
