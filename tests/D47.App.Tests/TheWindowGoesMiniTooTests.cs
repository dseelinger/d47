using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Configuration;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The desktop window shows what the headset already can.</summary>
public class TheWindowGoesMiniTooTests
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

    /// <summary>A mini window you cannot type into is worse than the full window in every respect.</summary>
    [AvaloniaFact]
    public void TheAskLineStaysInMiniOnlyWhereAHostAskedForIt()
    {
        var (window, panel) = Open();

        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        Assert.False(panel.GetControl<Border>("AskRow").IsVisible);

        // Nothing furnished, so mini is the headset's floor exactly.
        Assert.Equal(0, panel.MiniExtraHeight(PanelResolution.Mini.Width));

        panel.EnableAskInMini();
        Dispatcher.UIThread.RunJobs();

        Assert.True(panel.GetControl<Border>("AskRow").IsVisible);

        // And it is measured rather than typed, which is what the window's mini height is built out of: the
        // headset's floor plus whatever these rows actually want.
        var withAsk = panel.MiniExtraHeight(PanelResolution.Mini.Width);

        Assert.True(
            withAsk > 0,
            "The ask line reported no height, so the mini window would be the headset's size with it clipped.");

        // And the way out costs mini nothing since #194, because in mini the transcript is what is showing
        // and the way out rides StatusRow rather than a row of its own.
        panel.EnableModeToggle(_ => { });
        Dispatcher.UIThread.RunJobs();

        Assert.False(
            panel.GetControl<DockPanel>("ModeRow").IsVisible,
            "Mini spent a row on the way out, which is the row #194 gave back to the transcript.");

        Assert.Equal(withAsk, panel.MiniExtraHeight(PanelResolution.Mini.Width));

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

        panel.EnableAskInMini();
        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        Assert.True(panel.GetControl<DockPanel>("StatusRow").IsVisible);
        Assert.False(panel.GetControl<DockPanel>("TabStrip").IsVisible);
        Assert.False(panel.GetControl<DockPanel>("Header").IsVisible);

        window.Close();
    }

    /// <summary>
    /// The phase's acceptance, exactly as it is worded: full to mini to full lands on the pixel it
    /// started on, twice running, with a restart in the middle.
    /// </summary>
    [AvaloniaFact]
    public void FullToMiniToFullLandsOnThePixelItStartedOn()
    {
        var (_, viewState, _) = TestSurface.Create();
        var mini = new Size(512, 320);

        var window = new Window { Width = 820, Height = 640 };
        var memory = WindowPlacementMemory.Attach(window, viewState);
        window.Show();

        window.Width = 900;
        window.Height = 700;
        window.Position = new PixelPoint(120, 80);
        Dispatcher.UIThread.RunJobs();

        var full = Rect(window);

        // Once.
        Toggle(memory, window, true, mini);
        Assert.Equal(512, window.Width);
        Toggle(memory, window, false, null);
        Assert.Equal(full, Rect(window));

        // Twice running.
        Toggle(memory, window, true, mini);
        Toggle(memory, window, false, null);
        Assert.Equal(full, Rect(window));

        // And with a restart in the middle, left in mini.
        Toggle(memory, window, true, mini);
        window.Close();

        var again = new Window { Width = 820, Height = 640 };
        var restored = WindowPlacementMemory.Attach(again, viewState, startMini: true, miniSize: mini);
        again.Show();

        Assert.Equal(512, again.Width);

        Toggle(restored, again, false, null);
        Assert.Equal(full, Rect(again));

        again.Close();
    }

    /// <summary>
    /// And the other half of the same record: a Commander who widens their mini window keeps it,
    /// because the mini rectangle is remembered separately rather than merely being kept out of the
    /// full one's way.
    /// </summary>
    [AvaloniaFact]
    public void AWidenedMiniWindowIsKept()
    {
        var (_, viewState, _) = TestSurface.Create();
        var mini = new Size(512, 320);

        var window = new Window { Width = 820, Height = 640 };
        var memory = WindowPlacementMemory.Attach(window, viewState);
        window.Show();

        Toggle(memory, window, true, mini);

        window.Width = 700;
        Dispatcher.UIThread.RunJobs();

        Toggle(memory, window, false, null);
        Toggle(memory, window, true, mini);

        Assert.Equal(700, window.Width);

        window.Close();

        Assert.Equal(700, viewState.Load().MainWindowMini?.Width);

        // And the full rectangle is still its own, which is the whole point of two records.
        Assert.NotEqual(700, viewState.Load().MainWindow?.Width);
    }

    /// <summary>
    /// The window in mini, drawing the transcript's tail — and drawing something different when a line
    /// arrives, which is the assertion a "did it render" check would pass while frozen.
    /// </summary>
    [AvaloniaFact]
    public void TheWindowInMiniDrawsTheTail()
    {
        var (window, panel) = Open();

        panel.EnableAskInMini();
        panel.EnableModeToggle(_ => { });
        panel.Mode = PanelMode.Mini;

        window.Width = PanelResolution.Mini.Width;
        window.Height = PanelResolution.Mini.Height + panel.MiniExtraHeight(PanelResolution.Mini.Width);
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

    /// <summary>A way out you can see.</summary>
    [AvaloniaFact]
    public void MiniDrawsAControlThatLeavesIt()
    {
        var (window, panel) = Open();

        var asked = new List<PanelMode>();

        // Absent until a host furnishes it, so the headset's mini and the click-through overlay do not draw a
        // button nobody there could press.
        Assert.False(panel.GetControl<DockPanel>("ModeRow").IsVisible);
        Assert.False(panel.GetControl<Button>("ModeToggleSeat").IsVisible);

        panel.EnableModeToggle(asked.Add);
        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        // Whichever one is drawing it, rather than a name: mini reads the transcript, so since #194 this is
        // the seat in StatusRow and no row is spent on it.
        var toggle = WayOut(panel);

 // A mark, not a word — so the assertion is on the name it answers to, which is
        // what a screen reader says and what the tooltip shows.
        Assert.Equal("Expand to the whole panel", Name(toggle));

        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([PanelMode.Full], asked);

        // And it reads the other way round in full, so one word never has to be got backwards.
        panel.Mode = PanelMode.Full;
        Dispatcher.UIThread.RunJobs();

        toggle = WayOut(panel);

        Assert.Equal("Shrink to the mini panel", Name(toggle));

        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([PanelMode.Full, PanelMode.Mini], asked);

        window.Close();
    }

    /// <summary>And it is reachable from mini's other reading too.</summary>
    [AvaloniaFact]
    public void TheWayOutSurvivesTheStoryAndAChooser()
    {
        var (window, panel) = Open();

        panel.EnableModeToggle(_ => { });
        panel.EnableAdventures(AdventureFixture.Surface());
        panel.Tab = PanelTab.Adventures;
        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Adventures, panel.Tab);
        Assert.False(panel.GetControl<DockPanel>("StatusRow").IsVisible);
        Assert.True(panel.GetControl<DockPanel>("ModeRow").IsVisible);

        panel.Nav.Take(new NavCrumb("chooser", "Pick one"));
        Dispatcher.UIThread.RunJobs();

        Assert.True(
            panel.GetControl<DockPanel>("ModeRow").IsVisible,
            "A chooser hid the only control that leaves mini, which is the state a Commander "
            + "cannot get out of.");

        window.Close();
    }

    /// <summary>Mini is off out of the box: this is a shape a Commander asks for.</summary>
    [Fact]
    public void TheWindowIsFullOutOfTheBox() => Assert.Equal("full", new D47Settings().Ui.Mode);

    private static string? Name(Control control) =>
        Avalonia.Automation.AutomationProperties.GetName(control);

    /// <summary>
 /// The way out that is actually on screen, whichever of the two is drawing it, and an
    /// assertion that it is exactly one.
    /// </summary>
    private static Button WayOut(PanelView panel)
    {
        var row = panel.GetControl<Button>("ModeToggle");
        var seat = panel.GetControl<Button>("ModeToggleSeat");
        var showing = new[] { row, seat }.Where(button => button.IsEffectivelyVisible).ToArray();

        Assert.True(
            showing.Length == 1,
            $"{showing.Length} ways out are drawn; there should be exactly one.");

        return showing[0];
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

    /// <summary>What <c>MainWindow.ApplyWindowMode</c> does, in the order it does it.</summary>
    private static void Toggle(WindowPlacementMemory memory, Window window, bool mini, Size? measured)
    {
        _ = window;

        // The rectangle before the content, because changing the content raises a resize of its own and the
        // sample that has to happen is a sample of the shape being left.
        memory.Resize(mini, measured);
        Dispatcher.UIThread.RunJobs();
    }

    private static (double Width, double Height, PixelPoint At) Rect(Window window) =>
        (window.Width, window.Height, window.Position);

    private static byte[] Frame(Window window)
    {
        Dispatcher.UIThread.RunJobs();

        using var stream = new MemoryStream();

        window.CaptureRenderedFrame()!.Save(stream, new PngBitmapEncoderOptions());

        return stream.ToArray();
    }
}
