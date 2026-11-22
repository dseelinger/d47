using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// A library track counts as played once it ended or was skipped, never while it is shelved, held or restarted, so the
/// music library can delete it (#2650).
/// </summary>
public sealed class ALibraryTrackIsPlayedOnceItIsDoneTests : IDisposable
{
    private const string Pack = @"C:\data\music-pack";
    private const string Own = @"C:\data\audio";

    private static readonly GameStatus Docked = Status(StatusFlags.Docked);

    private static readonly GameStatus InSupercruise = Status(StatusFlags.Supercruise | StatusFlags.InMainShip);

    private readonly MemoryFileSystem _files = new();
    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;
    private readonly AmbientMusic _music;
    private readonly List<MusicTrack> _played = [];
    private CueLibrary _library;

    public ALibraryTrackIsPlayedOnceItIsDoneTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();

        AddTrack(Pack, Situations.Docked, "berth.mp3");
        AddTrack(Pack, Situations.Docked, "hangar.mp3");
        AddTrack(Pack, Situations.Supercruise, "cruise.mp3");
        AddTrack(Own, $@"music\{Situations.Docked}", "mine.wav");
        _library = Load();

        _music = new AmbientMusic(_audio, () => _library, new Random(7));
        _music.Played += _played.Add;
        _audio.MusicFinished += _music.TrackFinished;
        _music.GameRunning(running: true);
    }

    public void Dispose() => _audio.Dispose();

    [Fact]
    public void ALibraryTrackThatEndsIsPlayed()
    {
        var first = StartLibraryTrack();

        _sink.CompletePlayback(first.Id);

        Assert.Equal(first.Name, Assert.Single(_played).Name);
        Assert.NotNull(_played[0].PackFile);
    }

    [Fact]
    public void ASkippedLibraryTrackIsPlayed()
    {
        var first = StartLibraryTrack();

        _music.Control(MusicAction.Next);

        Assert.Equal(first.Name, Assert.Single(_played).Name);
    }

    [Fact]
    public void APausedLibraryTrackIsNotPlayed()
    {
        StartLibraryTrack();

        _music.Control(MusicAction.Pause);

        Assert.Empty(_played);
    }

    [Fact]
    public void AShelvedLibraryTrackIsNotPlayed()
    {
        StartLibraryTrack();

        _music.Follow(InSupercruise, musicTrack: null);

        Assert.Empty(_played);
    }

    [Fact]
    public void AMutedLibraryTrackIsNotPlayed()
    {
        StartLibraryTrack();

        _music.MuteChanged(muted: true);

        Assert.Empty(_played);
    }

    [Fact]
    public void PreviousOnALibraryTrackRestartsItAndItIsNotPlayed()
    {
        var first = StartLibraryTrack();

        var said = _music.Control(MusicAction.Previous);

        Assert.Equal("Restarted this track.", said);
        Assert.Contains(first.Id, _sink.Stopped);
        Assert.Equal(first.Name, _sink.Started[^1].Name);
        Assert.Empty(_played);
    }

    [Fact]
    public void AnOwnTrackIsNeverReportedAsPlayed()
    {
        _music.Follow(Docked, musicTrack: null);

        for (var i = 0; i < 6; i++)
        {
            _music.Control(MusicAction.Next);
        }

        Assert.All(_played, track => Assert.NotNull(track.PackFolder));
        Assert.DoesNotContain(_played, track => track.Name == "mine");
    }

    [Fact]
    public void TheTrackThatJustEndedIsNotDrawnFirstFromARebuiltLibrary()
    {
        for (var round = 0; round < 8; round++)
        {
            var playing = _sink.Started.Count == 0 ? StartLibraryTrack() : _sink.Started[^1];
            _library = Load();

            _sink.CompletePlayback(playing.Id);
            _sink.CompleteCurrent();

            Assert.NotEqual(playing.Name, _sink.Started[^1].Name);
        }
    }

    private PlaybackRequest StartLibraryTrack()
    {
        _music.Follow(Docked, musicTrack: null);

        for (var guard = 0; guard < 10 && _music.State.Track == "mine"; guard++)
        {
            _music.Control(MusicAction.Next);
        }

        _played.Clear();
        Assert.NotEqual("mine", _music.State.Track);
        return _sink.Started[^1];
    }

    private CueLibrary Load() =>
        CueLibrary.Load(
            null,
            new EmbeddedCueSource(typeof(CueLibrary).Assembly),
            new FolderAudioSource(Pack, _files, NullLogger<FolderAudioSource>.Instance, new StubDecoder(), musicPack: true),
            new FolderAudioSource(Own, _files, NullLogger<FolderAudioSource>.Instance));

    private void AddTrack(string root, string folder, string file) =>
        _files.WriteBytes(Path.Combine(root, folder, file), WavWriter.ToBytes(new byte[480], AudioFormat.Standard));

    private static GameStatus Status(StatusFlags flags) => new() { Flags = flags, ReadAt = DateTimeOffset.UnixEpoch };

    private sealed class StubDecoder : IAudioDecoder
    {
        public IReadOnlySet<string> Extensions { get; } = new HashSet<string> { ".mp3" };

        public AudioClip Decode(string path, string clipName) => throw new NotSupportedException();

        public IPcmStream Open(string path) =>
            WavReader.OpenMusic(
                new MemoryStream(WavWriter.ToBytes(new byte[480], AudioFormat.Standard)), "stub");
    }
}
