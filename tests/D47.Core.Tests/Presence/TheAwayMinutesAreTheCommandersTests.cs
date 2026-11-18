using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Presence;

/// <summary>The model cannot change how long counts as away; the panel can, within 0 to 240.</summary>
public class TheAwayMinutesAreTheCommandersTests
{
    [Fact]
    public async Task TheModelIsRefused()
    {
        var surface = TestSurface.For(new MemoryInstall());

        var result = await surface.Registry.Invoke(
            "set_setting",
            new ToolArguments(new Dictionary<string, string> { ["key"] = PrivacyCapability.AwayMinutesKey, ["value"] = "30" }));

        Assert.True(result.IsError);
        Assert.Equal(5, surface.Settings.Current.Presence.AwayMinutes);
    }

    [Theory]
    [InlineData("30", 30)]
    [InlineData("0", 0)]
    [InlineData("500", 240)]
    [InlineData("-3", 0)]
    public void ThePanelChangesItWithinItsRange(string value, int expected)
    {
        var surface = TestSurface.For(new MemoryInstall());

        Assert.True(surface.Settings.Apply(PrivacyCapability.AwayMinutesKey, value, SettingsCaller.Panel).Ok);
        Assert.Equal(expected, surface.Settings.Current.Presence.AwayMinutes);
    }
}
