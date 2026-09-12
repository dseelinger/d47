using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Audio.Tests;

/// <summary>
/// <see cref="DefaultDeviceFollowPolicy"/> waits for whatever is using a moved Default Device to go idle
/// before reopening on it, unless the device that was open has itself disappeared — in which case it
/// interrupts rather than waiting for a device that no longer exists (#67).
/// </summary>
public class TheSwitchWaitsForTheArbiterToBeIdleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static AudioClip Clip() => new("line", new byte[4800], AudioFormat.Standard);

    [Fact]
    public void NothingHappensWhenNoMoveIsDue()
    {
        var device = new FakeDeviceReopener();
        var interrupted = false;

        var move = DefaultDeviceFollowPolicy.Poll(
            device, Now, followingDefault: true, configuredDeviceId: null, isBusy: () => false, interrupt: () => interrupted = true);

        Assert.Null(move);
        Assert.Equal(0, device.ReopenCount);
        Assert.False(interrupted);
    }

    [Fact]
    public void AChosenDeviceAbsorbsTheMoveWithoutReopening()
    {
        var device = new FakeDeviceReopener();
        device.MakeDue();
        var interrupted = false;

        var move = DefaultDeviceFollowPolicy.Poll(
            device, Now, followingDefault: false, configuredDeviceId: "chosen-id", isBusy: () => true, interrupt: () => interrupted = true);

        Assert.Null(move);
        Assert.Equal(0, device.ReopenCount);
        Assert.False(interrupted);

        // Acknowledged: it does not keep asking every tick.
        Assert.False(device.DefaultDeviceMoved(Now));
    }

    [Fact]
    public void AnIdleDeviceReopensStraightAway()
    {
        var device = new FakeDeviceReopener();
        device.MakeDue();

        var move = DefaultDeviceFollowPolicy.Poll(
            device, Now, followingDefault: true, configuredDeviceId: null, isBusy: () => false, interrupt: () => throw new Exception("should not interrupt"));

        Assert.NotNull(move);
        Assert.False(move.Value.Interrupted);
        Assert.Equal("Old Device", move.Value.OldDeviceName);
        Assert.Equal("New Device", move.Value.NewDeviceName);
        Assert.Equal(1, device.ReopenCount);
    }

    [Fact]
    public void ALineStillPlayingThroughTheArbiterIsNotTruncated()
    {
        var sink = new FakeAudioSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Speech, Clip = Clip() });

        Assert.True(arbiter.IsSpeaking);

        var device = new FakeDeviceReopener();
        device.MakeDue();

        var move = DefaultDeviceFollowPolicy.Poll(
            device, Now, followingDefault: true, configuredDeviceId: null, isBusy: () => arbiter.IsSpeaking, interrupt: arbiter.Silence);

        Assert.Null(move);
        Assert.Equal(0, device.ReopenCount);
        Assert.False(sink.StoppedAll);
        Assert.True(arbiter.IsSpeaking);

        // Once the line finishes on its own, the still-due move goes through on the next poll.
        sink.Complete(sink.Played[0].Id);
        Assert.False(arbiter.IsSpeaking);

        move = DefaultDeviceFollowPolicy.Poll(
            device, Now, followingDefault: true, configuredDeviceId: null, isBusy: () => arbiter.IsSpeaking, interrupt: arbiter.Silence);

        Assert.NotNull(move);
        Assert.False(move.Value.Interrupted);
        Assert.Equal(1, device.ReopenCount);
    }

    [Fact]
    public void ADeviceThatDisappearedInterruptsTheLineAndSaysSo()
    {
        var sink = new FakeAudioSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Speech, Clip = Clip() });

        Assert.True(arbiter.IsSpeaking);

        var device = new FakeDeviceReopener();
        device.MakeDue();
        device.MakeGone();

        var move = DefaultDeviceFollowPolicy.Poll(
            device, Now, followingDefault: true, configuredDeviceId: null, isBusy: () => arbiter.IsSpeaking, interrupt: arbiter.Silence);

        Assert.NotNull(move);
        Assert.True(move.Value.Interrupted);
        Assert.True(sink.StoppedAll);
        Assert.False(arbiter.IsSpeaking);
        Assert.Equal(1, device.ReopenCount);
    }
}
