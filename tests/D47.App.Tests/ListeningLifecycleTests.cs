using D47.Audio;
using D47.Core.Listening;
using D47.Stt;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The microphone and the transcriber are owned for the life of the process and are opened and closed
/// as listening settings change.
/// </summary>
public class ListeningLifecycleTests
{
    private const string NoSuchDevice = "d47-tests-no-such-capture-device";

    /// <summary>
    /// Every listening.* change with no speech model selected takes this path, so it happens on binding
    /// a key, unbinding it, and changing any other listening row.
    /// </summary>
    [Fact]
    public void TheTranscriberCanBeUnloadedRepeatedly()
    {
        using var transcriber = new WhisperTranscriber(NullLogger<WhisperTranscriber>.Instance);

        transcriber.Unload();
        transcriber.Unload();
        transcriber.Unload();

        Assert.False(transcriber.IsReady);
        Assert.Null(transcriber.Model);
    }

    /// <summary>The shutdown path runs after the settings path has already run.</summary>
    [Fact]
    public void DisposingTheTranscriberTwiceIsNotAnError()
    {
        var transcriber = new WhisperTranscriber(NullLogger<WhisperTranscriber>.Instance);

        transcriber.Dispose();
        transcriber.Dispose();
    }

    /// <summary>Starting with no key bound closes the microphone; binding one has to reopen it.</summary>
    [Fact]
    public void TheMicrophoneReopensAfterBeingClosed()
    {
        using var microphone = new WasapiMicrophone(Gate(), NullLogger<WasapiMicrophone>.Instance);

        microphone.Close();
        microphone.Close();

        microphone.Open(NoSuchDevice);

        Assert.False(microphone.IsCapturing);
        Assert.NotNull(microphone.Unavailable);
    }

    /// <summary>Disposal still means disposal — closing is the reversible one.</summary>
    [Fact]
    public void TheMicrophoneRefusesToReopenAfterDisposal()
    {
        var microphone = new WasapiMicrophone(Gate(), NullLogger<WasapiMicrophone>.Instance);

        microphone.Dispose();
        microphone.Dispose();

        Assert.Throws<ObjectDisposedException>(() => microphone.Open(NoSuchDevice));
    }

    private static ListenGate Gate() =>
        new(WasapiMicrophone.SampleRate, NullLogger<ListenGate>.Instance);
}
