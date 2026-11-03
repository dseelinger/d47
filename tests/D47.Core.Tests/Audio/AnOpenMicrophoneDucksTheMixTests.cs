using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

public class AnOpenMicrophoneDucksTheMixTests
{
    private static AudioClip Clip(string name) =>
        new(name, new byte[AudioFormat.Standard.SampleRate * 3 * 2], AudioFormat.Standard);

    private static (AudioArbiter Arbiter, RecordingAudioSink Sink, long BedId) BuildWithBed(ChannelMix bed)
    {
        var sink = new RecordingAudioSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        arbiter.Mix = AudioMix.Default with { Bed = bed };
        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Bed, Clip = Clip("hum"), Loop = true });
        return (arbiter, sink, sink.Started[0].Id);
    }

    [Fact]
    public void ACategoryDropsToItsDuckWhileTheMicrophoneIsOpenAndReturnsAfter()
    {
        var (arbiter, sink, bedId) = BuildWithBed(new ChannelMix(0.5, false, 0.2));
        Assert.Equal(0.5f, sink.GainOf(bedId));

        arbiter.Listening = true;
        Assert.Equal(0.1f, sink.GainOf(bedId)!.Value, 3);

        arbiter.Listening = false;
        Assert.Equal(0.5f, sink.GainOf(bedId));
    }

    [Fact]
    public void ACategoryWithDuckOneDoesNotChange()
    {
        var (arbiter, sink, bedId) = BuildWithBed(new ChannelMix(0.5, false, 1));
        var changes = sink.GainChanges.Count;

        arbiter.Listening = true;

        Assert.Equal(0.5f, sink.GainOf(bedId));
        Assert.All(sink.GainChanges.Skip(changes), change => Assert.Equal(0.5f, change.Gain));
    }

    [Fact]
    public void SpeechKeepsItsGainWhileTheMicrophoneIsOpen()
    {
        var sink = new RecordingAudioSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        arbiter.Mix = AudioMix.Default with { Speech = new ChannelMix(0.7, false, 0.1) };
        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Speech, Clip = Clip("answer") });
        var speechId = sink.Started[0].Id;

        arbiter.Listening = true;

        Assert.Equal(0.7f, sink.GainOf(speechId));
    }

    [Fact]
    public void TheDuckAppliesOnceWhenD47SpeaksWithTheMicrophoneOpen()
    {
        var (arbiter, sink, bedId) = BuildWithBed(new ChannelMix(0.5, false, 0.2));

        arbiter.Listening = true;
        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Speech, Clip = Clip("answer") });
        Assert.Equal(0.1f, sink.GainOf(bedId)!.Value, 3);

        arbiter.Listening = false;
        Assert.Equal(0.1f, sink.GainOf(bedId)!.Value, 3);

        sink.CompleteCurrent();
        Assert.Equal(0.5f, sink.GainOf(bedId));
    }
}
