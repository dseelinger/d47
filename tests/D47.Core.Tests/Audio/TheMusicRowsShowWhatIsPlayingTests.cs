using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>The Pause and Next rows at the top of the Ambient music group (#545).</summary>
public class TheMusicRowsShowWhatIsPlayingTests
{
    private MusicState _state = new(null, false, false);
    private readonly List<MusicAction> _pressed = [];
    private Action? _refresh;

    private (SettingRow Now, SettingRow Next) Rows()
    {
        var rows = AudioCapability.Create(
            music: action =>
            {
                _pressed.Add(action);
                return "";
            },
            musicState: () => _state,
            watchMusic: refresh =>
            {
                _refresh = refresh;
                return () => _refresh = null;
            }).Settings;

        Assert.Equal(AudioCapability.NowPlayingKey, rows.First(row => row.Group == "Ambient music").Key);
        return (rows.Single(row => row.Key == AudioCapability.NowPlayingKey),
                rows.Single(row => row.Key == AudioCapability.NextTrackKey));
    }

    private static D47Settings Muted() => new()
    {
        Audio = D47Settings.Defaults.Audio.With(AudioChannel.Music, new ChannelMix(1, Muted: true, 1)),
    };

    [Fact]
    public void TheLabelIsTheFileNameWithoutItsExtension()
    {
        _state = new MusicState(@"music\docked\berth.mp3", true, false);
        Assert.Equal("berth", Rows().Now.Binding!.Read(D47Settings.Defaults));
    }

    [Fact]
    public void PausingReadsResumeAndPressingItResumes()
    {
        var (now, _) = Rows();
        _state = new MusicState("berth.mp3", true, false);
        Assert.Equal("Pause", now.PressLabelFor!());
        now.Press!();

        _state = new MusicState("berth.mp3", false, true);
        Assert.Equal("Resume", now.PressLabelFor!());
        Assert.Equal("Paused", now.Binding!.Read(D47Settings.Defaults));
        now.Press!();

        Assert.Equal([MusicAction.Pause, MusicAction.Resume], _pressed);
    }

    [Fact]
    public void NextSkips()
    {
        Rows().Next.Press!();
        Assert.Equal([MusicAction.Next], _pressed);
    }

    [Fact]
    public void NothingPlayingDisablesBothAndSaysSo()
    {
        var (now, next) = Rows();

        Assert.Equal("Nothing playing", now.Binding!.Read(D47Settings.Defaults));
        Assert.False(now.PressEnabled!(D47Settings.Defaults));
        Assert.False(next.PressEnabled!(D47Settings.Defaults));
    }

    [Fact]
    public void MutedDisablesBothAndSaysSo()
    {
        _state = new MusicState("berth.mp3", true, false);
        var (now, next) = Rows();

        Assert.Equal("Muted", now.Binding!.Read(Muted()));
        Assert.False(now.PressEnabled!(Muted()));
        Assert.False(next.PressEnabled!(Muted()));
    }

    [Fact]
    public void ChangesFromElsewhereReachTheRowThroughWatch()
    {
        var (now, _) = Rows();
        var refreshed = 0;

        var stop = now.Watch!(() => refreshed++);
        _refresh!();
        Assert.Equal(1, refreshed);

        stop();
        Assert.Null(_refresh);
    }

    [Fact]
    public void NoMusicMeansNoRows()
    {
        Assert.DoesNotContain(AudioCapability.Create().Settings, row => row.Key == AudioCapability.NowPlayingKey);
    }
}
