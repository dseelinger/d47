using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>The Your own audio row opens data\audio when there is a shell to open it with (#498).</summary>
public class TheAudioRowOpensItsFolderTests
{
    private static Capabilities.SettingRow DropsRow(Action? openFolder) =>
        AudioCapability.Create(() => "", openFolder).Settings.Single(row => row.Key == AudioCapability.DropsKey);

    [Fact]
    public void AnOpenerGivesTheRowAButton()
    {
        var opened = false;
        var row = DropsRow(() => opened = true);

        Assert.Equal("Open audio folder", row.PressLabel);
        row.Press!();
        Assert.True(opened);
    }

    [Fact]
    public void NoOpenerMeansNoButton()
    {
        var row = DropsRow(null);

        Assert.Null(row.Press);
        Assert.Null(row.PressLabel);
    }

    [Fact]
    public void AnEmptyFolderIsNamed()
    {
        Assert.Equal(@"Nothing in data\audio yet.", CueLibrary.Load().DescribeDrops());
    }
}
