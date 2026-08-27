using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Updates;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// A pre-release build says so, and nothing else claims to be anything
/// (<a href="https://github.com/dseelinger/d47/issues/92">#92</a>).
/// <para>
/// <b>Three states, not two.</b> Release and pre-release leave no room for the ordinary case of
/// being offline, rate-limited, or simply started before the answer came back. Collapsing that
/// into "release" is the lie in the dangerous direction — a Commander told a build is final at
/// exactly the moment nothing has confirmed it. <see cref="ReleaseChannel.Unknown"/> shows no
/// marker, which is what a final release looks like anyway; the difference is that it claims
/// nothing rather than claiming that.
/// </para>
/// <para>
/// <b>And it is read at run time rather than stamped in.</b> Pre-release is a property of the
/// GitHub Release, which is mutable — <c>gh release edit … --prerelease=false</c> promotes one, and
/// <c>release.ps1</c> prints that as the intended next step. It is not a property of the binary,
/// which is immutable: a published tag never moves, so a stamped build would go on calling itself
/// a pre-release for ever after promotion, on every machine that installed it.
/// </para>
/// </summary>
public class APreReleaseSaysSoTests
{
    private const string Version = "0.78.0";

    private static string VersionRow(ReleaseChannel channel)
    {
        var rows = AboutCapability.Create(
            new AppPaths(Path.Combine(Path.GetTempPath(), "d47-channel-rows")),
            Version,
            $"{Version}+4b18aaecbe2510b0aeae95d3f19583edd18ea205",
            showChangelog: () => { },
            showChangelogOnline: () => { },
            addToStartMenu: () => { },
            startMenuWanted: () => true,
            setUpKeys: () => { },
            showCommunity: () => { },
            channel: () => channel).Settings;

        return rows.Single(row => row.Key == AboutCapability.VersionKey)
            .Binding!.Read(D47Settings.Defaults) ?? string.Empty;
    }

    [Fact]
    public void AboutSaysSoOnAPreRelease()
    {
        Assert.Contains("pre-release", VersionRow(ReleaseChannel.PreRelease), StringComparison.Ordinal);
        Assert.Contains(Version, VersionRow(ReleaseChannel.PreRelease), StringComparison.Ordinal);
    }

    /// <summary>
    /// A final release is the unmarked case. A badge on every build is not a badge.
    /// </summary>
    [Fact]
    public void AndSaysNothingOnAFinalRelease()
    {
        Assert.Equal(Version, VersionRow(ReleaseChannel.Release));
    }

    /// <summary>
    /// The state the whole enum exists for. Offline is not evidence of being final, so the row
    /// must read exactly as a final release does rather than asserting either way.
    /// </summary>
    [Fact]
    public void AndClaimsNothingWhenItCouldNotAsk()
    {
        Assert.Equal(Version, VersionRow(ReleaseChannel.Unknown));
        Assert.DoesNotContain("release", VersionRow(ReleaseChannel.Unknown), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A host that supplies no channel at all — the designer, and every test that is not about
    /// this — behaves as Unknown rather than throwing or claiming.
    /// </summary>
    [Fact]
    public void AndAHostThatNeverAnswersIsTreatedAsUnknown()
    {
        var rows = AboutCapability.Create(
            new AppPaths(Path.Combine(Path.GetTempPath(), "d47-channel-rows")),
            Version,
            $"{Version}+abc1234",
            showChangelog: () => { },
            showChangelogOnline: () => { },
            addToStartMenu: () => { },
            startMenuWanted: () => true,
            setUpKeys: () => { },
            showCommunity: () => { }).Settings;

        Assert.Equal(
            Version,
            rows.Single(row => row.Key == AboutCapability.VersionKey).Binding!.Read(D47Settings.Defaults));
    }

    /// <summary>
    /// The title bar's form is short, for the reason the commit hash is kept out of it: that strip
    /// is on screen the entire time and a Commander cannot dismiss it.
    /// </summary>
    [Fact]
    public void TheTitleBarFormIsShortEnoughToLiveInChrome()
    {
        var marked = ReleaseChannelText.Marked(Version, ReleaseChannel.PreRelease);

        Assert.Contains("pre-release", marked, StringComparison.Ordinal);
        Assert.True(marked.Length <= 32, $"\"{marked}\" is too long for the title bar.");
        Assert.Equal(Version, ReleaseChannelText.Marked(Version, ReleaseChannel.Release));
    }

    /// <summary>
    /// One judgement, read the same way everywhere. The three sites take their wording from here,
    /// so a marker that appeared in one place and not another would be a bug in a surface rather
    /// than a second opinion about the build.
    /// </summary>
    [Fact]
    public void OnlyAPreReleaseIsEverMarked()
    {
        foreach (var channel in Enum.GetValues<ReleaseChannel>())
        {
            var marked = ReleaseChannelText.Short(channel) is not null;

            Assert.Equal(channel == ReleaseChannel.PreRelease, marked);
        }
    }
}
