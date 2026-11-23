using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using Xunit;
using static D47.App.Tests.UiWait;

namespace D47.App.Tests;

/// <summary>The headset draws the panel in a window that is never shown, so the Orbits plot moves from the host's tick without relying on a visible window (#2732).</summary>
[Trait("Category", "Integration")]
public sealed class TheHeadsetOrbitPlotMovesInTheHiddenWindowTests
{
    [AvaloniaFact]
    public void TheOrbitsPlotMarksTheHeadsetFrameDirtyOnEveryTick()
    {
        var clock = new AnOrbitMarkerStaysOnItsDrawnPathTests.FakeClock(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero));
        var systems = new TheSystemPageListsItsStationsTests.Systems(TheSystemPageListsItsStationsTests.Ltt7786Profile());
        var surface = new StarSystemSurface(systems, () => new JournalLocation("LTT 7786", null, false, null) { SystemAddress = 633608311522 })
        {
            Clock = clock,
        };

        var (settings, _, _) = TestSurface.Create();

        using var vr = new VrPanelSurface(new PanelViewModel(), settings, _ => null, starSystem: surface);
        vr.Nav.Select(PanelTab.Search);
        Dispatcher.UIThread.RunJobs();
        Serve(vr);
        Until(
            () =>
            {
                Serve(vr);
                return vr.Board.View.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == "LAST REPORTED");
            },
            10,
            20);
        TheSystemPageListsItsStationsTests.Section(vr.Board.View, "Orbits");
        Assert.NotEmpty(vr.Board.View.GetVisualDescendants().OfType<OrbitPlot>());
        Serve(vr);
        Assert.False(vr.IsDirty);

        for (var tick = 1; tick <= 4; tick++)
        {
            vr.TickOrbits();
            Assert.True(vr.IsDirty, $"tick {tick}: the plot did not mark the frame dirty");
            Serve(vr);
        }

        TheSystemPageDrawsItsOrbitsTests.Press(TheSystemPageDrawsItsOrbitsTests.Named(vr.Board.View, "Pause or run"));
        Serve(vr);
        vr.TickOrbits();
        Assert.False(vr.IsDirty);
    }

    private static void Serve(VrPanelSurface panel)
    {
        var (width, height) = panel.Size;
        var buffer = new byte[width * height * 4];

        unsafe
        {
            fixed (byte* pixels = buffer)
            {
                panel.Draw((IntPtr)pixels, width * 4);
            }
        }
    }
}
