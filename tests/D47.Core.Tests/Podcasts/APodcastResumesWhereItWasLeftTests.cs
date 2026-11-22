using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Podcasts;
using D47.Core.Storage;
using D47.Core.Tests.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Podcasts;

/// <summary>The podcast player resumes, finishes, saves and steps ambient music aside (#2657).</summary>
public sealed class APodcastResumesWhereItWasLeftTests
{
    private const string ListeningPath = @"C:\d47\data\podcasts\listening.json";

    private static readonly Uri Feed = new("https://example.test/feed.xml");

    private static readonly DateTimeOffset Start = new(2026, 11, 22, 20, 0, 0, TimeSpan.Zero);

    private static readonly Lazy<CueLibrary> NoMusic = new(() => CueLibrary.Load(null, new EmbeddedCueSource(typeof(CueLibrary).Assembly)));

    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;
    private readonly MemoryFileSystem _files = new();
    private readonly ManualTime _time = new();
    private readonly List<Action> _queued = [];

    /// <summary>What the next episode's stream does when it is opened.</summary>
    private Func<IPcmStream> _stream = () => new MemoryPcm(canSeek: true);

    public APodcastResumesWhereItWasLeftTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
    }

    private static PodcastEpisode Episode(string id, int month) =>
        new(id, $"Episode {id}", new DateTimeOffset(2026, month, 1, 0, 0, 0, TimeSpan.Zero), new Uri($"https://cdn.example.test/{id}.mp3"), "audio/mpeg", null, TimeSpan.FromHours(1));

    private static readonly PodcastEpisode First = Episode("e1", 1);

    private static readonly PodcastEpisode Second = Episode("e2", 2);

    private static readonly PodcastEpisode Third = Episode("e3", 3);

    private static readonly PodcastEpisode Fourth = Episode("e4", 4);

    /// <summary>Newest first, as the feed parser gives them.</summary>
    private static readonly PodcastShow Show = new("Deep Space Hour", [Fourth, Third, Second, First]);

    private PodcastPlayer Player(AmbientMusic? music = null) =>
        new(
            _audio,
            music ?? new AmbientMusic(_audio, () => NoMusic.Value),
            episode => new MusicTrack(episode.Id, () => _stream()),
            _files,
            ListeningPath,
            NullLogger<PodcastPlayer>.Instance,
            _queued.Add,
            _time);

    private void Saved(PodcastEpisode episode, TimeSpan position, bool finished = false) =>
        _files.WriteText(ListeningPath, new PodcastListening().With(Feed, episode.Id, position, finished).ToJson());

    private void RunQueued()
    {
        var work = _queued.ToList();
        _queued.Clear();
        work.ForEach(action => action());
    }

    private PodcastProgress? OnDisk(PodcastEpisode episode) =>
        PodcastListening.FromJson(_files.ReadText(ListeningPath)!).Listening.Find(Feed, episode.Id);

    private PlaybackRequest Live => _sink.Started.Last(request => request.Track is not null);

    [Fact]
    public async Task ASavedPositionResumes()
    {
        Saved(First, TimeSpan.FromMinutes(12));
        var player = Player();

        var said = player.Play(Feed, Show, First);
        using var opened = Live.Track!.Open();

        Assert.Equal((Live.Id, TimeSpan.FromMinutes(12)), _sink.Seeks.Last());
        Assert.Equal("Playing Episode e1 from 12:00.", await said);
    }

    [Fact]
    public async Task AnEpisodeWithNoSavedPositionStartsAtTheBeginning()
    {
        var player = Player();

        var said = player.Play(Feed, Show, First);
        using var opened = Live.Track!.Open();

        Assert.Empty(_sink.Seeks);
        Assert.Equal("Playing Episode e1.", await said);
    }

    [Fact]
    public void TheEndMarksTheEpisodeFinishedAndStops()
    {
        Saved(First, TimeSpan.FromMinutes(12));
        var player = Player();
        _ = player.Play(Feed, Show, First);
        var id = Live.Id;

        using (var stream = Live.Track!.Open())
        {
            var buffer = new byte[4096];

            while (stream.Read(buffer) > 0)
            {
            }
        }

        _sink.CompletePlayback(id);
        RunQueued();

        Assert.Null(player.State);
        Assert.Null(player.Problem);
        Assert.Equal(new PodcastProgress(Feed.AbsoluteUri, First.Id, TimeSpan.Zero, Finished: true), OnDisk(First));
    }

    [Fact]
    public async Task AnEpisodeThatFailsKeepsItsPositionAndIsNotFinished()
    {
        Saved(First, TimeSpan.FromMinutes(5));
        _stream = () => throw new IOException("The episode stopped arriving.");
        var player = Player();
        var said = player.Play(Feed, Show, First);
        var id = Live.Id;

        Assert.Throws<IOException>(() => Live.Track!.Open());
        _sink.CompletePlayback(id);
        RunQueued();

        Assert.Null(player.State);
        Assert.Equal("The episode stopped arriving.", player.Problem);
        Assert.Equal(new PodcastProgress(Feed.AbsoluteUri, First.Id, TimeSpan.FromMinutes(5), Finished: false), OnDisk(First));
        Assert.Equal("Episode e1 could not be played. The episode stopped arriving.", await said);
    }

    [Fact]
    public async Task AStreamThatCannotSeekPlaysFromTheStartAndSaysSo()
    {
        Saved(First, TimeSpan.FromMinutes(12));
        _stream = () => new MemoryPcm(canSeek: false);
        var player = Player();

        var said = player.Play(Feed, Show, First);
        using var opened = Live.Track!.Open();

        Assert.Equal($"Playing Episode e1 from the beginning. {PodcastPlayer.FromStartOnlySentence}", await said);
        Assert.True(player.State!.FromStartOnly);
        Assert.Equal(PodcastPlayer.FromStartOnlySentence, player.SkipForward());
        Assert.Equal(PodcastPlayer.FromStartOnlySentence, player.SkipBack());
    }

    [Fact]
    public void SkipsMoveThirtySecondsAndStopAtTheEnds()
    {
        var player = Player();
        _ = player.Play(Feed, Show, First);
        var id = Live.Id;

        _sink.Progresses[id] = new TrackProgress(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1));
        player.SkipBack();
        Assert.Equal((id, TimeSpan.Zero), _sink.Seeks.Last());

        _sink.Progresses[id] = new TrackProgress(TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(1));
        player.SkipForward();
        Assert.Equal((id, TimeSpan.FromSeconds(50)), _sink.Seeks.Last());

        _sink.Progresses[id] = new TrackProgress(TimeSpan.FromSeconds(45), TimeSpan.FromMinutes(1));
        player.SkipForward();
        Assert.Equal((id, TimeSpan.FromMinutes(1)), _sink.Seeks.Last());
    }

    [Fact]
    public void NextSkipsFinishedEpisodesAndTakesTheEarliestLaterOne()
    {
        Saved(Second, TimeSpan.Zero, finished: true);
        var player = Player();
        _ = player.Play(Feed, Show, First);

        _ = player.Next();

        Assert.Equal(Third.Id, Live.Name);
        Assert.Equal(Third, player.State!.Episode);
    }

    [Fact]
    public async Task NextWithNoLaterUnfinishedEpisodeSaysSoAndStops()
    {
        Saved(Fourth, TimeSpan.Zero, finished: true);
        var player = Player();
        _ = player.Play(Feed, Show, Third);
        var id = Live.Id;

        var said = await player.Next();

        Assert.Equal("There is no later unfinished episode of Deep Space Hour.", said);
        Assert.Contains(id, _sink.Stopped);
        Assert.Null(player.State);
    }

    [Fact]
    public void PausingStopAndPlayingAnotherEpisodeSaveThePosition()
    {
        var player = Player();
        _ = player.Play(Feed, Show, First);
        _sink.Progresses[Live.Id] = new TrackProgress(TimeSpan.FromMinutes(3), TimeSpan.FromHours(1));

        player.Pause();
        RunQueued();
        Assert.Equal(TimeSpan.FromMinutes(3), OnDisk(First)!.Position);
        Assert.True(player.State!.PausedByCommander);

        player.Resume();
        _sink.Progresses[Live.Id] = new TrackProgress(TimeSpan.FromMinutes(7), TimeSpan.FromHours(1));
        _ = player.Play(Feed, Show, Second);
        RunQueued();
        Assert.Equal(TimeSpan.FromMinutes(7), OnDisk(First)!.Position);

        _sink.Progresses[Live.Id] = new TrackProgress(TimeSpan.FromMinutes(2), TimeSpan.FromHours(1));
        player.Stop();
        RunQueued();
        Assert.Equal(TimeSpan.FromMinutes(2), OnDisk(Second)!.Position);
    }

    [Fact]
    public async Task StoppingBeforeTheEpisodeOpensSaysSoRatherThanWaiting()
    {
        var player = Player();
        var said = player.Play(Feed, Show, First);

        player.Stop();

        Assert.Equal("Episode e1 was stopped before it started.", await said);
    }

    [Fact]
    public void AFinishCarryingAnotherPlaybacksIdIsIgnored()
    {
        var player = Player();
        _ = player.Play(Feed, Show, First);
        var first = Live.Id;
        _ = player.Play(Feed, Show, Second);

        _sink.CompletePlayback(first);
        RunQueued();

        Assert.Equal(Second, player.State!.Episode);
    }

    [Fact]
    public void ExitingWithNothingPlayedWritesNoFile()
    {
        var player = Player();

        player.SaveNow();

        Assert.Null(_files.ReadText(ListeningPath));
    }

    [Fact]
    public void TheTickQueuesAtMostOneSaveEveryThirtySecondsAndNoneWhilePaused()
    {
        var player = Player();
        _ = player.Play(Feed, Show, First);
        _sink.Progresses[Live.Id] = new TrackProgress(TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));
        _queued.Clear();

        for (var second = 0; second <= 60; second++)
        {
            player.Tick(Start.AddSeconds(second));
        }

        Assert.Equal(2, _queued.Count);
        Assert.Null(_files.ReadText(ListeningPath));
        RunQueued();
        Assert.Equal(TimeSpan.FromMinutes(1), OnDisk(First)!.Position);

        player.Pause();
        _queued.Clear();

        for (var second = 61; second <= 180; second++)
        {
            player.Tick(Start.AddSeconds(second));
        }

        Assert.Empty(_queued);
    }

    [Fact]
    public void APauseFromSilenceIsSavedByTheTick()
    {
        var player = Player();
        _ = player.Play(Feed, Show, First);
        _sink.Progresses[Live.Id] = new TrackProgress(TimeSpan.FromMinutes(4), TimeSpan.FromHours(1));
        player.Tick(Start);
        _queued.Clear();

        _audio.Silence();
        player.Tick(Start.AddSeconds(1));
        RunQueued();

        Assert.Equal(TimeSpan.FromMinutes(4), OnDisk(First)!.Position);
        player.Tick(Start.AddSeconds(2));
        Assert.Empty(_queued);
    }

    [Fact]
    public void AmbientMusicYieldsWhileAPodcastPlaysAndComesBackOnStop()
    {
        var music = Music((Situations.Docked, "berth"));
        music.Follow(Docked, musicTrack: null, Start);
        var berth = Assert.Single(_sink.Started);
        var player = Player(music);

        _ = player.Play(Feed, Show, First);

        Assert.Contains(berth.Id, _sink.Held);
        Assert.False(music.State.Playing);

        player.Pause();
        music.Follow(Docked, musicTrack: null, Start.AddSeconds(10));
        Assert.Contains(berth.Id, _sink.Held);
        Assert.Equal(2, _sink.Started.Count);

        player.Stop();

        Assert.DoesNotContain(berth.Id, _sink.Held);
        Assert.Equal((berth.Id, 1f, AmbientMusic.WakeFadeIn, false), _sink.Fades.Last());
        Assert.True(music.State.Playing);
    }

    [Fact]
    public void AmbientMusicComesBackWhenTheEpisodeEnds()
    {
        var music = Music((Situations.Docked, "berth"));
        music.Follow(Docked, musicTrack: null, Start);
        var berth = Assert.Single(_sink.Started);
        var player = Player(music);
        _ = player.Play(Feed, Show, First);

        _sink.CompletePlayback(Live.Id);
        RunQueued();

        Assert.DoesNotContain(berth.Id, _sink.Held);
        Assert.True(music.State.Playing);
    }

    [Fact]
    public void WhileYieldingNothingStartsTheMusic()
    {
        var music = Music((Situations.Docked, "berth"), (Situations.Supercruise, "cruise"));
        var player = Player(music);
        _ = player.Play(Feed, Show, First);

        music.Follow(Docked, musicTrack: null, Start);
        music.Follow(Status(StatusFlags.Supercruise | StatusFlags.InMainShip), musicTrack: null, Start.AddSeconds(5));

        Assert.DoesNotContain(_sink.Started, request => request.Track is { } track && track.Name != First.Id);
        Assert.Equal("Ambient music waits while a podcast plays.", music.Control(MusicAction.Next));

        player.Stop();

        Assert.Contains(_sink.Started, request => request.Track is { } track && track.Name != First.Id);
    }

    private static readonly GameStatus Docked = Status(StatusFlags.Docked);

    private static GameStatus Status(StatusFlags flags) =>
        new() { Flags = flags, ReadAt = DateTimeOffset.UnixEpoch };

    private AmbientMusic Music(params (string Folder, string Track)[] tracks)
    {
        const string root = @"C:\d47-music";
        var files = new MemoryFileSystem();

        foreach (var (folder, track) in tracks)
        {
            files.WriteBytes(
                Path.Combine(root, FolderAudioSource.MusicFolder, folder, $"{track}.wav"),
                WavWriter.ToBytes(new byte[480], AudioFormat.Standard));
        }

        var library = CueLibrary.Load(
            null,
            new EmbeddedCueSource(typeof(CueLibrary).Assembly),
            new FolderAudioSource(root, files, NullLogger<FolderAudioSource>.Instance));

        var music = new AmbientMusic(_audio, () => library, new Random(7));
        _audio.MusicFinished += music.TrackFinished;
        music.GameRunning(running: true);
        return music;
    }
}

/// <summary>A second of silence as 48 kHz stereo PCM, read from memory.</summary>
internal sealed class MemoryPcm(bool canSeek) : IPcmStream
{
    private readonly byte[] _pcm = new byte[AudioFormat.Music.SampleRate * AudioFormat.Music.BytesPerFrame];
    private int _at;

    public TimeSpan? Length => TimeSpan.FromSeconds(1);

    public bool CanSeek => canSeek;

    public int Read(Span<byte> buffer)
    {
        var count = Math.Min(buffer.Length, _pcm.Length - _at);
        _pcm.AsSpan(_at, count).CopyTo(buffer);
        _at += count;
        return count;
    }

    public void Seek(TimeSpan to)
    {
        if (canSeek)
        {
            _at = (int)Math.Min(_pcm.Length, to.TotalSeconds * AudioFormat.Music.SampleRate) * AudioFormat.Music.BytesPerFrame;
        }
    }

    public void Dispose()
    {
    }
}
