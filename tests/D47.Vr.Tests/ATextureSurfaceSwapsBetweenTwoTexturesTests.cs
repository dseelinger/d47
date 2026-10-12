using D47.Core.Vr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// A surface that uploads as a texture goes up through <c>SetOverlayTexture</c>, alternating two
/// Direct3D 11 textures; every other surface goes up through <c>SetOverlayRaw</c> as before.
/// </summary>
public class ATextureSurfaceSwapsBetweenTwoTexturesTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 20, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Tick(int n) => Start + TimeSpan.FromMilliseconds(100 * n);

    [Fact]
    public void ARawSurfaceNeverTouchesTheDevice()
    {
        var openVr = new FakeOpenVr();
        var device = new FakeTextureDevice();
        var runtime = new SteamVrRuntime([new FakeSurface()], NullLogger<SteamVrRuntime>.Instance, openVr, device);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
            var panel = openVr.Live.Keys.Single();

            Assert.True(runtime.Serve(Start));

            Assert.Equal(1, openVr.Count(nameof(FakeOpenVr.SetOverlayRaw), panel));
            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.SetOverlayTexture)));
            Assert.Empty(device.Opens);
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void ConsecutiveFramesAlternateTwoTextures()
    {
        var openVr = new FakeOpenVr { Adapter = 2 };
        var device = new FakeTextureDevice();
        var surface = new FakeSurface { UploadsAsTexture = true };
        var runtime = new SteamVrRuntime([surface], NullLogger<SteamVrRuntime>.Instance, openVr, device);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
            var panel = openVr.Live.Keys.Single();

            for (var n = 0; n < 3; n++)
            {
                Assert.True(runtime.Serve(Tick(n)));
            }

            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.SetOverlayRaw)));
            Assert.Equal(3, openVr.Count(nameof(FakeOpenVr.SetOverlayTexture), panel));

            var sent = openVr.TexturesSubmitted;
            Assert.NotEqual(sent[0], sent[1]);
            Assert.Equal(sent[0], sent[2]);

            // Each frame is written into the texture it is then submitted from.
            Assert.Equal(sent, device.Writes);
            Assert.Equal([2], device.Opens);
            Assert.Equal(2, device.Created.Count);
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void ARefusedTextureGoesAgainWithoutBeingRewritten()
    {
        var openVr = new FakeOpenVr { TextureRefused = true };
        var device = new FakeTextureDevice();
        var surface = new FakeSurface { UploadsAsTexture = true };
        var runtime = new SteamVrRuntime([surface], NullLogger<SteamVrRuntime>.Instance, openVr, device);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

            Assert.True(runtime.Serve(Tick(0)));
            surface.IsDirty = false;

            // Nothing has changed, but the held frame is still waiting and the quad is on screen.
            Assert.True(runtime.Serve(Tick(1)));

            Assert.Equal(1, surface.Draws);
            Assert.Single(device.Writes);
            Assert.Equal(2, openVr.TexturesSubmitted.Count);
            Assert.Equal(openVr.TexturesSubmitted[0], openVr.TexturesSubmitted[1]);

            openVr.TextureRefused = false;
            Assert.True(runtime.Serve(Tick(2)));

            surface.IsDirty = true;
            Assert.True(runtime.Serve(Tick(3)));

            // Taken on the third try, so the next frame moves to the other texture.
            Assert.Equal(openVr.TexturesSubmitted[0], openVr.TexturesSubmitted[2]);
            Assert.NotEqual(openVr.TexturesSubmitted[0], openVr.TexturesSubmitted[3]);
            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.SetOverlayRaw)));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void WithNoDeviceATextureSurfaceGoesUpRawAndSaysWhyOnce()
    {
        var openVr = new FakeOpenVr();
        var device = new FakeTextureDevice { OpenRefused = "no adapter answered" };
        var lines = new List<string>();
        var runtime = new SteamVrRuntime(
            [new FakeSurface { UploadsAsTexture = true }],
            new Capture(lines),
            openVr,
            device);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
            var panel = openVr.Live.Keys.Single();

            for (var n = 0; n < 3; n++)
            {
                Assert.True(runtime.Serve(Tick(n)));
            }

            Assert.Equal(3, openVr.Count(nameof(FakeOpenVr.SetOverlayRaw), panel));
            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.SetOverlayTexture)));
            Assert.Single(device.Opens);
            Assert.Single(lines, line => line.Contains("no adapter answered", StringComparison.Ordinal));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void WhenATextureCannotBeMadeTheSurfaceGoesUpRaw()
    {
        var openVr = new FakeOpenVr();
        var device = new FakeTextureDevice { CreateRefused = "out of memory" };
        var lines = new List<string>();
        var runtime = new SteamVrRuntime(
            [new FakeSurface { UploadsAsTexture = true }],
            new Capture(lines),
            openVr,
            device);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

            Assert.True(runtime.Serve(Tick(0)));
            Assert.True(runtime.Serve(Tick(1)));

            Assert.Equal(2, openVr.Count(nameof(FakeOpenVr.SetOverlayRaw)));
            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.SetOverlayTexture)));
            Assert.Single(lines, line => line.Contains("out of memory", StringComparison.Ordinal));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void WithoutADeviceATextureSurfaceGoesUpRaw()
    {
        var openVr = new FakeOpenVr();
        var runtime = new SteamVrRuntime(
            [new FakeSurface { UploadsAsTexture = true }],
            NullLogger<SteamVrRuntime>.Instance,
            openVr);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
            Assert.True(runtime.Serve(Start));

            Assert.Equal(1, openVr.Count(nameof(FakeOpenVr.SetOverlayRaw)));
            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.SetOverlayTexture)));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void AResizeRemakesBothTextures()
    {
        var openVr = new FakeOpenVr();
        var device = new FakeTextureDevice();
        var surface = new FakeSurface { UploadsAsTexture = true };
        var runtime = new SteamVrRuntime([surface], NullLogger<SteamVrRuntime>.Instance, openVr, device);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
            Assert.True(runtime.Serve(Tick(0)));

            var before = device.Live.ToHashSet();

            surface.Size = (16, 16);
            Assert.True(runtime.Serve(Tick(1)));

            Assert.Equal(4, device.Created.Count);
            Assert.Equal(2, device.Live.Count);
            Assert.Empty(device.Live.Intersect(before));
            Assert.Single(device.Opens);
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void StopReleasesTheDeviceAndEveryTextureAndAStartMakesThemAgain()
    {
        var openVr = new FakeOpenVr();
        var device = new FakeTextureDevice();
        var runtime = new SteamVrRuntime(
            [new FakeSurface { UploadsAsTexture = true }],
            NullLogger<SteamVrRuntime>.Instance,
            openVr,
            device);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
            Assert.True(runtime.Serve(Tick(0)));
            Assert.Equal(2, device.Live.Count);

            runtime.Stop();

            Assert.Empty(device.Live);
            Assert.False(device.IsOpen);
            Assert.Equal(1, device.Closes);

            // A second Stop gives nothing back twice.
            runtime.Stop();
            Assert.Equal(1, device.Closes);

            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
            Assert.True(runtime.Serve(Tick(1)));

            Assert.Equal(2, device.Opens.Count);
            Assert.True(device.IsOpen);
            Assert.Equal(2, device.Live.Count);
            Assert.Equal(4, device.Created.Count);
        }
        finally
        {
            runtime.Stop();
        }
    }

    private sealed class Capture(List<string> lines) : ILogger<SteamVrRuntime>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            lines.Add(formatter(state, exception));
    }
}
