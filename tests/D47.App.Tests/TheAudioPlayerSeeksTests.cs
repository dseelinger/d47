using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Windowing;
using D47.Core.Audio;
using Xunit;

namespace D47.App.Tests;

/// <summary>The audio player shows how far through the track it is and seeks once, when a drag is released.</summary>
[Trait("Category", "Integration")]
public sealed class TheAudioPlayerSeeksTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static MusicState Playing => new("long-drift-04.mp3", true, false, false, "supercruise");

    private static (AudioPlayerDialog Dialog, PanelViewModel Model, List<TimeSpan> Seeks, Window Window) Shown(
        MusicState state, Func<TrackProgress?> progress, bool seekable = true)
    {
        new D47.App.Theming.ThemeManager(Application.Current!, Microsoft.Extensions.Logging.Abstractions.NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var model = new PanelViewModel { AudioPlayer = AudioPlayerReadout.From(state, false) };
        var seeks = new List<TimeSpan>();
        var window = new MainWindow(host: null);
        window.Show();
        Jobs();

        var dialog = new AudioPlayerDialog(
            model, TestSurface.Settings(), _ => string.Empty, () => { }, progress,
            seekable ? to => { seeks.Add(to); return true; } : null);
        _ = dialog.Over(window);
        Jobs();

        return (dialog, model, seeks, window);
    }

    private static T Named<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static TrackProgress Known => new(TimeSpan.FromSeconds(102), TimeSpan.FromSeconds(258));

    [AvaloniaFact]
    public void TheBarAndTimesShowHowFarThroughTheTrackItIs()
    {
        var (dialog, _, _, window) = Shown(Playing, () => Known);

        Assert.Equal("1:42", Named<TextBlock>(dialog, AudioPlayerDialog.ElapsedName).Text);
        Assert.Equal("4:18", Named<TextBlock>(dialog, AudioPlayerDialog.LengthName).Text);
        Assert.Equal(102.0 / 258, Named<SeekBar>(dialog, AudioPlayerDialog.SeekName).Position, 6);
        Assert.True(Named<SeekBar>(dialog, AudioPlayerDialog.SeekName).Active);

        window.SaveFrame("audio-player-seek.png");
    }

    [AvaloniaFact]
    public void AnUnknownLengthOrNothingPlayingLeavesTheBarEmptyAndGreyedOut()
    {
        var (dialog, model, _, _) = Shown(Playing, () => new TrackProgress(TimeSpan.Zero, null));
        var bar = Named<SeekBar>(dialog, AudioPlayerDialog.SeekName);

        Assert.Equal("0:00", Named<TextBlock>(dialog, AudioPlayerDialog.ElapsedName).Text);
        Assert.Equal(AudioPlayerReadout.UnknownClock, Named<TextBlock>(dialog, AudioPlayerDialog.LengthName).Text);
        Assert.Equal(0, bar.Position);

        model.AudioPlayer = AudioPlayerReadout.From(new MusicState(null, false, false), false);
        Jobs();

        Assert.False(bar.Active);
        Assert.Equal(AudioPlayerReadout.UnknownClock, Named<TextBlock>(dialog, AudioPlayerDialog.ElapsedName).Text);

        model.AudioPlayer = AudioPlayerReadout.From(Playing, muted: true);
        Jobs();

        Assert.False(bar.Active);
    }

    [AvaloniaFact]
    public void ADragShowsTheTargetTimeAndSeeksOnceOnRelease()
    {
        var (dialog, _, seeks, _) = Shown(Playing, () => Known);
        var bar = Named<SeekBar>(dialog, AudioPlayerDialog.SeekName);

        bar.Press(0.25);
        bar.Move(0.5);
        dialog.Poll();

        Assert.Equal("2:09", Named<TextBlock>(dialog, AudioPlayerDialog.ElapsedName).Text);
        Assert.Empty(seeks);

        bar.Release(0.5);

        Assert.Equal([TimeSpan.FromSeconds(129)], seeks);
    }

    [AvaloniaFact]
    public void TheBarOnTheBigPanelIsNotDraggable()
    {
        var (dialog, _, seeks, _) = Shown(Playing, () => Known, seekable: false);
        var bar = Named<SeekBar>(dialog, AudioPlayerDialog.SeekName);

        Assert.False(bar.Draggable);
        bar.Press(0.5);
        bar.Release(0.5);

        Assert.Empty(seeks);
    }

    [AvaloniaFact]
    public void TheTimerRunsOnlyWhileTheDialogIsOpen()
    {
        var (dialog, _, _, _) = Shown(Playing, () => Known);

        Assert.True(dialog.Polling);

        dialog.Close();
        Jobs();

        Assert.False(dialog.Polling);
    }

    [AvaloniaFact]
    public void AMiniPanelShowsTheTimesAndBarOnlyWhileThePlayerIsOpen()
    {
        new D47.App.Theming.ThemeManager(Application.Current!, Microsoft.Extensions.Logging.Abstractions.NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var model = new PanelViewModel { AudioPlayer = AudioPlayerReadout.From(Playing, false), AudioProgress = () => Known };
        var view = new PanelView { DataContext = model, Mode = PanelMode.Mini };
        var window = new Window { Width = 512, Height = 280, Content = view };
        window.Show();
        Jobs();

        var readout = Named<AudioReadout>(view, "AudioReadoutBar");

        Assert.False(readout.Polling);

        model.AudioPlayerOpen = true;
        Jobs();

        Assert.True(readout.Polling);
        Assert.Equal("1:42", Named<TextBlock>(readout, AudioReadout.ElapsedName).Text);
        Assert.Equal("4:18", Named<TextBlock>(readout, AudioReadout.LengthName).Text);

        window.SaveFrame("audio-player-mini-bar.png");

        model.AudioPlayer = AudioPlayerReadout.From(new MusicState(null, false, false), false);
        Jobs();

        Assert.Equal(AudioPlayerReadout.UnknownClock, Named<TextBlock>(readout, AudioReadout.LengthName).Text);

        model.AudioPlayerOpen = false;
        Jobs();

        Assert.False(readout.Polling);
    }
}
