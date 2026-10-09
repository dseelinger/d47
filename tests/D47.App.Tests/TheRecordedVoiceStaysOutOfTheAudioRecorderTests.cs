using D47.App.Recording;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Diagnostics.Recording;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

[Trait("Category", "Integration")]
public class TheRecordedVoiceStaysOutOfTheAudioRecorderTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-own-voice-ring", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private sealed class Tap : IRenderReferenceTap
    {
        public event Action<RenderReferenceFrame>? Rendered;

        public void Render(int bytes) =>
            Rendered?.Invoke(new RenderReferenceFrame(0, new byte[bytes], AudioFormat.Standard));
    }

    private sealed class Sink : IAudioSink
    {
        public List<long> Started { get; } = [];

        public event Action<long>? Finished;

        public IRenderReferenceTap ReferenceTap { get; } = new Tap();

        public void Play(PlaybackRequest request) => Started.Add(request.Id);

        public void Stop(long playbackId)
        {
        }

        public void StopAll()
        {
        }

        public void Pause(long playbackId)
        {
        }

        public void Resume(long playbackId)
        {
        }

        public void SetGain(long playbackId, float gain)
        {
        }

        public void Finish(long playbackId) => Finished?.Invoke(playbackId);
    }

    [Fact]
    public void PlayingTheRecordingBackLeavesNoRow()
    {
        var log = new RecordingLog(_folder, NullLogger.Instance);
        var sink = new Sink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var tap = new Tap();
        var recording = new AudioClip(OwnVoice.Sentence, new byte[24_000 * 2 * 6], new AudioFormat(24_000, 1));

        using (var recorder = AudioRecorder.Regardless(log, () => DateTimeOffset.UnixEpoch, NullLogger.Instance))
        {
            recorder.Watch(arbiter, tap);

            arbiter.Enqueue(OwnVoiceRecording.Playback(recording));
            tap.Render(96_000);
            tap.Render(96_000);
            sink.Finish(Assert.Single(sink.Started));
        }

        Assert.Empty(log.Rows);
    }
}
