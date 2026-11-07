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

/// <summary>The audio player draws the music's state and does what the Ambient music rows do; the mini panel reads it out.</summary>
[Trait("Category", "Integration")]
public sealed class TheAudioPlayerShowsTheMusicTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static MusicState Playing => new("long-drift-04.mp3", true, false, false, "supercruise");

    private static (AudioPlayerDialog Dialog, PanelViewModel Model, List<MusicAction> Actions, Func<int> Downs, Window Window) Shown(MusicState state)
    {
        new D47.App.Theming.ThemeManager(Application.Current!, Microsoft.Extensions.Logging.Abstractions.NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var settings = TestSurface.Settings();
        var model = new PanelViewModel { AudioPlayer = AudioPlayerReadout.From(state, false) };
        var actions = new List<MusicAction>();
        var downs = 0;

        var window = new MainWindow(host: null);
        window.Show();
        Jobs();

        var dialog = new AudioPlayerDialog(model, settings, action => { actions.Add(action); return string.Empty; }, () => downs++);
        _ = dialog.Over(window);
        Jobs();

        return (dialog, model, actions, () => downs, window);
    }

    private static T Named<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Press(Button button) =>
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void APlayingTrackShowsItsNameFolderAndStateAndPressesDoWhatTheRowsDo()
    {
        var (dialog, _, actions, downs, window) = Shown(Playing);

        Assert.Equal("long-drift-04.mp3", Named<TextBlock>(dialog, AudioPlayerDialog.TrackName).Text);
        Assert.Equal("supercruise", Named<TextBlock>(dialog, AudioPlayerDialog.FolderName).Text);
        Assert.Equal("Playing", Named<TextBlock>(dialog, AudioPlayerDialog.StateName).Text);
        Assert.Equal("PAUSE", Named<Button>(dialog, AudioPlayerDialog.PlayName).Content);

        Press(Named<Button>(dialog, AudioPlayerDialog.PlayName));
        Press(Named<Button>(dialog, AudioPlayerDialog.NextName));
        Press(Named<Button>(dialog, AudioPlayerDialog.PreviousName));
        Press(Named<Button>(dialog, AudioPlayerDialog.ThumbsDownName));

        Assert.Equal([MusicAction.Pause, MusicAction.Next, MusicAction.Previous], actions);
        Assert.Equal(1, downs());

        window.SaveFrame("audio-player-playing.png");
    }

    [AvaloniaFact]
    public void ThePlayerFollowsTheMusicWhenItPausesAndWhenItStops()
    {
        var (dialog, model, actions, _, window) = Shown(Playing);

        model.AudioPlayer = AudioPlayerReadout.From(new MusicState("long-drift-04.mp3", false, true, false, "supercruise"), false);
        Jobs();

        Assert.Equal("Paused", Named<TextBlock>(dialog, AudioPlayerDialog.StateName).Text);
        Assert.Equal("PLAY", Named<Button>(dialog, AudioPlayerDialog.PlayName).Content);

        Press(Named<Button>(dialog, AudioPlayerDialog.PlayName));
        Assert.Equal([MusicAction.Resume], actions);

        window.SaveFrame("audio-player-paused.png");

        model.AudioPlayer = AudioPlayerReadout.From(new MusicState(null, false, false), false);
        Jobs();

        Assert.Equal("Nothing playing", Named<TextBlock>(dialog, AudioPlayerDialog.StateName).Text);
        Assert.Equal(AudioPlayerReadout.Nothing, Named<TextBlock>(dialog, AudioPlayerDialog.TrackName).Text);

        foreach (var name in new[] { AudioPlayerDialog.PreviousName, AudioPlayerDialog.PlayName, AudioPlayerDialog.NextName, AudioPlayerDialog.ThumbsDownName })
        {
            Assert.False(Named<Button>(dialog, name).IsEnabled, name);
        }

        window.SaveFrame("audio-player-idle.png");
    }

    [AvaloniaFact]
    public void MutedLeavesTheButtonsOffAndTheLevelPressable()
    {
        var (dialog, model, _, _, _) = Shown(Playing);

        model.AudioPlayer = AudioPlayerReadout.From(Playing, muted: true);
        Jobs();

        Assert.Equal("Muted", Named<TextBlock>(dialog, AudioPlayerDialog.StateName).Text);
        Assert.False(Named<Button>(dialog, AudioPlayerDialog.PlayName).IsEnabled);
        Assert.True(Named<Level>(dialog, AudioPlayerDialog.LevelName).IsEnabled);
    }

    [AvaloniaFact]
    public void TheFootNamesTheBoundKey()
    {
        var (dialog, _, _, _, _) = Shown(Playing);

        var texts = Named<StackPanel>(dialog, AudioPlayerDialog.HintName).GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();

        Assert.Contains("CTRL+SHIFT+P", texts);
    }

    [AvaloniaFact]
    public void CloseLeavesTheDialog()
    {
        var (dialog, _, _, _, _) = Shown(Playing);

        Press(Named<Button>(dialog, AudioPlayerDialog.CloseName));
        Jobs();

        Assert.True(dialog.Left.IsCompleted);
    }

    [AvaloniaFact]
    public void AMiniPanelReadsTheTrackOutOnlyWhileThePlayerIsOpen()
    {
        new D47.App.Theming.ThemeManager(Application.Current!, Microsoft.Extensions.Logging.Abstractions.NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var model = new PanelViewModel { AudioPlayer = AudioPlayerReadout.From(Playing, false) };
        var view = new PanelView { DataContext = model, Mode = PanelMode.Mini };
        var window = new Window { Width = 512, Height = 280, Content = view };
        window.Show();
        Jobs();

        var readout = Named<AudioReadout>(view, "AudioReadoutBar");

        Assert.False(readout.IsVisible);

        model.AudioPlayerOpen = true;
        Jobs();

        Assert.True(readout.IsVisible);
        Assert.Equal("PLAYING", Named<TextBlock>(readout, AudioReadout.StateName).Text);
        Assert.Equal("long-drift-04.mp3", Named<TextBlock>(readout, AudioReadout.TrackName).Text);
        Assert.Contains("“pause”", Named<TextBlock>(readout, AudioReadout.SayName).Text);
        Assert.StartsWith("supercruise", Named<TextBlock>(readout, AudioReadout.SayName).Text);

        window.SaveFrame("audio-player-mini.png");

        model.AudioPlayer = AudioPlayerReadout.From(new MusicState("long-drift-04.mp3", false, true, false, "supercruise"), false);
        Jobs();

        Assert.Equal("PAUSED", Named<TextBlock>(readout, AudioReadout.StateName).Text);
        Assert.Contains("“play”", Named<TextBlock>(readout, AudioReadout.SayName).Text);

        model.AudioPlayerOpen = false;
        Jobs();

        Assert.False(readout.IsVisible);
    }
}
