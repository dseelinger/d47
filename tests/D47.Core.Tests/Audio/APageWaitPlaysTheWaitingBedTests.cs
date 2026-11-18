using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

public class APageWaitPlaysTheWaitingBedTests
{
    private static AudioClip Clip(string name) =>
        new(name, new byte[AudioFormat.Standard.SampleRate * 3 * 2], AudioFormat.Standard);

    private static (AudioArbiter Arbiter, RecordingAudioSink Sink) Build()
    {
        var sink = new RecordingAudioSink();
        return (new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start(), sink);
    }

    private static AudioRequest Bed() => new() { Channel = AudioChannel.Bed, Clip = Clip("hum"), Loop = true };

    private static IEnumerable<PlaybackRequest> LiveWaits(RecordingAudioSink sink) =>
        sink.Started.Where(request => request.Name == "pulse" && sink.Live.Contains(request.Id));

    [Fact]
    public void AWaitWithNoBedLoops()
    {
        var (arbiter, sink) = Build();

        using var wait = arbiter.OpenWait(Clip("pulse"));

        var playing = Assert.Single(LiveWaits(sink));
        Assert.True(playing.Loop);
    }

    [Fact]
    public void ABedStartedMidWaitStopsTheWaitingLoopAndStopBedRestartsIt()
    {
        var (arbiter, sink) = Build();
        using var wait = arbiter.OpenWait(Clip("pulse"));

        arbiter.Enqueue(Bed());

        Assert.Empty(LiveWaits(sink));

        arbiter.StopBed();

        Assert.Single(LiveWaits(sink));
    }

    [Fact]
    public void AWaitOpenedUnderABedWaitsForTheBedToStop()
    {
        var (arbiter, sink) = Build();
        arbiter.Enqueue(Bed());

        using var wait = arbiter.OpenWait(Clip("pulse"));

        Assert.Empty(LiveWaits(sink));

        arbiter.StopBed();

        Assert.Single(LiveWaits(sink));
    }

    [Fact]
    public void TwoOpensAndOneDisposeKeepItPlaying()
    {
        var (arbiter, sink) = Build();
        var first = arbiter.OpenWait(Clip("pulse"));
        var second = arbiter.OpenWait(Clip("pulse"));

        first.Dispose();
        first.Dispose();

        Assert.Single(LiveWaits(sink));

        second.Dispose();

        Assert.Empty(LiveWaits(sink));
    }

    [Fact]
    public void SilenceStopsItAndItDoesNotComeBackForWaitsAlreadyOpen()
    {
        var (arbiter, sink) = Build();
        var before = arbiter.OpenWait(Clip("pulse"));

        arbiter.Silence();

        Assert.Empty(LiveWaits(sink));

        arbiter.Enqueue(Bed());
        arbiter.StopBed();

        Assert.Empty(LiveWaits(sink));

        before.Dispose();

        using var after = arbiter.OpenWait(Clip("pulse"));

        Assert.Single(LiveWaits(sink));
    }

    [Fact]
    public void RelevelAppliesTheWaitMix()
    {
        var (arbiter, sink) = Build();
        using var wait = arbiter.OpenWait(Clip("pulse"));
        var id = Assert.Single(LiveWaits(sink)).Id;

        arbiter.Mix = AudioMix.Default with { Wait = new ChannelMix(0.4, false, 0.5) };

        Assert.Equal(0.4f, sink.GainOf(id)!.Value, 3);

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Speech, Clip = Clip("answer") });

        Assert.Equal(0.2f, sink.GainOf(id)!.Value, 3);
    }

    [Fact]
    public void AWaitIsNotQueued()
    {
        var (arbiter, _) = Build();

        Assert.Throws<ArgumentException>(() =>
            arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Wait, Clip = Clip("pulse"), Loop = true }));
    }
}
