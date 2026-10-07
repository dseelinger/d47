using Xunit;

namespace D47.Audio.Tests;

/// <summary>
/// <see cref="DefaultDeviceFollowPolicy.Poll"/> starts a reopen and returns without waiting for it, reports
/// the outcome on a later poll, and holds a second move back until the first reopen has finished.
/// </summary>
public class AReopenRunsOffTheTickTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Fact]
    public void PollReturnsWhileTheReopenIsStillRunning()
    {
        var device = new HeldDeviceReopener();
        var follow = new DefaultDeviceFollowPolicy(device);
        device.MakeDue();

        var move = follow.Poll(Now, followingDefault: true, configuredDeviceId: null, isBusy: () => false, interrupt: () => { });

        Assert.Null(move);
        Assert.True(device.Entered.Wait(Patience, TestContext.Current.CancellationToken));
        Assert.NotEqual(Environment.CurrentManagedThreadId, device.ReopenThread);

        // Still held: the poll came back before the reopen did.
        Assert.False(device.Finished.IsSet);

        device.Release.Set();
        Assert.True(device.Finished.Wait(Patience, TestContext.Current.CancellationToken));

        DefaultDeviceFollowPolicy.Move? reported = null;
        Assert.True(SpinWait.SpinUntil(
            () => (reported = follow.Poll(Now, true, null, () => false, () => { })) is not null,
            Patience));

        Assert.Equal("Old Device", reported!.Value.OldDeviceName);
        Assert.Equal("New Device", reported.Value.NewDeviceName);
        Assert.Null(reported.Value.Error);
    }

    [Fact]
    public void ASecondMoveIsFollowedAfterTheFirstReopenNotAlongsideIt()
    {
        var device = new FakeDeviceReopener();
        var queued = new List<Action>();
        var follow = new DefaultDeviceFollowPolicy(device, queued.Add);

        device.MakeDue();
        Assert.Null(follow.Poll(Now, true, null, () => false, () => { }));
        Assert.Single(queued);
        Assert.Equal(0, device.ReopenCount);

        // A second move settles while the first reopen is still queued.
        device.MakeDue();
        Assert.Null(follow.Poll(Now, true, null, () => false, () => { }));
        Assert.Single(queued);
        Assert.True(device.DefaultDeviceMoved(Now));

        queued[0]();
        Assert.Equal(1, device.ReopenCount);

        var first = follow.Poll(Now, true, null, () => false, () => { });
        Assert.NotNull(first);
        Assert.Equal(2, queued.Count);

        queued[1]();
        Assert.Equal(2, device.ReopenCount);
        Assert.NotNull(follow.Poll(Now, true, null, () => false, () => { }));
    }

    [Fact]
    public void AnInterruptedMoveIsReportedAsInterruptedOnceItFinishes()
    {
        var device = new FakeDeviceReopener();
        var queued = new List<Action>();
        var follow = new DefaultDeviceFollowPolicy(device, queued.Add);
        var interrupted = false;
        device.MakeDue();
        device.MakeGone();

        Assert.Null(follow.Poll(Now, true, null, () => true, () => interrupted = true));

        // The gate is reset on the tick, before the reopen runs.
        Assert.True(interrupted);
        Assert.Equal(0, device.ReopenCount);

        queued[0]();

        var move = follow.Poll(Now, true, null, () => true, () => { });
        Assert.NotNull(move);
        Assert.True(move.Value.Interrupted);
    }

    [Fact]
    public void AReopenThatThrowsIsReportedRatherThanLost()
    {
        var device = new HeldDeviceReopener { Throws = true };
        var queued = new List<Action>();
        var follow = new DefaultDeviceFollowPolicy(device, queued.Add);
        device.MakeDue();
        device.Release.Set();

        follow.Poll(Now, true, null, () => false, () => { });
        queued[0]();

        var move = follow.Poll(Now, true, null, () => false, () => { });
        Assert.NotNull(move);
        Assert.IsType<InvalidOperationException>(move.Value.Error);

        // The guard is released, so the next move is followed.
        device.MakeDue();
        follow.Poll(Now, true, null, () => false, () => { });
        Assert.Equal(2, queued.Count);
    }

    private sealed class HeldDeviceReopener : IDefaultDeviceReopener
    {
        private volatile bool _due;

        public string? OpenDeviceName { get; private set; } = "Old Device";

        public bool Throws { get; init; }

        public ManualResetEventSlim Entered { get; } = new();

        public ManualResetEventSlim Release { get; } = new();

        public ManualResetEventSlim Finished { get; } = new();

        public int? ReopenThread { get; private set; }

        public void MakeDue() => _due = true;

        public bool DefaultDeviceMoved(DateTimeOffset now) => _due;

        public void AcknowledgeDefaultDeviceMove() => _due = false;

        public bool OpenDeviceIsGone() => false;

        public void Reopen(string? deviceId)
        {
            ReopenThread = Environment.CurrentManagedThreadId;
            Entered.Set();
            Release.Wait(Patience);

            try
            {
                if (Throws)
                {
                    throw new InvalidOperationException("The device went away mid-open.");
                }

                OpenDeviceName = "New Device";
            }
            finally
            {
                Finished.Set();
            }
        }
    }
}
