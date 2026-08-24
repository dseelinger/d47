using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// A placement instruction lands on the panel the Commander is looking at
/// (<a href="https://github.com/dseelinger/d47/issues/21">#21</a>), ruled 2026-08-24:
/// <em>"whichever panel I'm looking at."</em>
/// <para>
/// The specific case — opacity — was closed in 0.60.7 by collapsing it to one shared value, and
/// <see cref="OneOpacityForBothPanelsTests"/> holds that line. The rest cannot go the same way:
/// distance, size and angle have a real reason to differ, because mini exists to sit smaller and
/// further out of the way. So they stay two values and gain a third row that resolves which of the
/// two was meant.
/// </para>
/// </summary>
public class ThePanelYouAreLookingAtTests
{
    private static IReadOnlyList<SettingRow> Rows()
    {
        var install = new TempInstall();
        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);

        return
        [
            .. VrCapability
                .Create(
                    settings,
                    new VrCapability.HeadsetSurface
                    {
                        Report = () => (D47.Core.Vr.VrState.Unavailable, "No runtime in a test."),
                        Reanchor = () => 0,
                    })
                .Settings,
        ];
    }

    private static SettingRow Row(string key) =>
        Assert.Single(Rows(), row => row.Key == key);

    private static D47Settings With(string mode, double panel, double mini)
    {
        var settings = new D47Settings();

        return settings with
        {
            Vr = settings.Vr with
            {
                Mode = mode,
                Panel = settings.Vr.Panel with { Distance = panel },
                Mini = settings.Vr.Mini with { Distance = mini },
            },
        };
    }

    [Fact]
    public void ItReadsWhicheverSurfaceIsOnScreen()
    {
        var row = Row("vr.current.distance");

        Assert.Equal("1.2", row.Binding!.Read(With("full", 1.2, 0.7)));
        Assert.Equal("0.7", row.Binding!.Read(With("mini", 1.2, 0.7)));
    }

    [Fact]
    public void MovingItInMiniLeavesTheBigPanelExactlyWhereItWas()
    {
        var moved = Row("vr.current.distance").Binding!.Write!(With("mini", 1.2, 0.7), "0.5");

        Assert.Equal(0.5, moved.Vr.Mini.Distance);
        Assert.Equal(1.2, moved.Vr.Panel.Distance);
    }

    [Fact]
    public void MovingItInFullLeavesTheMiniPanelExactlyWhereItWas()
    {
        var moved = Row("vr.current.distance").Binding!.Write!(With("full", 1.2, 0.7), "2.0");

        Assert.Equal(2.0, moved.Vr.Panel.Distance);
        Assert.Equal(0.7, moved.Vr.Mini.Distance);
    }

    /// <summary>
    /// The two explicit rows survive and still write their own surface, because setting mini up
    /// while wearing the big panel is an ordinary thing to want and the page is where it is done.
    /// </summary>
    [Fact]
    public void TheExplicitRowsSurviveAndStillWriteTheirOwnSurface()
    {
        var moved = Row("vr.mini.distance").Binding!.Write!(With("full", 1.2, 0.7), "0.4");

        Assert.Equal(0.4, moved.Vr.Mini.Distance);
        Assert.Equal(1.2, moved.Vr.Panel.Distance);
    }

    /// <summary>
    /// And the model is offered one of the three rather than all of them. Three ways to say one
    /// number is how the wrong one gets picked, which is the whole of the original report.
    /// </summary>
    [Fact]
    public void OnlyTheResolvingRowIsOfferedToTheModel()
    {
        Assert.False(Row("vr.current.distance").PageOnly, "the row that means what was asked");
        Assert.True(Row("vr.panel.distance").PageOnly, "on the page, not offered to the model");
        Assert.True(Row("vr.mini.distance").PageOnly, "on the page, not offered to the model");
    }

    /// <summary>
    /// Every placement knob resolves, not just the one that is easy to test — a Commander who says
    /// "turn it a bit" means the panel in front of them exactly as much as one who says "closer".
    /// </summary>
    [Theory]
    [InlineData("lock")]
    [InlineData("distance")]
    [InlineData("size")]
    [InlineData("curve")]
    public void EveryPlacementKnobHasAResolvingRow(string name)
    {
        var keys = Rows().Select(row => row.Key).ToList();

        Assert.Contains($"vr.current.{name}", keys);
        Assert.Contains($"vr.panel.{name}", keys);
        Assert.Contains($"vr.mini.{name}", keys);
    }
}
