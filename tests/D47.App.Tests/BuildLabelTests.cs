using D47.App;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Reading the label out of the version stamp — the one fact that separates a build somebody
/// published from a build somebody made.
/// <para>
/// Reported 2026-08-28: a hand-installed <c>0.84.3-local</c> displayed itself as <em>pre-release
/// 0.84.3</em>, because the version it compares by is <c>0.84.3</c> and GitHub was asked about
/// that. The label is the part that must survive to the display even though it is thrown away for
/// comparison.
/// </para>
/// </summary>
public class BuildLabelTests
{
    /// <summary>
    /// The SDK appends <c>+&lt;sha&gt;</c> to every build in a git checkout, so the metadata is cut
    /// before the label is looked for — a commit hash is not a label.
    /// </summary>
    [Theory]
    [InlineData("0.84.3-local+8b21b3d", "local")]
    [InlineData("0.84.3-local", "local")]
    [InlineData("1.0.0-rc.1+abc", "rc.1")]
    public void ALabelIsReadPastTheCommitHash(string stamp, string expected) =>
        Assert.Equal(expected, BuildInfo.LabelOf(stamp));

    /// <summary>
    /// A published build carries none: the release workflow builds with the tag's bare version, so
    /// this is what every signed <c>d47.exe</c> in the field looks like.
    /// </summary>
    [Theory]
    [InlineData("0.84.3+8b21b3dcafe")]
    [InlineData("0.84.3")]
    [InlineData("unknown")]
    [InlineData("0.84.3-")]
    public void APublishedBuildHasNoLabel(string stamp) => Assert.Null(BuildInfo.LabelOf(stamp));
}
