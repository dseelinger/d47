using D47.Core.Updates;
using Xunit;

namespace D47.Core.Tests.Updates;

/// <summary>
/// A build installed from a working tree must not wear a published release's badge (#145's sibling,
/// reported 2026-08-28).
/// <para>
/// <b>What happened.</b> <c>get-local</c> stamps a hand-installed build <c>0.84.3-local</c>, and
/// <see cref="ReleaseVersion"/> deliberately ignores everything after the dash so that versions
/// compare sensibly. The app then asked GitHub what channel <em>0.84.3</em> was on, got a truthful
/// answer about the published pre-release, and displayed <em>pre-release 0.84.3</em> — a claim to
/// be a signed, published build it was not, in the one piece of chrome that is never off screen.
/// </para>
/// <para>
/// The rule is not about one tool's wording: the release workflow builds with the tag's bare
/// version, so a published <c>d47.exe</c> never carries a label at all. Any label means this did
/// not come from a release.
/// </para>
/// </summary>
public class ALocalBuildSaysSoTests
{
    /// <summary>
    /// The marker a Commander sees, in the two lengths the surfaces use. A local build is called
    /// what it is rather than left unmarked — unmarked is what a final release looks like.
    /// </summary>
    [Fact]
    public void ALocalBuildIsMarkedAsOneRatherThanLeftBare()
    {
        Assert.Equal("local build", ReleaseChannelText.Short(ReleaseChannel.Local));
        Assert.Equal("0.84.3 (local build)", ReleaseChannelText.Marked("0.84.3", ReleaseChannel.Local));
        Assert.Contains("not from any release", ReleaseChannelText.Full(ReleaseChannel.Local), StringComparison.Ordinal);
    }

    /// <summary>
    /// And it is a different answer from every other channel, which is the point: the fault was a
    /// local build being indistinguishable from the published one it was cut from.
    /// </summary>
    [Fact]
    public void ALocalBuildIsNotConfusableWithAPublishedOne()
    {
        Assert.NotEqual(
            ReleaseChannelText.Short(ReleaseChannel.PreRelease),
            ReleaseChannelText.Short(ReleaseChannel.Local));

        // The unmarked cases stay unmarked. A build nobody could ask about must not be dressed as
        // one thing or the other, which is what Unknown has always been for.
        Assert.Null(ReleaseChannelText.Short(ReleaseChannel.Release));
        Assert.Null(ReleaseChannelText.Short(ReleaseChannel.Unknown));
        Assert.Equal("0.84.3", ReleaseChannelText.Marked("0.84.3", ReleaseChannel.Unknown));
    }

    /// <summary>
    /// The comparison rule this rides on, pinned so it cannot be "fixed" by making
    /// <see cref="ReleaseVersion"/> notice labels: a local build compares equal to the release it
    /// was cut from, and must, or the updater would offer to replace it with itself.
    /// </summary>
    [Fact]
    public void TheVersionItselfStillIgnoresTheLabel()
    {
        Assert.True(ReleaseVersion.TryParse("0.84.3-local+8b21b3d", out var local));
        Assert.True(ReleaseVersion.TryParse("0.84.3", out var published));

        Assert.Equal(published, local);
    }
}
