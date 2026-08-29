using D47.App.Flight;
using D47.Core.Audio;
using D47.Core.Diagnostics.Flight;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The spoken side of the audio flight recorder
/// (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>), driven with no audio device.
/// <para>
/// The recorder's whole job here is stitching: the tap produces a continuous stream of what went
/// to the speakers and knows nothing about where one utterance ends, and the arbiter knows
/// exactly that and never sees a sample. What is asserted is the seam between them — that a
/// change of clip closes one row and opens the next, that frames arriving while nothing is being
/// said are not written down, and that a row carries the provider and phonemes the pipeline
/// reported for the sentence it is playing.
/// </para>
/// </summary>
public class OneUtteranceIsOneRowTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static readonly AudioFormat Rendered = new(48_000, 2);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-flight-stitch", Guid.NewGuid().ToString("N"));

    private int _tick;

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>The arbiter's tap, driven by hand. The real one is a pass-through in the mixer.</summary>
    private sealed class Tap : IRenderReferenceTap
    {
        public event Action<RenderReferenceFrame>? Rendered;

        public void Render(int bytes, AudioFormat format) =>
            Rendered?.Invoke(new RenderReferenceFrame(0, new byte[bytes], format));
    }

    /// <summary>
    /// Enough of a sink for the arbiter to run against: it starts and finishes clips on demand
    /// and renders nothing, which is architecture.md §8's null sink.
    /// </summary>
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

        public void SetGain(long playbackId, float gain)
        {
        }

        public void Finish(long playbackId) => Finished?.Invoke(playbackId);
    }

    /// <summary>A clock that moves a second per read, so two rows cannot share an identity.</summary>
    private DateTimeOffset Now() => Noon.AddSeconds(Interlocked.Increment(ref _tick));

    private FlightLog Log() => new(_folder, NullLogger.Instance);

    private static AudioRequest Speech(string caption) =>
        new()
        {
            Channel = AudioChannel.Speech,
            Clip = new AudioClip(caption, new byte[16], AudioFormat.Standard),
            Group = "turn-1",
            Caption = caption,
        };

    [Fact]
    public void What_played_while_one_clip_was_current_becomes_one_row()
    {
        var log = Log();
        var sink = new Sink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var tap = new Tap();

        using (var recorder = AudioFlightRecorder.Regardless(log, Now, NullLogger.Instance))
        {
            recorder.Watch(arbiter, tap);

            // Nothing is playing yet, so nothing here is anybody speaking.
            tap.Render(4_000, Rendered);

            arbiter.Enqueue(Speech("You are in Sol."));

            // Half a second of the mix each: 48,000 frames a second at four bytes a frame.
            tap.Render(96_000, Rendered);
            tap.Render(96_000, Rendered);

            sink.Finish(sink.Started[0]);
        }

        var row = Assert.Single(log.Rows);

        Assert.Equal(FlightDirection.Spoken, row.Direction);
        Assert.Equal("You are in Sol.", row.Text);

        // The two frames that arrived while it was playing, and not the one that arrived
        // before anything was.
        Assert.Equal(TimeSpan.FromSeconds(1), row.Duration);
    }

    /// <summary>
    /// A second sentence is a second row rather than more of the first. The arbiter's clip id is
    /// what says so; the audio itself is one unbroken stream and cannot.
    /// </summary>
    [Fact]
    public void A_second_clip_is_a_second_row()
    {
        var log = Log();
        var sink = new Sink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var tap = new Tap();

        using (var recorder = AudioFlightRecorder.Regardless(log, Now, NullLogger.Instance))
        {
            recorder.Watch(arbiter, tap);

            arbiter.Enqueue(Speech("You are in Sol."));
            arbiter.Enqueue(Speech("Fuel is fine."));

            tap.Render(9_600, Rendered);
            sink.Finish(sink.Started[0]);

            tap.Render(9_600, Rendered);
            sink.Finish(sink.Started[1]);
        }

        var rows = log.Rows;

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.Text == "You are in Sol.");
        Assert.Contains(rows, row => row.Text == "Fuel is fine.");
    }

    /// <summary>
    /// What the pipeline reported is what the row carries. The tap has no way to know which
    /// provider produced the sound it is holding, and the phoneme string is the column the
    /// feature exists for.
    /// </summary>
    [Fact]
    public void The_row_carries_what_the_pipeline_reported()
    {
        var log = Log();
        var sink = new Sink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var tap = new Tap();

        using (var recorder = AudioFlightRecorder.Regardless(log, Now, NullLogger.Instance))
        {
            recorder.Watch(arbiter, tap);

            recorder.Noted(new SynthesisNote(
                "Observatory.",
                "Kokoro (on this machine)",
                "Heart (af_heart)",
                "ɒbzɜːveɪ",
                TimeSpan.FromMilliseconds(120)));

            arbiter.Enqueue(Speech("Observatory."));
            tap.Render(9_600, Rendered);
            sink.Finish(sink.Started[0]);
        }

        var row = Assert.Single(log.Rows);

        Assert.Equal("Kokoro (on this machine)", row.Provider);
        Assert.Equal("Heart (af_heart)", row.Voice);
        Assert.Equal("ɒbzɜːveɪ", row.Phonemes);
        Assert.Equal(TimeSpan.FromMilliseconds(120), row.Elapsed);
    }

    /// <summary>
    /// A cue is not d47 speaking. The arbiter puts loop-state chimes through the same queue, and
    /// a recorder that wrote a row for every tick would bury the utterances under them.
    /// </summary>
    [Fact]
    public void A_cue_is_not_written_down()
    {
        var log = Log();
        var sink = new Sink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var tap = new Tap();

        using (var recorder = AudioFlightRecorder.Regardless(log, Now, NullLogger.Instance))
        {
            recorder.Watch(arbiter, tap);

            arbiter.Enqueue(new AudioRequest
            {
                Channel = AudioChannel.Cue,
                Clip = new AudioClip("listening", new byte[16], AudioFormat.Standard),
            });

            tap.Render(9_600, Rendered);
            sink.Finish(sink.Started[0]);
        }

        Assert.Empty(log.Rows);
    }

    /// <summary>
    /// The exact buffer the transcriber was given, beside what it came back with. This is the
    /// point the ReadFully trap proved matters: a capture path inventing silence looks like a
    /// quiet Commander to every layer above it, and like a flat clip here.
    /// </summary>
    [Fact]
    public void What_the_transcriber_was_given_is_written_down_beside_what_it_said()
    {
        var log = Log();
        var samples = new float[16_000];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.5f;
        }

        using (var recorder = AudioFlightRecorder.Regardless(log, Now, NullLogger.Instance))
        {
            recorder.Heard(
                new Utterance(samples, 16_000),
                new Transcription("set course for Colonel")
                {
                    Model = "base.en",
                    Elapsed = TimeSpan.FromMilliseconds(340),
                });
        }

        var row = Assert.Single(log.Rows);

        Assert.Equal(FlightDirection.Heard, row.Direction);
        Assert.Equal("set course for Colonel", row.Text);
        Assert.Equal("base.en", row.Model);
        Assert.Equal(TimeSpan.FromMilliseconds(340), row.Elapsed);

        // Read back as audio rather than as a byte count: the whole point of retaining it is
        // that somebody can play it, and a header this writer got wrong would pass a length check.
        var clip = WavReader.Read(Path.Combine(_folder, row.Clip));

        Assert.Equal(16_000, clip.Format.SampleRate);
        Assert.Equal(TimeSpan.FromSeconds(1), clip.Duration);
    }
}
