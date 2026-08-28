using D47.Core.Audio;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Music separate from cues and sound effects, situational from what the game reports
/// (Phase 12, "Ambient music").
/// </summary>
public class AmbienceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-ambience-tests",
        Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder that will not delete is not a test failure.
        }
    }

    private static GameStatus Status(StatusFlags flags = StatusFlags.None, StatusFlags2 two = StatusFlags2.None) =>
        new()
        {
            Flags = flags,
            Flags2 = (uint)two,
            ReadAt = DateTimeOffset.UnixEpoch,
        };

    /// <summary>
    /// Each of the five, from the flags Elite actually writes. On foot is asked first because
    /// the first bitfield keeps reporting InMainShip for a Commander who has got out.
    /// </summary>
    [Theory]
    [InlineData(StatusFlags.None, StatusFlags2.None, Situations.General)]
    [InlineData(StatusFlags.Docked, StatusFlags2.None, Situations.Docked)]
    [InlineData(StatusFlags.Supercruise, StatusFlags2.None, Situations.Supercruise)]
    [InlineData(StatusFlags.InMainShip, StatusFlags2.None, Situations.NormalSpace)]
    [InlineData(StatusFlags.InSrv, StatusFlags2.None, Situations.NormalSpace)]
    [InlineData(StatusFlags.InMainShip | StatusFlags.Landed, StatusFlags2.None, Situations.NormalSpace)]
    [InlineData(StatusFlags.InMainShip, StatusFlags2.OnFoot, Situations.OnFoot)]
    [InlineData(StatusFlags.Docked, StatusFlags2.OnFoot, Situations.OnFoot)]
    public void EachFlagCombinationMapsToTheNamedSituation(
        StatusFlags flags,
        StatusFlags2 two,
        string expected)
    {
        Assert.Equal(expected, Situations.For(Status(flags, two)));
    }

    /// <summary>
    /// Status.json not written yet is general rather than a guess. That is also the main menu,
    /// and the whole of a session before Elite has been started.
    /// </summary>
    [Fact]
    public void AGameThatHasSaidNothingIsGeneral()
    {
        Assert.Equal(Situations.General, Situations.For(GameStatus.Unknown));
    }

    /// <summary>The first situation entered is a change, or the ambience would never begin.</summary>
    [Fact]
    public void TheFirstSituationIsAlwaysAChange()
    {
        var ambience = new Ambience();

        Assert.True(ambience.Enter(Situations.General));
        Assert.False(ambience.Enter(Situations.General));
        Assert.True(ambience.Enter(Situations.Docked));
    }

    [Fact]
    public void AnEmptySituationFallsBackToGeneral()
    {
        var library = Library(("general", "hum"), ("general", "drift"));
        var ambience = new Ambience(new Random(1));

        ambience.Enter(Situations.Docked);

        Assert.NotNull(ambience.Next(library));
        Assert.Equal(Situations.General, ambience.Playing);

        // And the situation itself is still what it is. The fallback is where the tracks came
        // from, not a claim about where the Commander is.
        Assert.Equal(Situations.Docked, ambience.Situation);
    }

    /// <summary>
    /// General with no files is silence, not an error. This is every Commander who has not
    /// dropped any music in, which is every Commander on the day this ships.
    /// </summary>
    [Fact]
    public void NoMusicAtAllIsQuietRatherThanAFailure()
    {
        var ambience = new Ambience();
        ambience.Enter(Situations.Supercruise);

        Assert.Null(ambience.Next(Library()));
    }

    /// <summary>
    /// The whole folder is played before any track repeats, and the order is not the folder's.
    /// A Commander who drops in eight tracks did not number them.
    /// </summary>
    [Fact]
    public void TracksAreShuffledAndTheWholeFolderPlaysBeforeAnyRepeats()
    {
        var library = Library(
            ("general", "one"), ("general", "two"), ("general", "three"),
            ("general", "four"), ("general", "five"), ("general", "six"));

        var ambience = new Ambience(new Random(7));
        ambience.Enter(Situations.General);

        var round = Enumerable.Range(0, 6).Select(_ => ambience.Next(library)!.Name).ToList();

        Assert.Equal(6, round.Distinct(StringComparer.Ordinal).Count());
        Assert.NotEqual(["five", "four", "one", "six", "three", "two"], round);

        // And it keeps going rather than stopping at the end of the shuffle.
        Assert.NotNull(ambience.Next(library));
    }

    /// <summary>
    /// The bed and the music play at once without either taking the queue head. Two background
    /// layers sharing one slot would be one of them cutting the other off every turn.
    /// </summary>
    [Fact]
    public void TheBedAndTheMusicPlayAtOnce()
    {
        var (arbiter, sink) = Arbiter();

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Music, Clip = Clip("track") });
        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Bed, Clip = Clip("bed"), Loop = true });
        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Speech, Clip = Clip("reply") });

        Assert.Equal(3, sink.Live.Count);

        var activity = arbiter.Activity;

        Assert.True(activity.MusicPlaying);
        Assert.True(activity.BedPlaying);
        Assert.Equal(AudioChannel.Speech, activity.Channel);
    }

    [Fact]
    public void MusicDucksUnderSpeechAndComesBack()
    {
        var (arbiter, sink) = Arbiter();

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Music, Clip = Clip("track") });
        var music = sink.Started[0].Id;

        // Half level out of the box, so the undisturbed gain is the level itself.
        Assert.Equal(0.5f, sink.GainOf(music)!.Value, 3);

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Speech, Clip = Clip("reply") });
        Assert.Equal(0.1f, sink.GainOf(music)!.Value, 3);

        sink.CompleteCurrent();
        Assert.Equal(0.5f, sink.GainOf(music)!.Value, 3);
    }

    /// <summary>A Commander who asked for quiet asked for quiet, background layer or not.</summary>
    [Fact]
    public void SilenceStopsTheMusic()
    {
        var (arbiter, sink) = Arbiter();

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Music, Clip = Clip("track") });
        Assert.True(arbiter.Activity.MusicPlaying);

        arbiter.Silence();

        Assert.False(arbiter.Activity.MusicPlaying);
        Assert.Equal(1, sink.StopAllCount);
    }

    /// <summary>
    /// A track ending is how the next one is asked for. The arbiter reports it and nothing more
    /// — which track comes next is a question about situations, and the queue does not know what
    /// a station is.
    /// </summary>
    [Fact]
    public void ATrackEndingAsksForTheNextOne()
    {
        var (arbiter, sink) = Arbiter();

        var asked = 0;
        arbiter.MusicFinished += () => asked++;

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Music, Clip = Clip("track") });
        sink.CompletePlayback(sink.Started[0].Id);

        Assert.Equal(1, asked);
        Assert.False(arbiter.Activity.MusicPlaying);
    }

    /// <summary>And a track the arbiter stopped is not a track that finished.</summary>
    [Fact]
    public void StoppingTheMusicDoesNotAskForAnother()
    {
        var (arbiter, _) = Arbiter();

        var asked = 0;
        arbiter.MusicFinished += () => asked++;

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Music, Clip = Clip("track") });
        arbiter.StopMusic();

        Assert.Equal(0, asked);
        Assert.False(arbiter.Activity.MusicPlaying);
    }

    /// <summary>A folder named for a situation d47 does not know is reported, not guessed at.</summary>
    [Fact]
    public void AFolderNamedForNoSituationIsReported()
    {
        var library = Library(("in-combat", "battle"));

        Assert.Single(library.Skipped);
        Assert.Contains("in-combat", library.Skipped[0], StringComparison.Ordinal);
        Assert.Contains(Situations.Supercruise, library.Skipped[0], StringComparison.Ordinal);
    }

    private static AudioClip Clip(string name) =>
        new(name, new byte[AudioFormat.Standard.SampleRate / 5 * 2], AudioFormat.Standard);

    private static (AudioArbiter Arbiter, RecordingAudioSink Sink) Arbiter()
    {
        var sink = new RecordingAudioSink();
        return (new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start(), sink);
    }

    /// <summary>The shipped set plus a music folder built for this test.</summary>
    private CueLibrary Library(params (string Situation, string Track)[] tracks)
    {
        foreach (var (situation, track) in tracks)
        {
            WriteWav(Path.Combine(_root, FolderAudioSource.MusicFolder, situation, $"{track}.wav"));
        }

        Directory.CreateDirectory(Path.Combine(_root, FolderAudioSource.MusicFolder));

        return CueLibrary.Load(
            null,
            new EmbeddedCueSource(typeof(CueLibrary).Assembly),
            new FolderAudioSource(_root, NullLogger<FolderAudioSource>.Instance));
    }

    private static void WriteWav(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        const int Samples = 240;
        const int Data = Samples * 2;

        using var file = File.Create(path);
        using var write = new BinaryWriter(file);

        write.Write("RIFF"u8.ToArray());
        write.Write(36 + Data);
        write.Write("WAVE"u8.ToArray());
        write.Write("fmt "u8.ToArray());
        write.Write(16);
        write.Write((short)1);
        write.Write((short)1);
        write.Write(AudioFormat.Standard.SampleRate);
        write.Write(AudioFormat.Standard.SampleRate * 2);
        write.Write((short)2);
        write.Write((short)16);
        write.Write("data"u8.ToArray());
        write.Write(Data);
        write.Write(new byte[Data]);
    }
}
