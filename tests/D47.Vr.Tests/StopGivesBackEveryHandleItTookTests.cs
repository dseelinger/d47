using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// The beam and the cursor are overlays too, and they are the two a Stop is most likely to forget:
/// they are built by <c>Guides</c> rather than from the source list.
/// </summary>
public class StopGivesBackEveryHandleItTookTests
{
    [Fact]
    public void EveryQuadIncludingTheBeamAndTheCursorIsDestroyed()
    {
        var openVr = new FakeOpenVr();

        var runtime = new SteamVrRuntime(
            [new FakeSurface(), new FakeSurface { Surface = VrSurface.Captions }],
            NullLogger<SteamVrRuntime>.Instance,
            openVr) { Pointing = true };

        Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

        // Panel, captions, beam, cursor.
        Assert.Equal(4, openVr.Live.Count);

        runtime.Stop();

        Assert.Empty(openVr.Live);
        Assert.Equal(4, openVr.Count(nameof(FakeOpenVr.DestroyOverlay)));
        Assert.Equal(4, openVr.Count(nameof(FakeOpenVr.ClearOverlayTexture)));
    }

    /// <summary>And the session itself is given back after the quads, not before.</summary>
    [Fact]
    public void TheSessionGoesAfterTheQuads()
    {
        var openVr = new FakeOpenVr();

        var runtime = new SteamVrRuntime(
            [new FakeSurface()],
            NullLogger<SteamVrRuntime>.Instance,
            openVr);

        Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
        runtime.Stop();

        var shutdown = openVr.Calls.FindIndex(call => call.Function == nameof(FakeOpenVr.Shutdown));
        var lastDestroy = openVr.Calls.FindLastIndex(call => call.Function == nameof(FakeOpenVr.DestroyOverlay));

        Assert.NotEqual(-1, shutdown);
        Assert.True(lastDestroy < shutdown, "the quads have to be handed back while the session still exists");
    }

    /// <summary>Stopping twice is not an error, and gives nothing back twice.</summary>
    [Fact]
    public void StoppingTwiceGivesNothingBackTwice()
    {
        var openVr = new FakeOpenVr();

        var runtime = new SteamVrRuntime(
            [new FakeSurface()],
            NullLogger<SteamVrRuntime>.Instance,
            openVr);

        Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

        runtime.Stop();
        runtime.Stop();

        Assert.Equal(1, openVr.Count(nameof(FakeOpenVr.DestroyOverlay)));
        Assert.Equal(1, openVr.Count(nameof(FakeOpenVr.Shutdown)));

        // And the slot is free, which is the whole point of stopping.
        Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
        runtime.Stop();
    }
}
