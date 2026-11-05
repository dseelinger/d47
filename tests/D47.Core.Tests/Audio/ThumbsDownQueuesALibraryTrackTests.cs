using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// "Thumbs down this track" queues the playing library track for a maintainer (#2033) and recycles the playing
/// own track for everyone (#2090).
/// </summary>
public sealed class ThumbsDownQueuesALibraryTrackTests : IDisposable
{
    private const string Pack = @"C:\data\music-pack";
    private const string Own = @"C:\data\audio";
    private const string Rejects = @"C:\data\music-rejects.tsv";

    private readonly MemoryFileSystem _files = new();
    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;
    private readonly MusicRejects _rejects;
    private readonly AmbientMusic _music;
    private readonly List<string> _recycled = [];
    private bool _token = true;
    private bool _deleteWorks = true;

    public ThumbsDownQueuesALibraryTrackTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
        _rejects = new MusicRejects(Rejects, _files);

        AddTrack(Pack, Situations.Docked, "berth.mp3");
        AddTrack(Pack, Situations.Docked, "hangar.mp3");
        AddTrack(Own, $@"music\{Situations.Supercruise}", "mine.wav");

        var library = CueLibrary.Load(
            null,
            new EmbeddedCueSource(typeof(CueLibrary).Assembly),
            new FolderAudioSource(Pack, _files, NullLogger<FolderAudioSource>.Instance, new StubDecoder(), musicPack: true),
            new FolderAudioSource(Own, _files, NullLogger<FolderAudioSource>.Instance));

