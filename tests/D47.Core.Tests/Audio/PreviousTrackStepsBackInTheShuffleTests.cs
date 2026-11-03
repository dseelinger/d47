using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Previous plays the track before the current one in the folder's shuffle (#2087).</summary>
[Trait("Category", "Integration")]
public class PreviousTrackStepsBackInTheShuffleTests : IDisposable
{
    private const string Arrival = "arrival-from-hyperspace";
    private const string Hyperspace = "DestinationFromHyperspace";

    private static readonly DateTimeOffset Start = new(2026, 11, 3, 20, 0, 0, TimeSpan.Zero);

    private static readonly GameStatus Docked = Status(StatusFlags.Docked);

    private static readonly GameStatus InSupercruise = Status(StatusFlags.Supercruise | StatusFlags.InMainShip);

    private readonly MusicBench _bench = new(
        "d47-music-previous-tests",
        (Situations.Docked, "a"),
        (Situations.Docked, "b"),
        (Situations.Docked, "c"),
        (Situations.Supercruise, "cruise"),
        (Arrival, "arrive"));

    public PreviousTrackStepsBackInTheShuffleTests() => _bench.Music.GameRunning(running: true);

    public void Dispose() => _bench.Dispose();

    [Fact]
    public void PreviousPlaysTheTrackBeforeTheCurrentOneFromTheStart()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        var first = _bench.Sink.Started[0];
        _bench.Music.Control(MusicAction.Next);
        var second = _bench.Sink.Started[1];

        var said = _bench.Music.Control(MusicAction.Previous);

        Assert.Equal("Previous track.", said);
        Assert.Contains(second.Id, _bench.Sink.Stopped);
        Assert.Equal(3, _bench.Sink.Started.Count);
        Assert.Equal(first.Name, _bench.Sink.Started[2].Name);
    }

    [Fact]
    public void NextAfterPreviousReplaysTheTrackPreviousLeft()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        _bench.Music.Control(MusicAction.Next);
        var second = _bench.Sink.Started[1];
        _bench.Music.Control(MusicAction.Previous);

        _bench.Music.Control(MusicAction.Next);

        Assert.Equal(second.Name, _bench.Sink.Started[3].Name);
    }

    [Fact]
    public void OnTheFirstTrackOfAShuffleThePlayingTrackRestarts()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        var first = _bench.Sink.Started[0];

        var said = _bench.Music.Control(MusicAction.Previous);

        Assert.Equal("Restarted this track.", said);
        Assert.Contains(first.Id, _bench.Sink.Stopped);
        Assert.Equal(2, _bench.Sink.Started.Count);
        Assert.Equal(first.Name, _bench.Sink.Started[1].Name);
    }

    [Fact]
    public void ATrackQueuedForDeletionIsSteppedOver()
    {
        var library = _bench.Library();
        var rejected = new HashSet<string>();
        var ambience = new Ambience(new Random(7), track => rejected.Contains(track.Name));
        ambience.Enter(Situations.Docked);

        var first = ambience.Next(library)!;
        var second = ambience.Next(library)!;
        var third = ambience.Next(library)!;
        rejected.Add(second.Name);

        var (back, restarted) = ambience.Previous(library, third);

        Assert.Equal(first, back);
        Assert.False(restarted);
    }

    [Fact]
    public void WhenEveryEarlierTrackIsQueuedForDeletionThePlayingTrackRestarts()
    {
        var library = _bench.Library();
        var rejected = new HashSet<string>();
        var ambience = new Ambience(new Random(7), track => rejected.Contains(track.Name));
        ambience.Enter(Situations.Docked);

        var first = ambience.Next(library)!;
        var second = ambience.Next(library)!;
        rejected.Add(first.Name);

        var (back, restarted) = ambience.Previous(library, second);

        Assert.Equal(second, back);
        Assert.True(restarted);
    }

    [Fact]
    public void DuringAOneShotCuePreviousEndsItAndContinuesTheShelvedTrack()
    {
        _bench.Music.Follow(InSupercruise, null, Start);
        var cruise = _bench.Sink.Started[0];
        _bench.Music.Follow(InSupercruise, Hyperspace, Start);
        Assert.Equal("arrive", _bench.Music.State.Track);

        _bench.Music.Control(MusicAction.Previous);

        Assert.Equal("cruise", _bench.Music.State.Track);
        Assert.Contains($"resume {cruise.Id}", _bench.Sink.Log);
    }

    [Fact]
    public void PreviousEndsAPause()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        _bench.Music.Control(MusicAction.Pause);

        _bench.Music.Control(MusicAction.Previous);

        Assert.False(_bench.Music.Paused);
        Assert.True(_bench.Music.State.Playing);
    }

    [Fact]
    public void WithEliteClosedPreviousSaysSoAndPlaysNothing()
    {
        _bench.Music.GameRunning(running: false);

        var said = _bench.Music.Control(MusicAction.Previous);

        Assert.Equal("Elite is not running.", said);
        Assert.Empty(_bench.Sink.Started);
    }

    [Fact]
    public void WhileMutedPreviousGivesNextsMutedReply()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        _bench.Audio.Mix = _bench.Audio.Mix.With(AudioChannel.Music, _bench.Audio.Mix.Music with { Muted = true });

        var said = _bench.Music.Control(MusicAction.Previous);

        Assert.Equal(_bench.Music.Control(MusicAction.Next), said);
        Assert.Contains("muted", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePhraseAndTheToolReachPrevious()
    {
        var tool = AudioCapability.Create().Tools.Single(t => t.Name == AudioCapability.ManageMusicTool);
        var command = Assert.Single(tool.Commands, c => c.Phrase == "previous track");
        Assert.Equal("previous", command.Arguments["action"]);

        MusicAction? ran = null;
        var wired = AudioCapability.Create(music: action =>
        {
            ran = action;
            return "Previous track.";
        }).Tools.Single(t => t.Name == AudioCapability.ManageMusicTool);

        var result = await wired.Handler(
            new D47.Core.Capabilities.ToolArguments(new Dictionary<string, string> { ["action"] = "previous" }),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(MusicAction.Previous, ran);
    }

    private static GameStatus Status(StatusFlags flags) =>
        new() { Flags = flags, ReadAt = DateTimeOffset.UnixEpoch };
}
