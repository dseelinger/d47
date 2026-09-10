using System.Numerics;
using System.Runtime.InteropServices;
using D47.Core.Interface;
using D47.Core.Vr;
using D47.Vr;
using D47.Vr.Binding;
using Microsoft.Extensions.Logging.Abstractions;
using Valve.VR;
using Xunit;

namespace D47.App.Tests;

/// <summary>The real runtime's attach path, exercised on a machine with no headset session.</summary>
public class SteamVrAttachTests
{
    /// <summary>Why this file declined to run, or null when it ran.</summary>
    private static string? Live =>
        SteamVrRuntime.SteamVrIsRunning()
            ? "SteamVR is running on this machine, so the attach path has nothing to prove here — "
              + "SteamVrLiveTests takes the session that is up."
            : null;

    /// <summary>A first attempt that produced no session must leave the one-session slot free: the retry loop calls Start every few seconds forever, and a leaked slot makes every later attempt a permanent "already running".</summary>
    [Fact]
    public void AnAttemptThatFindsNoSessionCanBeRetried()
    {
        Assert.SkipWhen(Live is not null, Live ?? string.Empty);

        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance, OpenVrBinding.Instance);

        var first = runtime.Start();

        Assert.NotEqual(VrStartOutcome.Started, first.Outcome);

        var second = runtime.Start();

        Assert.NotEqual(VrStartOutcome.Failed, second.Outcome);
        Assert.Equal(first.Outcome, second.Outcome);
    }

    /// <summary>And it says which of the two waiting conditions it is: "SteamVR is not running" and "no headset is switched on" send the Commander to different switches.</summary>
    [Fact]
    public void WaitingSaysWhatItIsWaitingFor()
    {
        Assert.SkipWhen(Live is not null, Live ?? string.Empty);

        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance, OpenVrBinding.Instance);

        var start = runtime.Start();

        try
        {
            Assert.True(
                start.Outcome is VrStartOutcome.NoRuntime or VrStartOutcome.NotReady,
                $"with no session up, an attach attempt should wait, but got {start.Outcome}: {start.Detail}");

            if (start.Outcome == VrStartOutcome.NotReady)
            {
                Assert.NotNull(start.Detail);
                Assert.Contains("attach when", start.Detail);
            }
        }
        finally
        {
            runtime.Stop();
        }
    }
}

/// <summary>Attaching to a session that is actually there.</summary>
public class SteamVrLiveTests
{
    /// <summary>
    /// Why this file declined to run, or null when it ran. Attaching does not wake a headset that is
    /// switched off, so these preconditions are the whole gate: any local run with SteamVR up and
    /// d47 closed takes these tests.
    /// </summary>
    private static string? Waiting
    {
        get
        {
            if (!OpenVrBinding.Instance.Load())
            {
                return "No SteamVR runtime on this machine, which is a supported configuration.";
            }

            if (!SteamVrRuntime.SteamVrIsRunning())
            {
                return "SteamVR is not running, so there is no session to round-trip against.";
            }

            if (!OpenVrBinding.Instance.IsHmdPresent())
            {
                return "No headset is switched on.";
            }

            // A running copy owns the overlay keys, and the manifest below names this process as d47 to
            // SteamVR. Neither belongs in a session that is already d47's.
            return System.Diagnostics.Process.GetProcessesByName("d47").Length > 0
                ? "d47 is running; close it to run the live checks."
                : null;
        }
    }

    /// <summary>Skips unless this machine can answer, in the one place that decides it.</summary>
    private static void Ready() => Assert.SkipWhen(Waiting is not null, Waiting ?? string.Empty);

    [Fact]
    public void ARealSessionIsAttachedToRatherThanRefused()
    {
        Ready();

        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance, OpenVrBinding.Instance);

        var start = runtime.Start();

