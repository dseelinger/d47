using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Voice;
using D47.App.Windowing;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Stories;

/// <summary>The headset's big panel opens its own copy of the story playback box while a story plays, and Stop on it ends the playback.</summary>
[Trait("Category", "Integration")]
public sealed class TheStoryPlaybackBoxOpensInTheHeadsetTests
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

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static (VrPanelSurface Surface, PanelViewModel Model, StoryRunPlayer Player) Headset(PanelMode mode)
    {
        var (settings, _, _) = TestSurface.Create();

        settings.Apply(VrCapability.ModeKey, mode == PanelMode.Full ? "full" : "mini", SettingsCaller.Panel);

        var player = new StoryRunPlayer(
            new AudioArbiter(new SteppedSink(), NullLogger<AudioArbiter>.Instance).Start(),
            (_, line) => new AudioClip(line.Body, new byte[960], AudioFormat.Standard),
            work => work());
        var model = new PanelViewModel();
        var surface = new VrPanelSurface(
            model, settings, _ => null,
            storyPlayback: () => new StoryPlaybackDialog(
                player, () => null, _ => null, null, StoryPlaybackDialog.PollEveryInHeadset));

        Jobs();
        return (surface, model, player);
    }

    [AvaloniaFact]
    public void ThePlaybackBoxOpensOnTheBigPanelAtHalfItsWidthAndStopClosesIt()
    {
        var (surface, model, player) = Headset(PanelMode.Full);
        using var _ = surface;

        player.Play(Run, [.. Enumerable.Range(1, 6).Select(Line)], 0);
        model.StoryPlayback = true;
        Jobs();
        surface.Board.Render();
        Jobs();
        surface.Board.Render();

        var box = surface.Board.View.GetVisualDescendants().OfType<StoryPlaybackDialog>().Single();
        var frame = surface.Board.View.GetVisualDescendants().OfType<Border>().Single(b => b.Name == ModalHost.FrameName);

        Assert.InRange(frame.Bounds.Width, surface.Size.Width / 2.0 - 2, surface.Size.Width / 2.0 + 2);

        box.GetVisualDescendants().OfType<Button>().First(b => b.Name == StoryPlaybackDialog.StopName)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();

        Assert.Null(player.Now);
        Assert.DoesNotContain(surface.Board.View.GetVisualDescendants(), visual => visual is StoryPlaybackDialog);
    }

    [AvaloniaFact]
    public void TheMiniPanelOpensNoPlaybackBox()
    {
        var (surface, model, player) = Headset(PanelMode.Mini);
        using var _ = surface;

        player.Play(Run, [.. Enumerable.Range(1, 6).Select(Line)], 0);
        model.StoryPlayback = true;
        Jobs();

        Assert.DoesNotContain(surface.Board.View.GetVisualDescendants(), visual => visual is StoryPlaybackDialog);
        Assert.NotNull(player.Now);
    }
}
