using Avalonia.Headless.XUnit;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>A surface with no frames of its own moves the System page's orbit view from the host's tick (#2391).</summary>
[Trait("Category", "Integration")]
public sealed class TheHeadsetOrbitViewMovesOnTheTickTests
{
    [AvaloniaFact]
    public void ARunningOrbitViewMovesOnEveryTick()
    {
        var (panel, window, _) = TheSystemPageDrawsItsOrbitsTests.Shown();

        Assert.True(panel.TickOrbits());
        Assert.True(panel.TickOrbits());

        window.Close();
    }

    [AvaloniaFact]
    public void APausedOrbitViewDoesNotMove()
    {
        var (panel, window, _) = TheSystemPageDrawsItsOrbitsTests.Shown();

        TheSystemPageDrawsItsOrbitsTests.Press(TheSystemPageDrawsItsOrbitsTests.Named(panel, "Pause or run"));
        Assert.False(panel.TickOrbits());

        TheSystemPageDrawsItsOrbitsTests.Press(TheSystemPageDrawsItsOrbitsTests.Named(panel, "Pause or run"));
        Assert.True(panel.TickOrbits());

        window.Close();
    }

    [AvaloniaFact]
    public void AnotherSectionOrAnotherTabDoesNotMove()
    {
        var (panel, window, _) = TheSystemPageDrawsItsOrbitsTests.Shown();

        TheSystemPageListsItsStationsTests.Section(panel, "Overview");
        Assert.False(panel.TickOrbits());

        TheSystemPageListsItsStationsTests.Section(panel, "Orbits");
        Assert.True(panel.TickOrbits());

        panel.Tab = PanelTab.Transcript;
        Assert.Equal(PanelTab.Transcript, panel.Tab);
        Assert.False(panel.TickOrbits());

        window.Close();
    }
}
