using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Configuration;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The mini panel keeps the newest turn in view unless the Commander scrolled it. The first frame of a
/// filled panel scrolls to the end and the extent then grows; that offset change must not read as the
/// Commander leaving the end.
/// </summary>
public class TheMiniPanelKeepsFollowingTests
{
    private static PanelViewModel Filled()
    {
        var model = new PanelViewModel();

        for (var line = 0; line < 60; line++)
        {
            model.Append(
                $"Line {line} of the transcript, long enough to wrap once or twice.\n",
                voice: line % 2 == 0 ? TranscriptVoice.Commander : TranscriptVoice.Ship,
                sourceKey: $"k{line}");
        }

        return model;
    }

    private static bool Following(PanelView view) => view.Following;

    private static bool AtTheEnd(PanelView view)
    {
        var scroller = view.GetVisualDescendants().OfType<ScrollViewer>().First(found => found.Name == "TranscriptScroller");
        return scroller.Offset.Y >= scroller.Extent.Height - scroller.Viewport.Height - 5;
    }

    private static void Settle(OffscreenSurface surface, PanelView view)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            surface.Render(view.KeepUp);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void TurnsArriveInView(PanelView view, PanelViewModel model, Action settle)
    {
        for (var turn = 0; turn < 3; turn++)
        {
            Task.Run(() => model.Append($"Turn {turn} that arrived afterwards, long enough to wrap.\n", sourceKey: $"t{turn}")).Wait();
            settle();

            Assert.True(Following(view), $"turn {turn}: following is off");
            Assert.True(AtTheEnd(view), $"turn {turn}: out of view");
        }
    }

    [AvaloniaFact]
    public void AMiniPanelFilledBeforeItsFirstFrameIsFollowing()
    {
        var model = Filled();
        var view = new PanelView { DataContext = model, Mode = PanelMode.Mini };
        using var surface = new OffscreenSurface(view, new PixelSize(600, 400));

        Settle(surface, view);

        Assert.True(Following(view));
        TurnsArriveInView(view, model, () => Settle(surface, view));
    }

    [AvaloniaFact]
    public void LeavingATabInMiniAndComingBackKeepsFollowing()
    {
        var model = Filled();
        var view = new PanelView { DataContext = model, Mode = PanelMode.Mini };
        view.EnableAdventures(AdventureFixture.Surface());
        using var surface = new OffscreenSurface(view, new PixelSize(600, 400));

        Settle(surface, view);
        view.Tab = PanelTab.Stories;
        Assert.Equal(PanelTab.Stories, view.Tab);
        Settle(surface, view);
        view.Tab = PanelTab.Transcript;
        Settle(surface, view);

        TurnsArriveInView(view, model, () => Settle(surface, view));
    }

    [AvaloniaFact]
    public void HidingAndShowingTheOverlayKeepsFollowing()
    {
        var model = Filled();
        var view = new PanelView { DataContext = model, Mode = PanelMode.Mini };
        var window = new Window { Width = 420, Height = 300, Content = view };

        void Settle()
        {
            Dispatcher.UIThread.RunJobs();
            view.KeepUp();
            Dispatcher.UIThread.RunJobs();
        }

        window.Show();
        Settle();
        window.Hide();
        Settle();
        model.Append("Arrived while hidden.\n", sourceKey: "hidden");
        window.Show();
        Settle();

        TurnsArriveInView(view, model, Settle);
    }

    [AvaloniaTheory]
    [InlineData("full", "mini")]
    [InlineData("mini", "full")]
    public void SwitchingTheHeadsetPanelModeAndBackKeepsFollowing(string start, string via)
    {
        var (settings, _, _) = TestSurface.Create();
        settings.Apply(VrCapability.ModeKey, start, SettingsCaller.Panel);
        var model = Filled();

        using var panel = new VrPanelSurface(model, settings, _ => null);
        var view = (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(panel)!;

        void Serve()
        {
            var (width, height) = panel.Size;
            var buffer = new byte[width * height * 4];

            unsafe
            {
                fixed (byte* pixels = buffer)
                {
                    for (var frame = 0; frame < 3; frame++)
                    {
                        panel.Draw((IntPtr)pixels, width * 4);
                        Dispatcher.UIThread.RunJobs();
                    }
                }
            }
        }

        void SwitchTo(string mode)
        {
            settings.Apply(VrCapability.ModeKey, mode, SettingsCaller.Panel);
            panel.ApplyMode();
            panel.Configure();
            Serve();
        }

        Serve();
        SwitchTo(via);
        SwitchTo(start);

        TurnsArriveInView(view, model, Serve);
    }
}
