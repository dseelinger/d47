using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>START saves the comms key and button the talk button step chose.</summary>
public class TheSetupSavesTheCommsKeyTests
{
    [Fact]
    public void StartSavesTheCommsKeyAndButton()
    {
        var surface = TestSurface.For(new MemoryInstall());
        var choices = SetupChoices.From(surface.Settings.Current) with { CommsKey = "Ctrl+F9" };

        Assert.Empty(FirstRun.Apply(surface.Settings, choices));
        Assert.Equal("Ctrl+F9", surface.Settings.Current.Listening.CommsPushToTalkKey);
    }

    [Fact]
    public void TheChoicesStartFromTheSavedCommsKey()
    {
        var surface = TestSurface.For(new MemoryInstall());

        Assert.Null(SetupChoices.From(surface.Settings.Current).CommsKey);
    }
}
