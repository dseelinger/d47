using D47.Core.Audio;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Starting or resuming a music track writes one Information line naming folder, source and file (#2219).</summary>
public class EachMusicTrackIsLoggedAsItStartsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 6, 20, 0, 0, TimeSpan.Zero);

    private readonly RecordingLogger<AudioArbiter> _log = new();
    private readonly AudioArbiter _audio;

    public EachMusicTrackIsLoggedAsItStartsTests()
    {
        _audio = new AudioArbiter(new RecordingAudioSink(), _log).Start();
    }

    private static MusicTrack Library() =>
        new("supercruise-109", () => throw new NotSupportedException(), "supercruise", "supercruise-109.mp3");

    private static MusicTrack Own() =>
        new("Tower", () => throw new NotSupportedException(), OwnPath: Path.Combine("C:", "Users", "someone", "music", "Tower.mp3"));

    [Fact]
    public void ALibraryTrackIsLoggedWithItsFileName()
    {
        _audio.PlayMusic(Library(), "supercruise");

        Assert.Equal(["Music started: supercruise, library, supercruise-109.mp3"], _log.Lines);
        Assert.All(_log.Entries, entry => Assert.Equal(LogLevel.Information, entry.Level));
    }

    [Fact]
    public void AnOwnTrackIsLoggedWithItsFileNameAndNotItsPath()
    {
        _audio.PlayMusic(Own(), "supercruise");

        Assert.Equal(["Music started: supercruise, own, Tower.mp3"], _log.Lines);
    }

    [Fact]
    public void ATrackAfterAGapIsLoggedOnceWhenRequested()
    {
        _audio.PlayMusic(Library(), "supercruise", afterGap: true);

        Assert.Single(_log.Lines);
    }

    [Fact]
    public void ResumingAShelvedTrackIsLoggedAndAnEmptyShelfIsNot()
    {
        _audio.PlayMusic(Own(), "docked");
        _audio.ShelveMusic(Start);
        _audio.PlayMusic(Library(), "supercruise");
        _log.Entries.Clear();

        Assert.Null(_audio.UnshelveMusic("hangar"));
        Assert.Empty(_log.Lines);

        Assert.NotNull(_audio.UnshelveMusic("docked"));
        Assert.Equal(["Music resumed: docked, own, Tower.mp3"], _log.Lines);
    }
}