        try
        {
            Assert.NotEqual(VrStartOutcome.Failed, start.Outcome);
        }
        finally
        {
            // Put the session back the way it was found, whatever happened.
            runtime.Stop();
        }
    }

    /// <summary>
    /// Its own key rather than the panel's, so the round-trips run whether or not d47 is up. What the
    /// production keys prove — that a second copy is turned away — is covered above the binding, in
    /// D47.Vr.Tests.
    /// </summary>
    private const string TestKey = "com.dseelinger.D47.livetest";

    /// <summary>
    /// Every setter and getter in one pass. This is what only a real runtime can answer: whether the
    /// vendored header's struct layouts and the installed runtime agree.
    /// </summary>
    [Fact]
    public void WhatIsSetOnAQuadIsWhatReadsBackOffIt()
    {
        Attach();

        try
        {
            var complaints = new List<string>();
            var overlay = Quad(complaints);

            try
            {
                overlay.Look(widthMetres: 0.8f, curvature: 0.35f, opacity: 0.62f);
                overlay.PlaceAbsolute(new VrPose(
                    new Vector3(0.11f, -0.22f, -1.33f),
                    Quaternion.CreateFromYawPitchRoll(0.3f, -0.2f, 0.1f)));

                var described = overlay.Describe();

                Assert.Contains("alpha=0.62", described);
                Assert.Contains("width=0.80m", described);
                Assert.Contains("universe=TrackingUniverseSeated", described);
                Assert.Contains("at (0.11, -0.22, -1.33)", described);

                // Hung off the headset instead, which is a different transform type and a different getter.
                overlay.PlaceOnHead(new VrPose(new Vector3(0f, -0.25f, -1.1f), Quaternion.Identity));

                Assert.Contains("riding device 0", overlay.Describe());

                // Curvature has no getter d47 reads, so the runtime taking the call is the whole answer.
                Assert.Empty(complaints);
            }
            finally
            {
                overlay.Dispose();
            }
        }
        finally
        {
            OpenVrBinding.Instance.Shutdown();
        }
    }

    /// <summary>Every resolution the panel offers, at the size it would actually go up at.</summary>
    [Fact]
    public void ThePanelRasterGoesUpAtEverySizeItIsOffered()
    {
        Attach();

        try
        {
            var complaints = new List<string>();
            var overlay = Quad(complaints);

            try
            {
                foreach (var (width, height) in PanelResolution.Steps)
                {
                    var pixels = new byte[width * height * 4];
                    var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);

                    try
                    {
                        Assert.True(
                            overlay.Submit(pinned.AddrOfPinnedObject(), width, height),
                            $"SteamVR would not take a {width}x{height} raster");
                    }
                    finally
                    {
                        pinned.Free();
                    }
                }

                Assert.Empty(complaints);
            }
            finally
            {
                overlay.Dispose();
            }
        }
        finally
        {
            OpenVrBinding.Instance.Shutdown();
        }
    }

    /// <summary>The manifest the installed runtime actually parses, and the handles it hands back.</summary>
    [Fact]
    public void TheActionManifestLoadsAndEveryHandleResolves()
    {
        Attach();

        var folder = Path.Combine(Path.GetTempPath(), $"d47-vr-live-{Guid.NewGuid():n}");

        try
        {
            var input = new VrActionInput(NullLogger.Instance, OpenVrBinding.Instance);

            input.Register(folder);

            Assert.True(input.Ready, "the action set and both action handles should resolve against a real runtime");

            // No press can be injected, so what is proved here is that the two action structs marshal at the
            // sizes the installed runtime expects: a claim that goes through and comes back.
            input.TriggerHeld(true);
            Assert.True(input.HoldingPriority);

            _ = input.BackPressed();

            input.Release();
            Assert.False(input.HoldingPriority, "the release should be accepted rather than left standing");
        }
        finally
        {
            OpenVrBinding.Instance.Shutdown();

            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A temp folder that would not go is not a test failure.
            }
        }
    }

    /// <summary>
    /// Both event queues drained. <c>VREvent_t</c> is the largest struct crossing the boundary and the
    /// one a header disagreement shows up in first.
    /// </summary>
    [Fact]
    public void BothEventPumpsDrain()
    {
        var system = Attach();

        try
        {
            var complaints = new List<string>();
            var overlay = Quad(complaints);

            try
            {
                var next = new VREvent_t();
                var size = (uint)Marshal.SizeOf<VREvent_t>();

                var drained = 0;

                while (system.PollNextEvent(ref next, size) && drained < 1000)
                {
                    drained++;
                }

                Assert.True(drained < 1000, "the session queue never emptied, which is a size disagreement");

                overlay.PumpEvents();

                // Still answering afterwards, which a corrupted marshal would not be.
                Assert.Contains("visible=", overlay.Describe());
                Assert.Empty(complaints);
            }
            finally
            {
                overlay.Dispose();
            }
        }
        finally
        {
            OpenVrBinding.Instance.Shutdown();
        }
    }

    /// <summary>Attaches to the session that is already up, or skips. The caller shuts it down.</summary>
    private static IOpenVrSystem Attach()
    {
        Ready();

        var error = EVRInitError.None;
        var system = OpenVrBinding.Instance.Init(ref error, EVRApplicationType.VRApplication_Overlay);

        Assert.Equal(EVRInitError.None, error);
        Assert.NotNull(system);

        return system;
    }

    /// <summary>A quad of this suite's own, whose refusals land in <paramref name="complaints"/>.</summary>
    private static VrOverlay Quad(List<string> complaints)
    {
        var overlay = VrOverlay.Create(
            OpenVrBinding.Instance,
            TestKey,
            "D47 live test",
            out var failure,
            complaints.Add);

        Assert.True(overlay is not null, $"SteamVR would not create the test quad: {failure.Detail}");

        return overlay!;
    }
}
