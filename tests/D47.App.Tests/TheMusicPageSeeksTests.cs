using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Audio;
using Xunit;
using static D47.App.Tests.TheMusicPageShowsTheMusicTests;

namespace D47.App.Tests;

/// <summary>Media › Music shows how far through the track it is and seeks once, when a drag is released (#2683).</summary>
[Trait("Category", "Integration")]
public sealed class TheMusicPageSeeksTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static MusicState Playing => new("long-drift-04.mp3", true, false, false, "supercruise");

    private static TrackProgress Known => new(TimeSpan.FromSeconds(102), TimeSpan.FromSeconds(258));

    /// <summary>The page's poll timer, read from its field.</summary>
    private static DispatcherTimer Timer(MusicPage page) =>
        (DispatcherTimer)typeof(MusicPage).GetField("_poll", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page)!;

    [AvaloniaFact]
    public void TheBarAndTimesShowHowFarThroughTheTrackItIs()
    {
        var shown = Open(Playing, () => Known);
        var page = shown.Page;

        Assert.Equal("1:42", Named<TextBlock>(page, MusicPage.ElapsedName).Text);
        Assert.Equal("4:18", Named<TextBlock>(page, MusicPage.LengthName).Text);
        Assert.Equal(102.0 / 258, Named<SeekBar>(page, MusicPage.SeekName).Position, 6);
        Assert.True(Named<SeekBar>(page, MusicPage.SeekName).Active);
    }

    [AvaloniaFact]
    public void AnUnknownLengthOrNothingPlayingLeavesTheBarEmptyAndGreyedOut()
    {
        var shown = Open(Playing, () => new TrackProgress(TimeSpan.Zero, null));
        var page = shown.Page;
        var bar = Named<SeekBar>(page, MusicPage.SeekName);

        Assert.Equal("0:00", Named<TextBlock>(page, MusicPage.ElapsedName).Text);
        Assert.Equal(AudioPlayerReadout.UnknownClock, Named<TextBlock>(page, MusicPage.LengthName).Text);
        Assert.Equal(0, bar.Position);

        shown.Model.AudioPlayer = AudioPlayerReadout.From(new MusicState(null, false, false), false);
        Jobs();

        Assert.False(bar.Active);
        Assert.Equal(AudioPlayerReadout.UnknownClock, Named<TextBlock>(page, MusicPage.ElapsedName).Text);

        shown.Model.AudioPlayer = AudioPlayerReadout.From(Playing, muted: true);
        Jobs();

        Assert.False(bar.Active);
    }

    [AvaloniaFact]
    public void ADragShowsTheTargetTimeAndSeeksOnceOnRelease()
    {
        var shown = Open(Playing, () => Known);
        var page = shown.Page;
        var bar = Named<SeekBar>(page, MusicPage.SeekName);

        bar.Press(0.25);
        bar.Move(0.5);
        page.Poll();

        Assert.Equal("2:09", Named<TextBlock>(page, MusicPage.ElapsedName).Text);
        Assert.Empty(shown.Seeks);

        bar.Release(0.5);

        Assert.Equal([TimeSpan.FromSeconds(129)], shown.Seeks);
    }

    [AvaloniaFact]
    public void TheBarOnTheBigPanelIsNotDraggable()
    {
        var shown = Open(Playing, () => Known, headset: true);
        var bar = Named<SeekBar>(shown.Page, MusicPage.SeekName);

        Assert.False(bar.Draggable);
        Assert.Equal(MusicPage.PollEveryInHeadset, Timer(shown.Page).Interval);
        bar.Press(0.5);
        bar.Release(0.5);

        Assert.Empty(shown.Seeks);
    }

    [AvaloniaFact]
    public void TheTimerRunsOnlyWhileThePageIsShown()
    {
        var shown = Open(Playing, () => Known);
        var page = shown.Page;

        Assert.True(Timer(page).IsEnabled);

        Assert.True(shown.Panel.Nav.Previous());
        Jobs();

        Assert.False(Timer(page).IsEnabled);
    }

    [AvaloniaFact]
    public void AMiniPanelShowsTheTimesAndBarOnlyWhileItIsOnMediaMusic()
    {
        var shown = Open(Playing, () => Known, mode: PanelMode.Mini);
        var readout = Named<AudioReadout>(shown.Panel, "AudioReadoutBar");

        Assert.True(readout.Polling);
        Assert.Equal("1:42", Named<TextBlock>(readout, AudioReadout.ElapsedName).Text);
        Assert.Equal("4:18", Named<TextBlock>(readout, AudioReadout.LengthName).Text);

        shown.Model.AudioPlayer = AudioPlayerReadout.From(new MusicState(null, false, false), false);
        Jobs();

        Assert.Equal(AudioPlayerReadout.UnknownClock, Named<TextBlock>(readout, AudioReadout.LengthName).Text);

        Assert.True(shown.Panel.Nav.Previous());
        Jobs();

        Assert.False(readout.Polling);
    }
}
