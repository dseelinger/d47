using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Valve.VR;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// An overlay queue that is never drained grows for the length of a play session, and the session
/// queue is where SteamVR says it is going away.
/// </summary>
public class EveryServeDrainsTheEventQueuesTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 9, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AQuadsQueueIsEmptiedRatherThanReadOnceAServe()
    {
        var openVr = new FakeOpenVr();
        var runtime = new SteamVrRuntime([new FakeSurface()], NullLogger<SteamVrRuntime>.Instance, openVr);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

            var panel = openVr.Live.Keys.Single();

            for (var waiting = 0; waiting < 3; waiting++)
            {
                openVr.Queue(panel, EVREventType.VREvent_MouseMove);
            }

            Assert.True(runtime.Serve(Start));

            Assert.Empty(openVr.OverlayEvents[panel]);

            // Three taken and one that came back empty, which is what drained means.
            Assert.Equal(4, openVr.Count(nameof(FakeOpenVr.PollNextOverlayEvent), panel));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void EveryServeOfEveryQuadAsksAgain()
    {
        var openVr = new FakeOpenVr();

        var runtime = new SteamVrRuntime(
            [new FakeSurface(), new FakeSurface { Surface = VrSurface.Captions }],
            NullLogger<SteamVrRuntime>.Instance,
            openVr);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

            for (var tick = 0; tick < 5; tick++)
            {
                Assert.True(runtime.Serve(Start + TimeSpan.FromMilliseconds(100 * tick)));
            }

            foreach (var quad in openVr.Live.Keys)
            {
                Assert.Equal(5, openVr.Count(nameof(FakeOpenVr.PollNextOverlayEvent), quad));
            }

            Assert.Equal(5, openVr.Count(nameof(FakeOpenVr.PollNextEvent)));
        }
        finally
        {
            runtime.Stop();
        }
    }

    /// <summary>SteamVR saying it is going away ends the serve, and is acknowledged so it can go.</summary>
    [Fact]
    public void SteamVrGoingAwayEndsTheServeAndIsAcknowledged()
    {
        var openVr = new FakeOpenVr();
        var surface = new FakeSurface();
        var runtime = new SteamVrRuntime([surface], NullLogger<SteamVrRuntime>.Instance, openVr);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

            openVr.SessionEvents.Enqueue(new VREvent_t { eventType = (uint)EVREventType.VREvent_Quit });

            Assert.False(runtime.Serve(Start));

            Assert.Equal(1, openVr.Count(nameof(FakeOpenVr.AcknowledgeQuit_Exiting)));
            Assert.Equal(0, surface.Draws);
        }
        finally
        {
            runtime.Stop();
        }
    }
}
