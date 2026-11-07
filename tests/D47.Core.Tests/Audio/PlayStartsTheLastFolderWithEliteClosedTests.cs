using D47.Core.Audio;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>With Elite closed, play, next and previous start the last folder that played, and it carries on.</summary>
[Trait("Category", "Integration")]
public sealed class PlayStartsTheLastFolderWithEliteClosedTests : IDisposable
{
    private static readonly (string, string)[] Tracks =
    [
        (Situations.Docked, "berth"),
        (Situations.Docked, "hangar"),
        (Situations.Supercruise, "cruise"),
        (Situations.General, "lobby"),
    ];

    private static readonly GameStatus Docked = new() { Flags = StatusFlags.Docked, ReadAt = DateTimeOffset.UnixEpoch };

    private static readonly GameStatus InSupercruise = new() { Flags = StatusFlags.Supercruise | StatusFlags.InMainShip, ReadAt = DateTimeOffset.UnixEpoch };

    private MusicBench? _bench;

    private MusicBench Bench(string? lastFolder = null) => _bench = new MusicBench("d47-music-closed-tests", lastFolder, Tracks);

    public void Dispose() => _bench?.Dispose();

    [Fact]
    public void BeforePlayTheStateSaysTheFolderCanBeStarted()
    {
        var bench = Bench(Situations.Supercruise);

        Assert.True(bench.Music.State.Startable);
        Assert.Equal(Situations.Supercruise, bench.Music.State.Folder);
        Assert.Empty(bench.Sink.Started);
    }

    [Fact]
    public void ASavedFolderPlaysWhenEliteHasNotRunThisSession()
    {
        var bench = Bench(Situations.Supercruise);

        Assert.Equal("Music resumed.", bench.Music.Control(MusicAction.Resume));

        Assert.Equal("cruise", Assert.Single(bench.Sink.Started).Name);
        Assert.True(bench.Music.State.Playing);
        Assert.False(bench.Music.State.Startable);
    }

    [Fact]
    public void ATrackThatFinishesStartsTheNextFromTheSameFolder()
    {
        var bench = Bench(Situations.Docked);
        bench.Music.Control(MusicAction.Resume);

        bench.Music.TrackFinished();

        Assert.Equal(2, bench.Sink.Started.Count);
        Assert.Equal(Situations.Docked, bench.Music.LastFolder);
    }

    [Fact]
    public void EliteExitingKeepsTheFolderAndPlayStartsItAgain()
    {
        var bench = Bench();
        bench.Music.GameRunning(running: true);
        bench.Music.Follow(InSupercruise, musicTrack: null);
        bench.Music.GameRunning(running: false);

        Assert.Equal(Situations.Supercruise, bench.Music.LastFolder);

        bench.Music.Control(MusicAction.Resume);

        Assert.Equal(2, bench.Sink.Started.Count);
        Assert.Equal("cruise", bench.Sink.Started[1].Name);
    }

    [Fact]
    public void EliteStartingHandsTheMusicToTheSituation()
    {
        var bench = Bench(Situations.Supercruise);
        bench.Music.Control(MusicAction.Resume);

        bench.Music.GameRunning(running: true);
        bench.Music.Follow(Docked, musicTrack: null);

        Assert.Equal(Situations.Docked, bench.Music.State.Folder);
        Assert.Equal(Situations.Docked, bench.Music.LastFolder);
    }

    [Fact]
    public void MutedStillStartsNothing()
    {
        var bench = Bench(Situations.Supercruise);
        bench.Audio.Mix = bench.Audio.Mix.With(AudioChannel.Music, bench.Audio.Mix.Music with { Muted = true });

        var said = bench.Music.Control(MusicAction.Resume);

        Assert.Contains("muted", said, StringComparison.Ordinal);
        Assert.Empty(bench.Sink.Started);
        Assert.False(bench.Music.State.Startable);
    }
}
