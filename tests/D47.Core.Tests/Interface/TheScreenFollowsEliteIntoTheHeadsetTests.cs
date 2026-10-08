using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>The headset's eye is the picture only while SteamVR is showing Elite's own process.</summary>
public class TheScreenFollowsEliteIntoTheHeadsetTests
{
    private const uint Elite = 4242;

    [Fact]
    public void NoSteamVrSessionGivesTheWindow() =>
        Assert.Equal(ScreenSource.EliteWindow, ScreenPictures.Choose(0, Elite));

    [Fact]
    public void ASceneThatIsNotEliteGivesTheWindow() =>
        Assert.Equal(ScreenSource.EliteWindow, ScreenPictures.Choose(1717, Elite));

    [Fact]
    public void ASceneThatIsEliteGivesTheEye() =>
        Assert.Equal(ScreenSource.HeadsetEye, ScreenPictures.Choose(Elite, Elite));

    [Theory]
    [InlineData(0u)]
    [InlineData(1717u)]
    public void EliteNotRunningIsRefused(uint scene) =>
        Assert.Equal(ScreenSource.EliteNotRunning, ScreenPictures.Choose(scene, 0));
}
