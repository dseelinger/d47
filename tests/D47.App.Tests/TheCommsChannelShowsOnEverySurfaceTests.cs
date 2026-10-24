using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The comms channel and who is on it show on the main window, the desktop overlay and both headset panels, on every tab (#1602).</summary>
public class TheCommsChannelShowsOnEverySurfaceTests
{
    private sealed record Surface(string Name, Window Window, PanelView Panel)
    {
        public CommsIndicator Indicator => Panel.GetControl<CommsIndicator>(Panel.Mode == PanelMode.Mini ? "CommsMini" : "CommsFull");

        public string Text => string.Join(" ", Indicator.GetVisualDescendants().OfType<TextBlock>().Select(part => part.Text));
    }

    private static Surface Open(PanelViewModel model, string name, PanelMode mode)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(TestSurface.Settings().Current.Ui.Theme);

        var panel = new PanelView { DataContext = model, Mode = mode };
        var window = mode == PanelMode.Mini
            ? new Window { Content = panel, Width = 792, Height = 280 }
            : new Window { Content = panel, Width = 1280, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return new Surface(name, window, panel);
    }

    private static CommsChannels Carrier()
    {
        var channels = new CommsChannels();
        channels.Update(new CommsInputs { CarrierOwned = true });
        return channels;
    }

    private static void Capture(Surface surface, string name)
    {
        using var frame = surface.Window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    [AvaloniaFact]
    public void ASelectionChangeUpdatesAllFourSurfaces()
    {
        var model = new PanelViewModel();
        var channels = Carrier();
        model.Comms = CommsIndicatorState.Of(channels, null);

        Surface[] surfaces =
        [
            Open(model, "window", PanelMode.Full),
            Open(model, "overlay", PanelMode.Mini),
            Open(model, "headset-full", PanelMode.Full),
            Open(model, "headset-mini", PanelMode.Mini),
        ];

        Assert.All(surfaces, surface => Assert.Equal("COMMS CARRIER · CAPTAIN", surface.Text));

        channels.NextSubChannel();
        model.Comms = CommsIndicatorState.Of(channels, null);
        Dispatcher.UIThread.RunJobs();

        Assert.All(surfaces, surface =>
        {
            Assert.True(surface.Indicator.IsVisible, surface.Name);
            Assert.Equal("COMMS CARRIER · TOWER", surface.Text);
        });

        foreach (var surface in surfaces)
        {
            surface.Window.Close();
        }
    }

    [AvaloniaFact]
    public void TheIndicatorShowsInFullAndMiniOnTwoTabs()
    {
        var model = new PanelViewModel();
        model.Comms = CommsIndicatorState.Of(Carrier(), null);

        var full = Open(model, "full", PanelMode.Full);
        full.Panel.EnableStanding(() => null);
        var mini = Open(model, "mini", PanelMode.Mini);
        Dispatcher.UIThread.RunJobs();

        foreach (var tab in new[] { PanelTab.Transcript, PanelTab.Commander })
        {
            full.Panel.Tab = tab;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(tab, full.Panel.Tab);
            Assert.True(full.Indicator.IsVisible);
            Assert.False(full.Indicator.IsHitTestVisible);
            Assert.True(full.Indicator.Bounds.Width > 0);
            Capture(full, $"comms-indicator-full-{tab}.png".ToLowerInvariant());
        }

        Assert.True(mini.Indicator.IsVisible);
        Assert.True(mini.Indicator.Bounds.Width > 0);
        Capture(mini, "comms-indicator-mini-transcript.png");

        full.Window.Close();
        mini.Window.Close();
    }

    [AvaloniaFact]
    public void TheStatesReadAsTheDesignDrawsThem()
    {
        var none = new CommsChannels();
        Assert.Equal(CommsIndicatorState.NoChannel, CommsIndicatorState.Of(none, null));

        var channels = new CommsChannels();
        channels.Update(new CommsInputs { Target = new TargetedShip("Python") });

        var unscanned = CommsIndicatorState.Of(channels, new TargetedShip("Python"));
        Assert.Equal(new CommsIndicatorState("TARGET", "PYTHON", "UNSCANNED"), unscanned);

        var scanned = new TargetedShip("Python", "Mara Kessin");
        channels.Update(new CommsInputs { Target = scanned });
        Assert.Equal(new CommsIndicatorState("TARGET", "MARA KESSIN"), CommsIndicatorState.Of(channels, scanned));

        channels.Update(new CommsInputs { Target = scanned, StoryRunning = true });
        channels.Select(CommsChannel.Story);
        var noOne = CommsIndicatorState.Of(channels, scanned);
        Assert.True(noOne.IsNoOne);
    }
}
