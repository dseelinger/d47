using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>A toggle that names a press row with Beside draws that row on its own line.</summary>
public class APressRowBesideAToggleIsDrawnOnItsLineTests
{
    [Fact]
    public void EveryBesideNamesAnElsewhereDrawnPressRowOnAToggle()
    {
        var surface = TestSurface.For(new MemoryInstall(), everyOptionalSurface: true);
        var rows = surface.Settings.Sections.SelectMany(section => section.Rows).ToList();

        var beside = rows.Where(row => row.Beside is not null).ToList();
        Assert.NotEmpty(beside);

        foreach (var row in beside)
        {
            Assert.Equal(SettingKind.Toggle, row.Kind);

            var press = rows.SingleOrDefault(other => other.Key == row.Beside);
            Assert.True(press is not null, $"{row.Key} names {row.Beside}, which is not a row.");
            Assert.True(press.DrawnElsewhere, $"{press.Key} is beside {row.Key} and must be DrawnElsewhere.");
            Assert.True(press.Press is not null || press.PressAsync is not null, $"{press.Key} has no press.");
        }
    }

    [Fact]
    public void TheCovasReverbToggleHasTheCovasTestBesideIt()
    {
        var surface = TestSurface.For(new MemoryInstall(), everyOptionalSurface: true);
        var row = surface.Settings.Sections.SelectMany(section => section.Rows)
            .Single(row => row.Key == SpeechCapability.CovasReverbKey);

        Assert.Equal(SpeechCapability.CovasTestKey, row.Beside);
    }
}
