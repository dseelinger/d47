using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Stories;

/// <summary>While a story run plays, the mini panel reads it out in place of its text, with no buttons, and goes back afterwards.</summary>
[Trait("Category", "Integration")]
public sealed class TheMiniPanelShowsTheStoryPlaybackTests
{
    private static readonly Story Run = new()
    {
        Id = StoryFixture.Story.Id,
        Title = StoryFixture.Story.Title,
        PublicLayer = "-",
        PickedAt = new DateTimeOffset(2026, 11, 5, 20, 0, 0, TimeSpan.Zero),
        State = StoryState.Running,
        Chapters = ["ch1"],
    };

    private static readonly string LongBody = string.Join(" ", Enumerable.Range(1, 40).Select(n => $"Sentence number {n} of a long line."));

    private static StoryRunLine Line(int number, string? body = null) => new()
    {
        Sent = Run.PickedAt.AddMinutes(number),
        From = D47.Core.Messages.MessageStore.Narrator,
        Key = $"line {number}",
        Body = body ?? $"Line number {number} of the run.",
        Clip = $"line-{number}.opus",
    };

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static (Window Window, PanelView View, PanelViewModel Model, StoryRunPlayer Player, double[] Share) Shown(int lines, string? first = null)
    {
        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var player = new StoryRunPlayer(
            new AudioArbiter(new SteppedSink(), NullLogger<AudioArbiter>.Instance).Start(),
            (_, line) => new AudioClip(line.Body, new byte[960], AudioFormat.Standard),
            work => work());
        var share = new[] { 0.4 };
        var model = new PanelViewModel
        {
            StoryPlayer = player,
            StoryPlaybackProgress = () => new TrackProgress(TimeSpan.FromSeconds(share[0] * 10), TimeSpan.FromSeconds(10)),
        };
        var view = new PanelView { DataContext = model, Mode = PanelMode.Mini };
        var window = new Window { Width = 792, Height = 280, Content = view };
        window.Show();
        Jobs();

        player.Play(Run, [.. Enumerable.Range(1, lines).Select(n => Line(n, n == 1 ? first : null))], 0);
        model.StoryPlayback = true;
        Jobs();

        return (window, view, model, player, share);
    }

    private static T Named<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    [AvaloniaFact]
    public void APlaybackShowsItsLabelTimeLineAndHintWithNoButtons()
    {
        var (window, view, _, _, _) = Shown(6);

        var readout = Named<StoryPlaybackReadout>(view, "StoryReadout");

        Assert.True(readout.IsVisible);
        Assert.Equal("PLAYING 1 OF 6 · NARRATOR", Named<TextBlock>(readout, StoryPlaybackReadout.LabelName).Text);
        Assert.Equal("0:04 / 0:10", Named<TextBlock>(readout, StoryPlaybackReadout.TimeName).Text);
        Assert.Equal("Line number 1 of the run.", Named<TextBlock>(readout, StoryPlaybackReadout.TextName).Text);
        Assert.Equal(StoryPlaybackDialog.HintWithNext, Named<TextBlock>(readout, StoryPlaybackReadout.HintName).Text);
        Assert.Empty(readout.GetVisualDescendants().OfType<Button>());
        Assert.False(readout.IsHitTestVisible);

        window.SaveFrame("story-playback-mini-playing.png");
        window.Close();
    }

    [AvaloniaFact]
    public void OneLineLeavesNextOutOfTheHint()
    {
        var (window, view, _, _, _) = Shown(1);

        Assert.Equal(StoryPlaybackDialog.HintWithoutNext, Named<TextBlock>(Named<StoryPlaybackReadout>(view, "StoryReadout"), StoryPlaybackReadout.HintName).Text);

        window.Close();
    }

    [AvaloniaFact]
    public void PausingTurnsTheLabelAndTheProgressLineAmber()
    {
        var (window, view, _, player, _) = Shown(6);

        player.Pause(StoryRunPause.Commander);
        Jobs();
        Named<StoryPlaybackReadout>(view, "StoryReadout").Refresh();
        Jobs();

        var readout = Named<StoryPlaybackReadout>(view, "StoryReadout");

        Assert.Equal("PAUSED · 1 OF 6 · NARRATOR", Named<TextBlock>(readout, StoryPlaybackReadout.LabelName).Text);
        Assert.True(readout.Paused);
        Assert.True(Named<Border>(view, "StoryReadoutProgress").IsVisible);

        window.SaveFrame("story-playback-mini-paused.png");
        window.Close();
    }

    [AvaloniaFact]
    public void ALongLineScrollsToItsEndAsTheClipEnds()
    {
        var (window, view, _, _, share) = Shown(6, LongBody);

        var readout = Named<StoryPlaybackReadout>(view, "StoryReadout");
        var scroll = readout.GetVisualDescendants().OfType<ScrollViewer>().Single();

        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);

        share[0] = 0;
        readout.Poll();
        Assert.Equal(0, scroll.Offset.Y);

        share[0] = 1;
        readout.Poll();
        Assert.Equal(scroll.Extent.Height - scroll.Viewport.Height, scroll.Offset.Y, 1);

        window.SaveFrame("story-playback-mini-long-line.png");
        window.Close();
    }

    [AvaloniaFact]
    public void TheMiniPanelGoesBackWhenThePlaybackEnds()
    {
        var (window, view, model, player, _) = Shown(6);

        player.Stop();
        model.StoryPlayback = false;
        Jobs();

        var readout = Named<StoryPlaybackReadout>(view, "StoryReadout");

        Assert.False(readout.IsVisible);
        Assert.False(Named<Border>(view, "StoryReadoutProgress").IsVisible);

        window.Close();
    }
}
