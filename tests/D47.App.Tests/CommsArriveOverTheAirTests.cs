using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A sender who is not aboard arrives over a link, and is not captioned
/// (remediation.md, "An NPC message was spoken inside the cockpit" and "NPC speech does not need
/// captioning").
/// <para>
/// The radio half was reported against 0.23.0 and <b>is not reproduced here</b>: the announce
/// path passes <see cref="RadioVoice.Colours"/> for every role that is not the ship's AI or the
/// crew, and this asserts it end to end rather than by reading the code. What 0.23.0 could not
/// say is which way a given line went, so the spoken-voice log line now records whether it came
/// over the air or from the next seat.
/// </para>
/// </summary>
public class CommsArriveOverTheAirTests
{
    private static Announcement FromAnNpc() =>
        new("message.npc", "All I was doing was mining!")
        {
            Voice = VoiceRole.Comms,
            Speaker = "Limp",
            Transcript = "Limp: All I was doing was mining!\n",
        };

    private sealed record Spoken(AudioClip Clip, string? Caption);

    /// <summary>Announces one thing and reports what reached the speaker.</summary>
    private static async Task<Spoken> SayAsync(Announcement announcement)
    {
        var sink = new Sink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();

        string? caption = null;
        arbiter.ActivityChanged += activity => caption ??= activity.Caption;

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = new Tts(),
        };

        await voice.AnnounceAsync(announcement);

        return new Spoken(Assert.Single(sink.Started).Clip, caption);
    }

    private static async Task<AudioClip> RawAsync(string text) =>
        await new Tts().SynthesizeAsync(text, VoiceSelection.Default);

    [Fact]
    public async Task AnNpcMessageIsPutThroughTheLink()
    {
        var spoken = await SayAsync(FromAnNpc());
        var raw = await RawAsync("All I was doing was mining!");

        // Not the provider's own samples: something happened to them on the way out.
        Assert.NotEqual(raw.Pcm.ToArray(), spoken.Clip.Pcm.ToArray());
    }

    /// <summary>And the ship's AI is not, because it is in the room.</summary>
    [Fact]
    public async Task TheShipsAiIsNot()
    {
        var spoken = await SayAsync(new Announcement("fuel.low", "Fuel is low."));
        var raw = await RawAsync("Fuel is low.");

        Assert.Equal(raw.Pcm.ToArray(), spoken.Clip.Pcm.ToArray());
    }

    /// <summary>
    /// And it is not captioned. It is already on the comms page, and a station approach produces
    /// a steady stream of it.
    /// </summary>
    [Fact]
    public async Task AnNpcMessageIsNotCaptioned() => Assert.Null((await SayAsync(FromAnNpc())).Caption);

    /// <summary>And what D47 says is, because nothing else writes it into the headset.</summary>
    [Fact]
    public async Task WhatD47SaysIsCaptioned() =>
        Assert.Equal("Fuel is low.", (await SayAsync(new Announcement("fuel.low", "Fuel is low."))).Caption);

    /// <summary>A tone, so two clips can be told apart by their samples.</summary>
    private sealed class Tts : ITtsProvider
    {
        public string Id => "fake";

        public string Name => "Fake";

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Silent);

        public Task<AudioClip> SynthesizeAsync(
            string text, VoiceSelection voice, CancellationToken cancellationToken = default)
        {
            var samples = new byte[AudioFormat.Standard.SampleRate];

            for (var i = 0; i + 1 < samples.Length; i += 2)
            {
                var value = (short)(8000 * Math.Sin(
                    i / 2.0 * 2 * Math.PI * 300 / AudioFormat.Standard.SampleRate));

                samples[i] = (byte)(value & 0xFF);
                samples[i + 1] = (byte)((value >> 8) & 0xFF);
            }

            return Task.FromResult(new AudioClip(text, samples, AudioFormat.Standard));
        }
    }

    /// <summary>Keeps what it was handed, and finishes nothing.</summary>
    private sealed class Sink : IAudioSink
    {
        public List<PlaybackRequest> Started { get; } = [];

        public event Action<long>? Finished;

        public IRenderReferenceTap ReferenceTap { get; } = new NullTap();

        public void Play(PlaybackRequest request) => Started.Add(request);

        public void Stop(long playbackId)
        {
        }

        public void StopAll()
        {
        }

        public void SetGain(long playbackId, float gain)
        {
        }

        /// <summary>Never raised here; the assertions are about what was started.</summary>
        public void Ignore() => Finished?.Invoke(0);

        private sealed class NullTap : IRenderReferenceTap
        {
            public event Action<RenderReferenceFrame>? Rendered;

            public void Ignore() => Rendered?.Invoke(default);
        }
    }
}
