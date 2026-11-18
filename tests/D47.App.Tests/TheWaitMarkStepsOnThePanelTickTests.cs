using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.App.Tests.UiWait;

namespace D47.App.Tests;

/// <summary>The wait mark moves only on the panel tick: one step every second beat, on the desktop and in the headset.</summary>
public class TheWaitMarkStepsOnThePanelTickTests
{
    private static object? Ink(string key) =>
        Application.Current!.TryFindResource(key, out var brush) ? brush : null;

    private static List<IBrush?> Cells(WaitingState mark) =>
        [.. mark.GetLogicalDescendants().OfType<Border>().Select(cell => cell.Background)];

    /// <summary>The lit cell's index, after checking the cell it left is Line and the rest are Tile.</summary>
    private static int Lit(WaitingState mark)
    {
        var cells = Cells(mark);

        Assert.Equal(WaitingState.Cells, cells.Count);

        var lit = cells.FindIndex(cell => ReferenceEquals(cell, Ink(ThemeManager.AKey)));
        var trail = (lit + WaitingState.Cells - 1) % WaitingState.Cells;

        Assert.Single(cells, cell => ReferenceEquals(cell, Ink(ThemeManager.AKey)));
        Assert.Same(Ink(ThemeManager.LineKey), cells[trail]);
        Assert.Equal(WaitingState.Cells - 2, cells.Count(cell => ReferenceEquals(cell, Ink(ThemeManager.TileKey))));

        return lit;
    }

    [AvaloniaFact]
    public void AShownMarkStepsOnEverySecondBeatAndTheTickSaysWhenItMoved()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var mark = new WaitingState { IsVisible = false };
        var root = new StackPanel { Children = { mark } };
        var window = new Window { Content = root, Width = 200, Height = 100 };
        window.Show();

        var ticker = new WaitTicker(root);

        Assert.False(ticker.Tick());
        Assert.False(ticker.Tick());

        mark.IsVisible = true;

        Assert.Equal(0, Lit(mark));
        Assert.True(ticker.Tick(), "the tick did not report the mark appearing");
        Assert.Equal(0, Lit(mark));

        for (var step = 1; step <= WaitingState.Cells; step++)
        {
            Assert.True(ticker.Tick());
            Assert.Equal(step % WaitingState.Cells, Lit(mark));
            Assert.False(ticker.Tick());
            Assert.Equal(step % WaitingState.Cells, Lit(mark));
        }

        mark.IsVisible = false;

        Assert.True(ticker.Tick(), "the tick did not report the mark going");
        Assert.False(ticker.Tick());

        mark.IsVisible = true;

        Assert.Equal(0, Lit(mark));

        window.Close();
    }

    [AvaloniaFact]
    public void AMarkOnAHiddenPageIsNotBeaten()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var mark = new WaitingState();
        var page = new StackPanel { Children = { mark }, IsVisible = false };
        var root = new StackPanel { Children = { page } };
        var window = new Window { Content = root, Width = 200, Height = 100 };
        window.Show();

        var ticker = new WaitTicker(root);

        Assert.False(ticker.Tick());
        Assert.False(ticker.Tick());
        Assert.Equal(0, Lit(mark));

        window.Close();
    }

    /// <summary>
    /// The headset draws the panel in a window that is never shown, and redraws only when a tick says something
    /// moved. The Log page's mark there has to be beaten, and each step has to change the frame.
    /// </summary>
    [AvaloniaFact]
    public void InTheHeadsetTheTickMovesTheMarkAndTheFrameShowsIt()
    {
        using var look = AppLook.Put();

        var release = new TaskCompletionSource();
        var model = new PanelViewModel
        {
            LogSource = () =>
            {
                release.Task.GetAwaiter().GetResult();
                return "one line";
            },
        };

        var panel = new PanelView { DataContext = model };
        panel.Classes.Add("headset");

        using var surface = new OffscreenSurface(panel, new PixelSize(1024, 640));

        surface.Render();
        Dispatcher.UIThread.RunJobs();

        Assert.False(panel.TickWaiting());

        panel.Page = TranscriptPage.Log;

        var mark = panel.GetControl<DockPanel>("ModePicker").Children.OfType<WaitingState>().Single();

        Until(() => mark.IsVisible);

        Assert.True(panel.TickWaiting(), "the headset was not told the mark appeared");

        var first = surface.Render().Encoded();
        surface.Render().SaveCapture("wait-mark-headset-first.png");

        Assert.True(panel.TickWaiting(), "the headset was not told the mark stepped");

        var second = surface.Render().Encoded();
        surface.Render().SaveCapture("wait-mark-headset-second.png");

        Assert.NotEqual(first, second);
        Assert.Equal(1, Lit(mark));

        Assert.False(panel.TickWaiting());

        release.SetResult();
        Until(() => !mark.IsVisible);

        Assert.True(panel.TickWaiting(), "the headset was not told the mark went");
        Assert.False(panel.TickWaiting());
    }
}
