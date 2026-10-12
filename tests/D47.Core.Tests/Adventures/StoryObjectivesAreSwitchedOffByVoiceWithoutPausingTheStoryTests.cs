using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>"stop calling out the story" turns off the story objectives callout and does not pause the story.</summary>
public sealed class StoryObjectivesAreSwitchedOffByVoiceWithoutPausingTheStoryTests
{
    [Fact]
    public void StopCallingOutTheStoryIsTheCalloutSettingAndNotThePause()
    {
        var surface = TestSurface.For(new MemoryInstall());

        var setting = surface.Router.MatchSetting("stop calling out the story");

        Assert.NotNull(setting);
        Assert.Contains("adventure", setting!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Null(surface.Router.MatchToolCommand("stop calling out the story"));
        Assert.Equal(AdventureCapability.PauseTool, surface.Router.MatchToolCommand("pause the story")?.ToolName);
    }
}
