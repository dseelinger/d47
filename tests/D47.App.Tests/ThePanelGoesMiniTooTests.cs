using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The headset's mini panel, which <see cref="PanelView.Mode"/> still switches.</summary>
public class ThePanelGoesMiniTooTests
{
    /// <summary>A hole that was already open.</summary>
    [AvaloniaFact]
    public void MiniLeavesATabItHasNoReadingOfAndPutsItBack()
    {
        var (window, panel) = Open();

        panel.EnableSettings(() => new TextBlock { Text = "settings" });
        panel.Tab = PanelTab.Settings;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Settings, panel.Tab);
        Assert.True(panel.GetControl<Border>("PagePane").IsVisible);

        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        // Not on Settings, and not on a blank pane either: it is reading the transcript, which is the reading
        // mini actually has.
        Assert.Equal(PanelTab.Transcript, panel.Tab);
        Assert.False(panel.GetControl<Border>("PagePane").IsVisible);
        Assert.True(panel.GetControl<Border>("TranscriptPane").IsVisible);

        panel.Mode = PanelMode.Full;
        Dispatcher.UIThread.RunJobs();

        // And coming back restores the tab that was showing, rather than leaving the Commander on the
        // transcript wondering where their page went.
        Assert.Equal(PanelTab.Settings, panel.Tab);

        window.Close();
    }

    /// <summary>
    /// Mini keeps the tab it is on, and the story gets its own short reading rather than the full page
    /// — which is what <c>MiniPane</c> is, and is why "mini shows this tab" and "mini shows this tab
    /// the same way" are two different questions.
    /// </summary>
    [AvaloniaFact]
    public void MiniStaysOnAdventuresWhereAHostFurnishedTheShortReading()
    {
        var (window, panel) = Open();

        panel.EnableAdventures(AdventureFixture.Surface());
        panel.Tab = PanelTab.Adventures;
        Dispatcher.UIThread.RunJobs();

        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Adventures, panel.Tab);
        Assert.True(panel.GetControl<Border>("MiniPane").IsVisible);
        Assert.False(panel.GetControl<Border>("TranscriptPane").IsVisible);

        window.Close();
    }

    /// <summary>And while in mini, a move to Settings is declined — whatever moved the navigator.</summary>
    [AvaloniaFact]
    public void AMoveToATabMiniLacksIsDeclinedWhileMini()
    {
        var (window, panel) = Open();

        panel.EnableSettings(() => new TextBlock { Text = "settings" });
        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        panel.Nav.Select(PanelTab.Settings);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Transcript, panel.Tab);

        // And leaving mini does not then jump to a tab the Commander never chose: the bounce is not the same
        // as having been there.
        panel.Mode = PanelMode.Full;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Transcript, panel.Tab);

        window.Close();
    }

    /// <summary>
    /// The provenance line and the microphone indicator are unchanged: they are what mini already
    /// showed, and this phase adds the ask line rather than rearranging the rest.
    /// </summary>
    [AvaloniaFact]
    public void EverythingElseMiniShowsIsUnchanged()
    {
        var (window, panel) = Open();

        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        Assert.True(panel.GetControl<DockPanel>("StatusRow").IsVisible);
        Assert.False(panel.GetControl<DockPanel>("TabStrip").IsVisible);
        Assert.False(panel.GetControl<DockPanel>("Header").IsVisible);

        window.Close();
    }

    /// <summary>
    /// The panel in mini, drawing the transcript's tail — and drawing something different when a line
    /// arrives, which is the assertion a "did it render" check would pass while frozen.
    /// </summary>
    [AvaloniaFact]
    public void TheWindowInMiniDrawsTheTail()
    {
        var (window, panel) = Open();

        panel.Mode = PanelMode.Mini;

        window.Width = PanelResolution.Mini.Width;
        window.Height = PanelResolution.Mini.Height;
        Dispatcher.UIThread.RunJobs();

        var model = (PanelViewModel)panel.DataContext!;

        model.Append("Fixture One, docked. Fuel at 82 percent.\n");
        model.Append("\n> how far to Shinrarta\n");
        model.Append("Eleven jumps, and you are carrying more than the scoop likes.");
        Dispatcher.UIThread.RunJobs();

        var before = Frame(window);

        model.Append("\nStill talking, in a window this size.");
        var after = Frame(window);

        Assert.NotEmpty(after);
        Assert.NotEqual(before, after);

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "window-mini.png"),
            new PngBitmapEncoderOptions());

        window.Close();
    }

    private static (Window Window, PanelView Panel) Open()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = 900, Height = 640 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static byte[] Frame(Window window)
    {
        Dispatcher.UIThread.RunJobs();

        using var stream = new MemoryStream();

        window.CaptureRenderedFrame()!.Save(stream, new PngBitmapEncoderOptions());

        return stream.ToArray();
    }
}
