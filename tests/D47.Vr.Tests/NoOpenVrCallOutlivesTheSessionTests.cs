using System.Diagnostics;
using System.Numerics;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Valve.VR;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// The aim thread runs at ninety hertz while the tick thread ends the session. A native call left in
/// flight across the shutdown faults the process rather than throwing, so the teardown waits for the
/// frame and every frame after it finds no session (#134).
/// </summary>
public class NoOpenVrCallOutlivesTheSessionTests
{
    [Fact]
    public void ATeardownWaitsForTheFrameInFlightAndSilencesEveryFrameAfterIt()
    {
        var openVr = new FakeOpenVr();

        openVr.Poses[OpenVR.k_unTrackedDeviceIndex_Hmd] = FakeOpenVr.Tracked(Matrix4x4.Identity);
        openVr.Poses[3] = FakeOpenVr.Tracked(Matrix4x4.CreateTranslation(0.2f, -0.3f, -0.4f));
        openVr.Classes[3] = ETrackedDeviceClass.Controller;

        var runtime = new SteamVrRuntime(
            [new FakeSurface()],
            NullLogger<SteamVrRuntime>.Instance,
            openVr)
        {
            // Set before the session comes up, so the beam and the cursor exist to be aimed.
            Pointing = true,
        };

        Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

        var inside = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var reached = 0;

        // The first pose read the aim thread makes stops inside the call, still holding the runtime's lock.
        openVr.Entered = function =>
        {
            if (function == nameof(FakeOpenVr.GetDeviceToAbsoluteTrackingPose)
                && Interlocked.Exchange(ref reached, 1) == 0)
            {
                inside.Set();
                release.Wait();
            }
        };

        var stopping = false;
        var frames = 0;

        var aim = new Thread(() =>
        {
            while (!Volatile.Read(ref stopping))
            {
                var frame = Volatile.Read(ref frames);
                var where = new VrPose(new Vector3(0f, 0f, frame * 0.01f), Quaternion.Identity);

                runtime.HandsAndHead();
                runtime.Reposition(VrSurface.PanelFull, where);
                runtime.AimBeam(where, where, 1f + (frame * 0.01f));
                runtime.ShowCursor(new Vector3(0f, 0f, frame * 0.02f), where);

                Interlocked.Increment(ref frames);

                // The real loop runs on an eleven millisecond period; a tight one here would starve the
                // teardown of the lock rather than testing it.
                Thread.Sleep(1);
            }
        })
        {
            IsBackground = true,
            Name = "aim-under-test",
        };

        aim.Start();

        try
        {
            Assert.True(
                inside.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
                "The aim thread never reached OpenVR.");

            var teardown = new Thread(runtime.Stop) { IsBackground = true, Name = "stop-under-test" };
            teardown.Start();

            Assert.False(
                teardown.Join(TimeSpan.FromMilliseconds(250)),
                "Stop returned while an aim frame was still inside OpenVR.");

            release.Set();

            Assert.True(
                teardown.Join(TimeSpan.FromSeconds(5)),
                "Stop did not return once the aim frame had left OpenVR.");

            // Three more frames, every one of them against a session that has gone.
            var ran = Volatile.Read(ref frames);
            var waited = Stopwatch.StartNew();

            while (Volatile.Read(ref frames) < ran + 3 && waited.Elapsed < TimeSpan.FromSeconds(5))
            {
                Thread.Sleep(1);
            }

            Assert.True(Volatile.Read(ref frames) >= ran + 3, "The aim thread stopped running.");

            Volatile.Write(ref stopping, true);
            Assert.True(aim.Join(TimeSpan.FromSeconds(5)), "The aim thread did not stop.");

            var shutdown = openVr.Calls.FindIndex(call => call.Function == nameof(FakeOpenVr.Shutdown));

            Assert.True(shutdown >= 0, "The session was never shut down.");

            Assert.Equal(
                [],
                openVr.Calls.Skip(shutdown + 1).Select(call => call.Function).ToArray());
        }
        finally
        {
            release.Set();
            Volatile.Write(ref stopping, true);
            aim.Join(TimeSpan.FromSeconds(5));
            runtime.Stop();
        }
    }
}
