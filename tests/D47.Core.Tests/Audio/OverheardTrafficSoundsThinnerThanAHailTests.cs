using D47.Core.Audio;
using D47.Core.Callouts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

public class OverheardTrafficSoundsThinnerThanAHailTests
{
    private static AudioClip Chord(params int[] hertz)
    {
        var format = AudioFormat.Standard;
        var samples = format.SampleRate;
        var pcm = new byte[samples * 2];

        for (var index = 0; index < samples; index++)
        {
            var sum = hertz.Sum(f => Math.Sin(2 * Math.PI * f * index / format.SampleRate));
            var value = (short)Math.Clamp(sum * 0.15 * short.MaxValue, short.MinValue, short.MaxValue);

            pcm[index * 2] = (byte)(value & 0xFF);
            pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new AudioClip("chord", pcm, format);
    }

    private static double Magnitude(AudioClip clip, int hertz)
    {
        var pcm = clip.Pcm.Span;
        var samples = pcm.Length / 2;
        var coefficient = 2 * Math.Cos(2 * Math.PI * hertz / clip.Format.SampleRate);
        double previous = 0, beforeThat = 0;

        for (var index = 0; index < samples; index++)
        {
            var sample = (short)(pcm[index * 2] | (pcm[(index * 2) + 1] << 8)) / 32768.0;
            var next = sample + (coefficient * previous) - beforeThat;
            beforeThat = previous;
            previous = next;
        }

        return Math.Sqrt((previous * previous) + (beforeThat * beforeThat) - (coefficient * previous * beforeThat));
    }

    [Fact]
    public void OverheardTrafficHasLessEnergyAboveTwoPointFourKilohertz()
    {
        var clip = Chord(1_000, 2_600, 3_000);

        var hail = RadioVoice.Colours(VoiceRole.Comms, 1, overheard: false)!(clip);
        var overheard = RadioVoice.Colours(VoiceRole.Comms, 1, overheard: true)!(clip);

        double Above(AudioClip c) => Magnitude(c, 2_600) + Magnitude(c, 3_000);

        Assert.True(Above(overheard) < Above(hail));
    }

    [Fact]
    public void ACrewMemberIsNeverTreatedAsOverheard() =>
        Assert.Null(RadioVoice.Colours(VoiceRole.Crew, 1, overheard: true));

    [Fact]
    public void AnOverheardLineIsOnTheOverheardChannelUnlessUrgent()
    {
        Assert.Equal(AudioChannel.Overheard, new Announcement("k", "t") { Overheard = true }.Channel);
        Assert.Equal(
            AudioChannel.Alert,
            new Announcement("k", "t", CalloutUrgency.Urgent) { Overheard = true }.Channel);
    }

    [Fact]
    public void AQueuedOverheardLinePlaysAfterSpeechEnqueuedBehindIt()
    {
        var sink = new RecordingAudioSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();

        AudioClip Clip(string name) => new(name, new byte[4_800], AudioFormat.Standard);

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Speech, Clip = Clip("playing") });
        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Overheard, Clip = Clip("chatter") });
        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Speech, Clip = Clip("answer") });

        sink.CompleteCurrent();
        sink.CompleteCurrent();
        sink.CompleteCurrent();

        Assert.Equal(["playing", "answer", "chatter"], sink.Started.Select(request => request.Name).Where(n => !n.Contains("gap", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void TheMixerHasAnOverheardRowThatDoesNotDuck()
    {
        Assert.Equal(0.5, AudioMix.Default.For(AudioChannel.Overheard).Level);
        Assert.False(AudioMix.Ducks(AudioChannel.Overheard));
    }
}
