using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Updates;
using Xunit;

namespace D47.Core.Tests;

/// <summary>A pre-release build says so, and nothing else claims to be anything.</summary>
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

    /// <summary>A final release is the unmarked case.</summary>
    [Fact]
    public void AndSaysNothingOnAFinalRelease()
    {
        Assert.Equal(Version, VersionRow(ReleaseChannel.Release));
    }

    /// <summary>The state the whole enum exists for.</summary>
    [Fact]
    public void AndClaimsNothingWhenItCouldNotAsk()
    {
        Assert.Equal(Version, VersionRow(ReleaseChannel.Unknown));
        Assert.DoesNotContain("release", VersionRow(ReleaseChannel.Unknown), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A host that supplies no channel at all — the designer, and every test that is not about this —
    /// behaves as Unknown rather than throwing or claiming.
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
    /// The title bar's form is short, for the reason the commit hash is kept out of it: that strip is
    /// on screen the entire time and a Commander cannot dismiss it.
    /// </summary>
    [Fact]
    public void TheTitleBarFormIsShortEnoughToLiveInChrome()
    {
        var marked = ReleaseChannelText.Marked(Version, ReleaseChannel.PreRelease);

        Assert.Contains("pre-release", marked, StringComparison.Ordinal);
        Assert.True(marked.Length <= 32, $"\"{marked}\" is too long for the title bar.");
        Assert.Equal(Version, ReleaseChannelText.Marked(Version, ReleaseChannel.Release));
    }

    /// <summary>One judgement, read the same way everywhere.</summary>
    [Fact]
    public void EveryBuildThatIsNotAFinalReleaseSaysWhatItIs()
    {
        foreach (var channel in Enum.GetValues<ReleaseChannel>())
        {
            var marked = ReleaseChannelText.Short(channel) is not null;
            var shouldBe = channel is ReleaseChannel.PreRelease or ReleaseChannel.Local;

            Assert.Equal(shouldBe, marked);

            // And the long form agrees with the short one, since About and the title bar are answering the
            // same question at two lengths.
            Assert.Equal(marked, ReleaseChannelText.Full(channel) is not null);
        }
    }
}
