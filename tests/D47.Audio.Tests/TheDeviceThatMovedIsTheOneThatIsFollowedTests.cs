using Microsoft.Extensions.Logging.Abstractions;
using NAudio.CoreAudioApi;
using Xunit;

namespace D47.Audio.Tests;

/// <summary>
/// <see cref="WasapiAudioSink"/> and <see cref="WasapiMicrophone"/> each wire their own
/// <see cref="DefaultDeviceFollower"/> to the Windows role they follow (<see cref="Role.Console"/>) and
/// the flow they own — a stubbed enumerator's notification is the only thing that can make
/// <c>DefaultDeviceMoved</c> come due (#67).
/// </summary>
public class TheDeviceThatMovedIsTheOneThatIsFollowedTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UtcNow;

    [Fact]
    public void TheAudioSinkComesDueOnlyForARenderConsoleChange()
    {
        var enumerator = new FakeEndpointEnumerator();
        using var sink = new WasapiAudioSink(NullLogger<WasapiAudioSink>.Instance, enumerator);

        Assert.False(sink.DefaultDeviceMoved(Start));

        enumerator.Raise(DataFlow.Capture, Role.Console);
        enumerator.Raise(DataFlow.Render, Role.Communications);
        Assert.False(sink.DefaultDeviceMoved(Start + TimeSpan.FromSeconds(5)));

        enumerator.Raise(DataFlow.Render, Role.Console);
        Assert.False(sink.DefaultDeviceMoved(Start + TimeSpan.FromSeconds(6)));
        Assert.True(sink.DefaultDeviceMoved(Start + TimeSpan.FromSeconds(7)));

        sink.AcknowledgeDefaultDeviceMove();
        Assert.False(sink.DefaultDeviceMoved(Start + TimeSpan.FromSeconds(8)));
    }

    [Fact]
    public void TheMicrophoneComesDueOnlyForACaptureConsoleChange()
    {
        var enumerator = new FakeEndpointEnumerator();
        using var microphone = new WasapiMicrophone(
            new RecordingCaptureSink(), NullLogger<WasapiMicrophone>.Instance, enumerator);

        Assert.False(microphone.DefaultDeviceMoved(Start));

        enumerator.Raise(DataFlow.Render, Role.Console);
        enumerator.Raise(DataFlow.Capture, Role.Communications);
        Assert.False(microphone.DefaultDeviceMoved(Start + TimeSpan.FromSeconds(5)));

        enumerator.Raise(DataFlow.Capture, Role.Console);
        Assert.False(microphone.DefaultDeviceMoved(Start + TimeSpan.FromSeconds(6)));
        Assert.True(microphone.DefaultDeviceMoved(Start + TimeSpan.FromSeconds(7)));

        microphone.AcknowledgeDefaultDeviceMove();
        Assert.False(microphone.DefaultDeviceMoved(Start + TimeSpan.FromSeconds(8)));
    }

    [Fact]
    public void NeitherReportsItsUnopenedDeviceAsGone()
    {
        var enumerator = new FakeEndpointEnumerator();
        using var sink = new WasapiAudioSink(NullLogger<WasapiAudioSink>.Instance, enumerator);
        using var microphone = new WasapiMicrophone(
            new RecordingCaptureSink(), NullLogger<WasapiMicrophone>.Instance, enumerator);

        Assert.False(sink.OpenDeviceIsGone());
        Assert.False(microphone.OpenDeviceIsGone());
    }
}
