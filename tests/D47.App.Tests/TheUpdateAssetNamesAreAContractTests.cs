using D47.App.Updates;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The names of the assets the updater fetches are a published interface and cannot be changed
/// (<a href="https://github.com/dseelinger/d47/issues/96">#96</a>).
/// <para>
/// <b>This is a test rather than a comment because a comment is advice.</b> The reasoning has been
/// in the source since v0.5.14 — phrased as history, about a rename that once cost in-place updating
/// for every build older than that release — and history sitting near a constant is not a thing that
/// stops the constant being edited.
/// </para>
/// <para>
/// <b>What a rename costs.</b> <c>UpdateChecker</c> matches release assets by ordinal equality
/// against these literals, so calling the archive <c>d47-0.79.0.zip</c> does not break the next
/// release — it breaks every build already installed, permanently. Each one finds no archive,
/// <c>CanInstall</c> goes false, and the update degrades to "open the release page" without saying
/// anything. It fails quietly, which is the shape this repository distrusts most.
/// </para>
/// <para>
/// <b>And it cannot be repaired afterwards.</b> Teaching this class to match a pattern helps only
/// builds released after the change. The asset name is the one part of a release that binaries
/// already in the field reach back and read, which makes it the one part that can never be
/// retroactively fixed.
/// </para>
/// <para>
/// So: if this test fails, the correct response is almost never to update the expected string. It
/// is to put the asset name back.
/// </para>
/// </summary>
public class TheUpdateAssetNamesAreAContractTests
{
    /// <summary>
    /// Asserted as literals rather than against the constants, which would agree with any change
    /// and prove nothing — the rule <c>TheModelOnTheWireTests</c> already sets for pinned values:
    /// changing one should be a decision somebody makes, not a change a test follows silently.
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
    /// should find them beside each other. Stated so that renaming one without the other — the
    /// halfway version of this mistake — is also caught.
    /// </summary>
    [Fact]
    public void AndTheTwoAgreeWithEachOther()
    {
        Assert.Equal($"{UpdateChecker.ArchiveAsset}.sha256", UpdateChecker.ChecksumAsset);
    }

    /// <summary>
    /// Carries no version, which is the property that makes it findable by a build that has no
    /// idea what version it is looking for. The installer is the opposite case and is deliberately
    /// versioned; nothing in <c>src/</c> reads that one.
    /// </summary>
    [Fact]
    public void AndCarriesNoVersion()
    {
        // A version would be appended, which is what "d47-0.79.0.zip" looks like. The name has to
        // stay findable by a build that has no idea what version it should be looking for.
        Assert.DoesNotContain('-', UpdateChecker.ArchiveAsset);
        Assert.Equal("d47", System.IO.Path.GetFileNameWithoutExtension(UpdateChecker.ArchiveAsset));
    }
}
