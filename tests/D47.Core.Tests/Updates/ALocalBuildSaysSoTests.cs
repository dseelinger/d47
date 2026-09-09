using D47.Core.Updates;
using Xunit;

namespace D47.Core.Tests.Updates;

/// <summary>A build installed from a working tree must not wear a published release's badge (#145's
/// sibling).</summary>
public class ALocalBuildSaysSoTests
{
    /// <summary>The marker a Commander sees, in the two lengths the surfaces use.</summary>
    [Fact]
    public void ALocalBuildIsMarkedAsOneRatherThanLeftBare()
    {
        Assert.Equal("local build", ReleaseChannelText.Short(ReleaseChannel.Local));
        Assert.Equal("0.84.3 (local build)", ReleaseChannelText.Marked("0.84.3", ReleaseChannel.Local));
        Assert.Contains("not from any release", ReleaseChannelText.Full(ReleaseChannel.Local), StringComparison.Ordinal);
    }

    /// <summary>
    /// And it is a different answer from every other channel, which is the point: the fault was a local
    /// build being indistinguishable from the published one it was cut from.
    /// </summary>
    [Fact]
    public void ALocalBuildIsNotConfusableWithAPublishedOne()
    {
        Assert.NotEqual(
            ReleaseChannelText.Short(ReleaseChannel.PreRelease),
            ReleaseChannelText.Short(ReleaseChannel.Local));

        // The unmarked cases stay unmarked.
        Assert.Null(ReleaseChannelText.Short(ReleaseChannel.Release));
        Assert.Null(ReleaseChannelText.Short(ReleaseChannel.Unknown));
        Assert.Equal("0.84.3", ReleaseChannelText.Marked("0.84.3", ReleaseChannel.Unknown));
    }

    /// <summary>
    /// The comparison rule this rides on, pinned so it cannot be "fixed" by making <see
    /// cref="ReleaseVersion"/> notice labels: a local build compares equal to the release it was cut
    /// from, and must, or the updater would offer to replace it with itself.
    /// </summary>
    [Fact]
    public void TheVersionItselfStillIgnoresTheLabel()
    {
        Assert.True(ReleaseVersion.TryParse("0.84.3-local+8b21b3d", out var local));
        Assert.True(ReleaseVersion.TryParse("0.84.3", out var published));

        Assert.Equal(published, local);
    }
}
