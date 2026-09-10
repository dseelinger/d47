using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// A serve runs ten times a second and shares a GPU with Elite, so a surface that says nothing has
/// changed is neither rasterised nor uploaded.
/// </summary>
public class PixelsGoUpOnlyWhenTheSurfaceChangedTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 9, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnUnchangedSurfaceIsNeitherDrawnNorUploaded()
    {
        var openVr = new FakeOpenVr();
        var surface = new FakeSurface();

        var runtime = new SteamVrRuntime([surface], NullLogger<SteamVrRuntime>.Instance, openVr);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

            var panel = openVr.Live.Keys.Single();

            Assert.True(runtime.Serve(Start));

            Assert.Equal(1, surface.Draws);
            Assert.Equal(1, openVr.Count(nameof(FakeOpenVr.SetOverlayRaw), panel));

            surface.IsDirty = false;

            for (var tick = 1; tick <= 10; tick++)
            {
                Assert.True(runtime.Serve(Start + TimeSpan.FromMilliseconds(100 * tick)));
            }

            Assert.Equal(1, surface.Draws);
            Assert.Equal(1, openVr.Count(nameof(FakeOpenVr.SetOverlayRaw), panel));

            surface.IsDirty = true;

            Assert.True(runtime.Serve(Start + TimeSpan.FromSeconds(2)));

            Assert.Equal(2, surface.Draws);
            Assert.Equal(2, openVr.Count(nameof(FakeOpenVr.SetOverlayRaw), panel));
        }
        finally
        {
            runtime.Stop();
        }
    }

    /// <summary>A surface that is switched off is hidden and not drawn at all.</summary>
    [Fact]
    public void AnInvisibleSurfaceIsHiddenRatherThanDrawn()
    {
        var openVr = new FakeOpenVr();
        var surface = new FakeSurface { Visible = false };

        var runtime = new SteamVrRuntime([surface], NullLogger<SteamVrRuntime>.Instance, openVr);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

            var panel = openVr.Live.Keys.Single();

            Assert.True(runtime.Serve(Start));

            Assert.Equal(0, surface.Draws);
            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.SetOverlayRaw), panel));
            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.ShowOverlay), panel));
        }
        finally
        {
            runtime.Stop();
        }
    }

    /// <summary>A frame the compositor turned down goes again while the quad is on screen.</summary>
    [Fact]
    public void ARefusedFrameGoesAgainWithoutBeingRedrawn()
    {
        var openVr = new FakeOpenVr { RawRefused = true };
        var surface = new FakeSurface();

        var runtime = new SteamVrRuntime([surface], NullLogger<SteamVrRuntime>.Instance, openVr);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

            var panel = openVr.Live.Keys.Single();

            Assert.True(runtime.Serve(Start));
            surface.IsDirty = false;

            // Nothing has changed, but the held frame is still waiting and the quad is on screen.
            Assert.True(runtime.Serve(Start + TimeSpan.FromMilliseconds(100)));

            Assert.Equal(1, surface.Draws);
            Assert.Equal(2, openVr.Count(nameof(FakeOpenVr.SetOverlayRaw), panel));
        }
        finally
        {
            runtime.Stop();
        }
    }
}
