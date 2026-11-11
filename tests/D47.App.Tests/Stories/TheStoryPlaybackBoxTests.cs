using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Voice;
using D47.App.Windowing;
using D47.Core.Audio;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Stories;

/// <summary>The story playback box follows the run player: half the host wide, Next only for a run of lines, and Stop ends the playback.</summary>
[Trait("Category", "Integration")]
public sealed class TheStoryPlaybackBoxTests
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

    private static StoryRunLine Line(int number) => new()
    {
        Sent = Run.PickedAt.AddMinutes(number),
        From = D47.Core.Messages.MessageStore.Narrator,
        Key = $"line {number}",
        Body = $"Line number {number} of the run.",
        Clip = $"line-{number}.opus",
    };

    private sealed record Shown(Window Window, StoryRunPlayer Player, StoryPlaybackDialog Box);

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static Shown Open(int lines)
    {
        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var player = new StoryRunPlayer(
            new AudioArbiter(new SteppedSink(), NullLogger<AudioArbiter>.Instance).Start(),
            (_, line) => new AudioClip(line.Body, new byte[960], AudioFormat.Standard),
            work => work());
        var window = new Window { Content = new Border(), Width = 1600, Height = 1000 };
        window.Show();

        player.Play(Run, [.. Enumerable.Range(1, lines).Select(Line)], 0);

        var box = new StoryPlaybackDialog(player, () => new TrackProgress(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(11)), _ => "Chapter 1 · The Beacon Job", null);
        _ = box.Over(window);
        Jobs();

        return new Shown(window, player, box);
    }

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().First(button => button.Name == name);

    private static string Text(Control root, string name) =>
        root.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == name).Text ?? string.Empty;

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
    }

    [AvaloniaFact]
    public void TheFrameIsHalfTheHostsWidth()
    {
        var (window, _, box) = Open(6);

        var frame = (Border)box.GetVisualParent()!;
        var host = window.GetVisualDescendants().OfType<ModalHost>().Single();

        Assert.Equal(ModalHost.FrameName, frame.Name);
        Assert.InRange(frame.Bounds.Width, host.Bounds.Width / 2 - 2, host.Bounds.Width / 2 + 2);
        Assert.True(frame.Bounds.Height <= host.Bounds.Height - 2 * Modal.ScrimPadding + 1, $"{frame.Bounds.Height} high in {host.Bounds.Height}");

        window.SaveFrame("story-playback-box-playing.png");
        window.Close();
    }

    [AvaloniaFact]
    public void ThePictureAreaShrinksWhereTheHostIsShort()
    {
        var (window, _, box) = Open(6);
        var tall = box.Bounds.Height;

        window.Height = 700;
        Jobs();

        Assert.True(box.Bounds.Height < tall);
        Assert.True(box.Bounds.Height <= 700 - 2 * Modal.ScrimPadding + 1);

        window.Close();
    }

    [AvaloniaFact]
    public void NextShowsForARunOfLinesAndMovesOnByOne()
    {
        var (window, player, box) = Open(6);

        Assert.True(Named(box, StoryPlaybackDialog.NextName).IsVisible);
        Assert.Equal("PLAYING 1 OF 6 · NARRATOR", Text(box, StoryPlaybackDialog.LabelName));

        Press(Named(box, StoryPlaybackDialog.NextName));

        Assert.Equal(1, player.Now!.Index);
        Assert.Equal("PLAYING 2 OF 6 · NARRATOR", Text(box, StoryPlaybackDialog.LabelName));
        Assert.Equal(StoryPlaybackDialog.HintWithNext, Text(box, StoryPlaybackDialog.HintName));

        window.Close();
    }

    [AvaloniaFact]
    public void OneLineHasNoCountNoNextAndAShorterHint()
    {
        var (window, _, box) = Open(1);

        Assert.False(Named(box, StoryPlaybackDialog.NextName).IsVisible);
        Assert.Equal("PLAYING · NARRATOR", Text(box, StoryPlaybackDialog.LabelName));
        Assert.Equal(StoryPlaybackDialog.HintWithoutNext, Text(box, StoryPlaybackDialog.HintName));
        Assert.Equal("0:04 / 0:11", Text(box, StoryPlaybackDialog.TimeName));

        window.Close();
    }

    [AvaloniaFact]
    public void PausingShowsPausedAndPlayAndPushToTalkSaysSo()
    {
        var (window, player, box) = Open(6);

        Press(Named(box, StoryPlaybackDialog.PlayName));

        Assert.Equal("PAUSED · 1 OF 6 · NARRATOR", Text(box, StoryPlaybackDialog.LabelName));
        Assert.Equal("Play", Named(box, StoryPlaybackDialog.PlayName).Content);
        Assert.True(box.GetVisualDescendants().OfType<Border>().Single(b => b.Name == StoryPlaybackDialog.PausedChipName).IsVisible);
        Assert.Equal(D47.App.Panel.StoryRunPage.PausedText, Text(box, StoryPlaybackDialog.StateLineName));
        window.SaveFrame("story-playback-box-paused.png");

        Press(Named(box, StoryPlaybackDialog.PlayName));
        player.Pause(StoryRunPause.PushToTalk);
        Jobs();

        Assert.Equal(D47.App.Panel.StoryRunPage.PushToTalkText, Text(box, StoryPlaybackDialog.StateLineName));

        window.Close();
    }

    [AvaloniaFact]
    public void StopEndsThePlaybackAndClosesTheBox()
    {
        var (window, player, box) = Open(6);

        Press(Named(box, StoryPlaybackDialog.StopName));

        Assert.Null(player.Now);
        Assert.Empty(window.Modals());

        window.Close();
    }

    [AvaloniaFact]
    public void ClosingTheBoxStopsThePlaybackButDismissingItDoesNot()
    {
        var (window, player, box) = Open(6);

        box.Dismiss();
        Jobs();

        Assert.NotNull(player.Now);
        Assert.Empty(window.Modals());

        var (second, again, boxAgain) = Open(6);
        boxAgain.Close();
        Jobs();

        Assert.Null(again.Now);

        window.Close();
        second.Close();
    }

    [AvaloniaFact]
    public void TheBoxClosesWhenTheLastLineEnds()
    {
        var (window, player, _) = Open(2);

        player.Skip();
        player.Skip();
        Jobs();

        Assert.Null(player.Now);
        Assert.Empty(window.Modals());

        window.Close();
    }
}
