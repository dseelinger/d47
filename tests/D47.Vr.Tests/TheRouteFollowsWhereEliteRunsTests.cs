using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// Each start chooses the route: SteamVR's overlay for stock Elite and for Elite on SteamVR's OpenXR runtime,
/// the layer for Elite on any other OpenXR runtime, and only while the layer setting is on.
/// </summary>
public class TheRouteFollowsWhereEliteRunsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 17, 20, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Tick(int n) => Start + TimeSpan.FromMilliseconds(100 * n);

    [Fact]
    public void StockEliteOnOpenVrGoesThroughSteamVr()
    {
        using var rig = new Rig(layerWanted: true);

        Assert.Equal(VrStartOutcome.Started, rig.Runtime.Start().Outcome);

        Assert.Same(rig.SteamVr, rig.Runtime.SteamVr);
        Assert.True(rig.Runtime.SupportsCurvature);
    }

    [Fact]
    public void EliteOnSteamVrsOpenXrRuntimeGoesThroughSteamVr()
    {
        using var rig = new Rig(layerWanted: true);
        rig.Block.Attach("SteamVR/OpenXR");
        rig.Block.Frame();

        Assert.Equal(VrStartOutcome.Started, rig.Runtime.Start().Outcome);
        rig.Block.Frame();
        Assert.True(rig.Runtime.Serve(Tick(0)));

        Assert.Same(rig.SteamVr, rig.Runtime.SteamVr);
        Assert.Empty(rig.Device.LuidOpens);
    }

    [Fact]
    public void EliteOnAnotherOpenXrRuntimeGoesThroughTheLayer()
    {
        using var rig = new Rig(layerWanted: true);
        rig.Block.Attach("VirtualDesktopXR");

        var first = rig.Runtime.Start();
        Assert.Equal(VrStartOutcome.NotReady, first.Outcome);
        Assert.Equal(OpenXrLayerRuntime.NoFrames, first.Detail);

        rig.Block.Frame();
        Assert.Equal(VrStartOutcome.Started, rig.Runtime.Start().Outcome);

        Assert.Null(rig.Runtime.SteamVr);
        Assert.False(rig.Runtime.SupportsCurvature);
        Assert.Equal(0, rig.OpenVr.Count(nameof(FakeOpenVr.SessionIsRunning)));
    }

    [Fact]
    public void ALayerAttachingWhileSteamVrIsLiveEndsTheSessionAndTheNextStartTakesTheLayer()
    {
        using var rig = new Rig(layerWanted: true);

        Assert.Equal(VrStartOutcome.Started, rig.Runtime.Start().Outcome);
        Assert.True(rig.Runtime.Serve(Tick(0)));

        rig.Block.Attach("Oculus");
        Assert.True(rig.Runtime.Serve(Tick(1)));

        rig.Block.Frame();
        Assert.False(rig.Runtime.Serve(Tick(2)));

        rig.Runtime.Stop();
        rig.Block.Frame();

        Assert.Equal(VrStartOutcome.Started, rig.Runtime.Start().Outcome);
        Assert.Null(rig.Runtime.SteamVr);
        Assert.False(rig.Runtime.SupportsCurvature);
    }

    [Fact]
    public void SwitchingTheLayerSettingOffEndsTheLayerRoute()
    {
        using var rig = new Rig(layerWanted: true);
        rig.Block.Attach("VirtualDesktopXR");
        rig.Runtime.Start();
        rig.Block.Frame();
        Assert.Equal(VrStartOutcome.Started, rig.Runtime.Start().Outcome);
        Assert.True(rig.Runtime.Serve(Tick(0)));

        rig.LayerWanted = false;
        rig.Block.Frame();
        Assert.False(rig.Runtime.Serve(Tick(1)));

        rig.Runtime.Stop();
        Assert.Equal(VrStartOutcome.Started, rig.Runtime.Start().Outcome);
        Assert.Same(rig.SteamVr, rig.Runtime.SteamVr);
    }

    [Fact]
    public void PointingCarriesOverToTheRouteThatStarts()
    {
        using var rig = new Rig(layerWanted: true) { Runtime = { Pointing = true } };
        rig.Block.Attach("VirtualDesktopXR");
        rig.Runtime.Start();
        rig.Block.Frame();

        Assert.Equal(VrStartOutcome.Started, rig.Runtime.Start().Outcome);
        Assert.Equal(1u, rig.Block.D47Half.Pointing);
    }

    [Fact]
    public void WithTheLayerSettingOffTheLayerIsNeverLookedAt()
    {
        using var rig = new Rig(layerWanted: false);
        rig.Block.Attach("VirtualDesktopXR");
        rig.Block.Frame();

        Assert.Equal(VrStartOutcome.Started, rig.Runtime.Start().Outcome);
        rig.Block.Frame();
        Assert.True(rig.Runtime.Serve(Tick(0)));

        Assert.Same(rig.SteamVr, rig.Runtime.SteamVr);
        Assert.Equal(0, rig.Block.Opens);
    }

    [Fact]
    public void WithTheLayerSettingOffSteamVrsReasonIsUnchanged()
    {
        using var rig = new Rig(layerWanted: false, steamVrRunning: false);
        var alone = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance, new FakeOpenVr { Running = false });

        Assert.Equal(alone.Start(), rig.Runtime.Start());
    }

    [Fact]
    public void WithNeitherRouteTheReasonNamesBoth()
    {
        using var rig = new Rig(layerWanted: true, steamVrRunning: false);

        var start = rig.Runtime.Start();

        Assert.Equal(VrStartOutcome.NotReady, start.Outcome);
        Assert.StartsWith("SteamVR is not running.", start.Detail, StringComparison.Ordinal);
        Assert.EndsWith(HeadsetRuntime.NoLayerEither, start.Detail, StringComparison.Ordinal);
    }

    private sealed class Rig : IDisposable
    {
        public Rig(bool layerWanted, bool steamVrRunning = true)
        {
            LayerWanted = layerWanted;
            OpenVr = new FakeOpenVr { Running = steamVrRunning };
            SteamVr = new SteamVrRuntime([new FakeSurface()], NullLogger<SteamVrRuntime>.Instance, OpenVr);
            Runtime = new HeadsetRuntime(
                SteamVr,
                new OpenXrLayerRuntime([new FakeSurface()], NullLogger<OpenXrLayerRuntime>.Instance, Block, Device),
                () => LayerWanted,
                NullLogger<HeadsetRuntime>.Instance);
        }

        public bool LayerWanted { get; set; }

        public FakeOpenVr OpenVr { get; }

        public SteamVrRuntime SteamVr { get; }

        public InMemoryXrBlock Block { get; } = new();

        public FakeTextureDevice Device { get; } = new();

        public HeadsetRuntime Runtime { get; }

        public void Dispose()
        {
            Runtime.Stop();
            SteamVr.Stop();
            Block.Dispose();
        }
    }
}