        _music = new AmbientMusic(_audio, () => library, new Random(7), _rejects, path =>
        {
            _recycled.Add(path);
            return _deleteWorks;
        });
        _audio.MusicFinished += _music.TrackFinished;
        _music.GameRunning(running: true);
    }

    public void Dispose() => _audio.Dispose();

    [Fact]
    public async Task WithATokenThePhraseQueuesThePlayingPackTrackAndSkipsToTheNext()
    {
        _music.Follow(Status(StatusFlags.Docked), musicTrack: null);
        var first = Assert.Single(_sink.Started);

        var reply = await Say("thumbs down this track");

        Assert.Contains(first.Id, _sink.Stopped);
        Assert.Equal(2, _sink.Started.Count);
        Assert.NotEqual(first.Name, _sink.Started[1].Name);
        Assert.Contains("1 track is queued", reply, StringComparison.Ordinal);

        var row = Assert.Single(Rows());
        Assert.StartsWith(Situations.Docked + "\t", row, StringComparison.Ordinal);
        Assert.Contains(first.Name, row, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASecondThumbsDownOnTheSameTrackAddsNoRow()
    {
        _music.Follow(Status(StatusFlags.Docked), musicTrack: null);
        var playing = Assert.Single(_sink.Started).Name;
        Assert.True(_rejects.Add(Situations.Docked, playing + ".mp3"));

        var reply = await Say("thumbs down");

        Assert.StartsWith("Already queued", reply, StringComparison.Ordinal);
        Assert.Single(Rows());
    }

    [Fact]
    public async Task ATrackFromTheCommandersOwnFolderIsRecycledAndSkippedWithAToken()
    {
        _music.Follow(Status(StatusFlags.Supercruise | StatusFlags.InMainShip), musicTrack: null);
        var playing = Assert.Single(_sink.Started);

        var reply = await Say("thumbs down this track");

        Assert.Contains("Deleted mine", reply, StringComparison.Ordinal);
        Assert.Equal(Path.Combine(Own, "music", Situations.Supercruise, "mine.wav"), Assert.Single(_recycled));
        Assert.Empty(Rows());
        Assert.Contains(playing.Id, _sink.Stopped);
    }

    [Fact]
    public async Task ATrackFromTheCommandersOwnFolderIsRecycledAndSkippedWithoutAToken()
    {
        _token = false;
        _music.Follow(Status(StatusFlags.Supercruise | StatusFlags.InMainShip), musicTrack: null);
        var playing = Assert.Single(_sink.Started);

        var reply = await Say("thumbs down");

        Assert.Contains("Deleted mine", reply, StringComparison.Ordinal);
        Assert.Single(_recycled);
        Assert.Empty(Rows());
        Assert.Contains(playing.Id, _sink.Stopped);
    }

    [Fact]
    public async Task ADeletedOwnTrackIsNotDrawnAgain()
    {
        _music.Follow(Status(StatusFlags.Supercruise | StatusFlags.InMainShip), musicTrack: null);

        await Say("thumbs down this track");

        Assert.Single(_sink.Started);
        Assert.Single(_recycled);
    }

    [Fact]
    public async Task AnOwnTrackThatCannotBeDeletedIsSaidSoAndStillSkipped()
    {
        _deleteWorks = false;
        _music.Follow(Status(StatusFlags.Supercruise | StatusFlags.InMainShip), musicTrack: null);
        var playing = Assert.Single(_sink.Started);

        var reply = await Say("thumbs down this track");

        Assert.Contains("Could not delete mine", reply, StringComparison.Ordinal);
        Assert.Contains(playing.Id, _sink.Stopped);
    }

    [Fact]
    public async Task WithoutATokenALibraryTrackIsRefusedAndKeepsPlaying()
    {
        _token = false;
        _music.Follow(Status(StatusFlags.Docked), musicTrack: null);
        var playing = Assert.Single(_sink.Started);

        var reply = await Say("thumbs down this track");

        Assert.Contains("for the library's maintainer", reply, StringComparison.Ordinal);
        Assert.Empty(Rows());
        Assert.DoesNotContain(playing.Id, _sink.Stopped);
        Assert.Empty(_recycled);
    }

    [Fact]
    public async Task WithNoMusicPlayingTheReplySaysSo()
    {
        var reply = await Say("thumbs down this track");

        Assert.Equal("No music is playing.", reply);
        Assert.Empty(Rows());
    }

    [Fact]
    public void WithoutATokenThePhraseIsMatched()
    {
        _token = false;

        Assert.NotNull(Router().MatchToolCommand("thumbs down this track"));
        Assert.NotNull(Router().MatchToolCommand("thumbs down"));
    }

    [Fact]
    public void TheHotkeyRowsShowWithoutAToken()
    {
        _token = false;

        var rows = Capability().Settings.Where(row => row.Key is AudioCapability.ThumbsDownHotkeyKey or AudioCapability.ThumbsDownButtonKey);

        Assert.All(rows, row => Assert.Null(row.AppliesWhen));
    }

    [Fact]
    public async Task TheModelCannotCallIt()
    {
        var registry = CapabilityRegistry.Build([Capability()]);

        var result = await registry.InvokeAsync(
            AudioCapability.ThumbsDownTool, new ToolArguments(new Dictionary<string, string>()), TestContext.Current.CancellationToken, ToolCaller.Model);

        Assert.True(result.IsError);
        Assert.Empty(Rows());
        Assert.True(Capability().Tools.Single(t => t.Name == AudioCapability.ThumbsDownTool).Protected);
    }

    [Fact]
    public void AmbienceDoesNotChooseAQueuedTrack()
    {
        var library = CueLibrary.Load(
            null,
            new EmbeddedCueSource(typeof(CueLibrary).Assembly),
            new FolderAudioSource(Pack, _files, NullLogger<FolderAudioSource>.Instance, new StubDecoder(), musicPack: true));
        var queued = library.Music(Situations.Docked).Single(track => track.Name == "berth");
        _rejects.Add(queued.PackFolder!, queued.PackFile!);
        var ambience = new Ambience(new Random(1), track => _rejects.Contains(track.PackFolder ?? "", track.PackFile ?? ""));

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal("hangar", ambience.Draw(Situations.Docked, library)!.Name);
        }
    }

    private CapabilityDescriptor Capability() =>
        AudioCapability.Create(thumbsDown: () => _music.ThumbsDown(_token), maintainer: () => _token);

    private KeywordRouter Router() => new(CapabilityRegistry.Build([Capability()]));

    private async Task<string> Say(string phrase)
    {
        var match = Router().MatchToolCommand(phrase);
        Assert.NotNull(match);

        var registry = CapabilityRegistry.Build([Capability()]);
        var result = await registry.InvokeAsync(match.ToolName, match.Arguments, TestContext.Current.CancellationToken);
        return result.Content;
    }

    private string[] Rows() =>
        (_files.ReadText(Rejects) ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

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
