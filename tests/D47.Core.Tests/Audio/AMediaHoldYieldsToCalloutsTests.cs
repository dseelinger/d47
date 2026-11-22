using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A book's hold on the speech channels gives way to every callout and keeps chatter out (#2688).</summary>
public sealed class AMediaHoldYieldsToCalloutsTests
{
    private const string Book = "book-playback";

    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;
    private int _paused;

    public AMediaHoldYieldsToCalloutsTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
        _audio.PlaybackPaused += () => _paused++;
    }

    private static AudioRequest On(AudioChannel channel, string name, string? group = null) =>
        new()
        {
            Channel = channel,
            Clip = new AudioClip(name, new byte[AudioFormat.Standard.SampleRate / 5 * 2], AudioFormat.Standard),
            Group = group,
        };

    private List<string> Names() =>
        [.. _sink.Started.Select(request => request.Name).Where(name => name != "spoken-gap")];

    [Theory]
    [InlineData(AudioChannel.Speech, "callout-1")]
    [InlineData(AudioChannel.Speech, null)]
    [InlineData(AudioChannel.Alert, "alert-1")]
    public void ACalloutPausesTheHoldAndPlays(AudioChannel channel, string? group)
    {
        Assert.True(_audio.HoldSpeechForMedia(Book));
        _audio.Enqueue(On(AudioChannel.Speech, "sentence", Book));
        var sentence = _sink.Started.Single().Id;

        _audio.Enqueue(On(channel, "callout", group));

        Assert.Equal(1, _paused);
        Assert.Contains(sentence, _sink.Stopped);
        Assert.Equal(["sentence", "callout"], Names());

        _audio.Enqueue(On(AudioChannel.Speech, "next sentence", Book));
        _sink.PlayOutAll();

        Assert.Equal(["sentence", "callout"], Names());
    }

    [Fact]
    public void ItsOwnSentencesDoNotPauseIt()
    {
        _audio.HoldSpeechForMedia(Book);
        _audio.Enqueue(On(AudioChannel.Speech, "first", Book));
        _sink.PlayOutAll();
        _audio.Enqueue(On(AudioChannel.Speech, "second", Book));
        _sink.PlayOutAll();

        Assert.Equal(0, _paused);
        Assert.Equal(["first", "second"], Names());
    }

    [Fact]
    public void ACalloutAlreadyQueuedOrPlayingIsKeptAndTheHoldStartsPaused()
    {
        _audio.Enqueue(On(AudioChannel.Speech, "callout", "callout-1"));
        _audio.Enqueue(On(AudioChannel.Speech, "second callout", "callout-2"));

        Assert.False(_audio.HoldSpeechForMedia(Book));
        _audio.Enqueue(On(AudioChannel.Speech, "sentence", Book));
        _sink.PlayOutAll();

        Assert.Empty(_sink.Stopped);
        Assert.Equal(["callout", "second callout"], Names());

        Assert.True(_audio.HoldSpeechForMedia(Book));
    }

    [Fact]
    public void ChatterIsDroppedWhenTheHoldStartsAndRefusedWhileItHolds()
    {
        _audio.Enqueue(On(AudioChannel.Overheard, "chatter", "comms"));
        var chatter = _sink.Started.Single().Id;

        Assert.True(_audio.HoldSpeechForMedia(Book));
        _audio.Enqueue(On(AudioChannel.Overheard, "more chatter", "comms"));
        _sink.PlayOutAll();

        Assert.Contains(chatter, _sink.Stopped);
        Assert.Equal(["chatter"], Names());
        Assert.Equal(0, _paused);
    }

    [Fact]
    public void ChatterIsRefusedWhileMediaPlaysEvenWithTheHoldPausedAndAcceptedOnceItIsNot()
    {
        _audio.HoldSpeechForMedia(Book);
        _audio.MediaPlays(true);
        _audio.Enqueue(On(AudioChannel.Speech, "callout", "callout-1"));
        _sink.PlayOutAll();

        _audio.Enqueue(On(AudioChannel.Overheard, "chatter", "comms"));
        _sink.PlayOutAll();
        Assert.Equal(["callout"], Names());

        _audio.MediaPlays(false);
        _audio.Enqueue(On(AudioChannel.Overheard, "chatter", "comms"));
        _sink.PlayOutAll();
        Assert.Equal(["callout", "chatter"], Names());
    }

    [Fact]
    public void ChatterIsNotRefusedUnderAmbientMusic()
    {
        _audio.PlayMusic(new MusicTrack("berth", () => throw new NotSupportedException()));

        _audio.Enqueue(On(AudioChannel.Overheard, "chatter", "comms"));

        Assert.Equal(["berth", "chatter"], Names());
    }

    [Fact]
    public void ItDoesNotHoldAPodcast()
    {
        _audio.PlayPodcast(new MusicTrack("episode", () => throw new NotSupportedException()), TimeSpan.Zero);
        var podcast = _sink.Started.Single().Id;

        _audio.HoldSpeechForMedia(Book);

        Assert.DoesNotContain(podcast, _sink.Paused);
    }

    [Fact]
    public void AStoryHoldStillRefusesOtherSpeech()
    {
        _audio.HoldSpeechFor("story-run");
        _audio.Enqueue(On(AudioChannel.Speech, "callout", "callout-1"));

        Assert.Empty(Names());
        Assert.Equal(0, _paused);
    }
}
