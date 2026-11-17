using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

public class OnlyElitesProcessIsActedOnTests
{
    [Theory]
    [InlineData(@"C:\Program Files (x86)\Steam\steamapps\common\Elite Dangerous\Products\elite-dangerous-odyssey-64\EliteDangerous64.exe")]
    [InlineData(@"D:\Games\Elite\ELITEDANGEROUS64.EXE")]
    [InlineData("EliteDangerous64.exe")]
    public void ElitesExecutableIsActedOn(string processPath) =>
        Assert.True(LayerScope.AppliesTo(processPath));

    [Theory]
    [InlineData(@"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\bin\win64\vrstartup.exe")]
    [InlineData(@"C:\Games\Other\Game-Win64-Shipping.exe")]
    [InlineData(@"C:\EliteDangerous64.exe\Launcher.exe")]
    [InlineData(@"C:\Games\EliteDangerous64.exe.bak")]
    [InlineData(@"C:\Games\EliteDangerous32.exe")]
    [InlineData("")]
    [InlineData(null)]
    public void AnyOtherProcessIsLeftAlone(string? processPath) =>
        Assert.False(LayerScope.AppliesTo(processPath));
}
