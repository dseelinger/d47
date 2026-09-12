using Microsoft.Extensions.Logging.Abstractions;
using NAudio.CoreAudioApi;
using Xunit;

namespace D47.Audio.Tests;

/// <summary>
/// D47 follows the Windows Default Device (<see cref="Role.Console"/>), not the Communications
/// device, on both the microphone and the speaker — and a Commander who has chosen a specific
/// device is unaffected by which role "system default" follows (#65).
/// </summary>
public class TheDefaultDeviceIsTheOneSoundSettingsShowsTests
{
    private static readonly AudioEndpoint ConsoleCapture = new("capture-console", "Console Microphone");
    private static readonly AudioEndpoint CommsCapture = new("capture-comms", "Comms Headset Mic");
    private static readonly AudioEndpoint ConsoleRender = new("render-console", "Console Speakers");
    private static readonly AudioEndpoint CommsRender = new("render-comms", "Comms Headset Speaker");
    private static readonly AudioEndpoint ChosenCapture = new("capture-chosen", "Chosen Microphone");
    private static readonly AudioEndpoint ChosenRender = new("render-chosen", "Chosen Speaker");

    [Fact]
    public void TheMicrophoneNamesTheConsoleDeviceWhenTheTwoDefaultsDiffer()
    {
        using var microphone = new WasapiMicrophone(
            new RecordingCaptureSink(), NullLogger<WasapiMicrophone>.Instance, SplitEnumerator());

        Assert.Equal(ConsoleCapture.Name, microphone.DefaultDeviceName());
        Assert.Equal(ConsoleCapture, microphone.Resolve(null));
    }

    [Fact]
    public void TheAudioSinkNamesTheConsoleDeviceWhenTheTwoDefaultsDiffer()
    {
        using var sink = new WasapiAudioSink(NullLogger<WasapiAudioSink>.Instance, SplitEnumerator());

        Assert.Equal(ConsoleRender.Name, sink.DefaultDeviceName());
        Assert.Equal(ConsoleRender, sink.ResolveEndpoint(null));
    }

    [Fact]
    public void AChosenMicrophoneIsUnaffectedByWhichDefaultRoleIsFollowed()
    {
        using var microphone = new WasapiMicrophone(
            new RecordingCaptureSink(), NullLogger<WasapiMicrophone>.Instance, SplitEnumerator());

        Assert.Equal(ChosenCapture, microphone.Resolve(ChosenCapture.Id));
    }

    [Fact]
    public void AChosenOutputDeviceIsUnaffectedByWhichDefaultRoleIsFollowed()
    {
        using var sink = new WasapiAudioSink(NullLogger<WasapiAudioSink>.Instance, SplitEnumerator());

        Assert.Equal(ChosenRender, sink.ResolveEndpoint(ChosenRender.Id));
    }

    /// <summary>The console and communications roles resolve to different devices in each direction.</summary>
    private static IAudioEndpointEnumerator SplitEnumerator() => new StubEndpointEnumerator(
        capture: [ConsoleCapture, CommsCapture, ChosenCapture],
        render: [ConsoleRender, CommsRender, ChosenRender],
        defaultCapture: (Role.Console, ConsoleCapture),
        defaultCaptureComms: CommsCapture,
        defaultRender: (Role.Console, ConsoleRender),
        defaultRenderComms: CommsRender);

    private sealed class StubEndpointEnumerator(
        IReadOnlyList<AudioEndpoint> capture,
        IReadOnlyList<AudioEndpoint> render,
        (Role Role, AudioEndpoint Endpoint) defaultCapture,
        AudioEndpoint defaultCaptureComms,
        (Role Role, AudioEndpoint Endpoint) defaultRender,
        AudioEndpoint defaultRenderComms) : IAudioEndpointEnumerator
    {
        public IReadOnlyList<AudioEndpoint> Active(DataFlow flow) => flow == DataFlow.Capture ? capture : render;

        event Action<DataFlow, Role>? IAudioEndpointEnumerator.DefaultDeviceChanged
        {
            add { }
            remove { }
        }

        public AudioEndpoint? Default(DataFlow flow, Role role) => (flow, role) switch
        {
            (DataFlow.Capture, var r) when r == defaultCapture.Role => defaultCapture.Endpoint,
            (DataFlow.Capture, Role.Communications) => defaultCaptureComms,
            (DataFlow.Render, var r) when r == defaultRender.Role => defaultRender.Endpoint,
            (DataFlow.Render, Role.Communications) => defaultRenderComms,
            _ => null,
        };
    }
}
