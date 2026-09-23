using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The stat tile, the gauge and the modal dialog layout (#397).</summary>
public class TilesGaugesAndDialogsShareOneLookTests
{
    private sealed class StoppedClock : IWallClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
    }

    private static Color Resource(string key) =>
        ((ISolidColorBrush)Application.Current!.FindResource(key)!).Color;

    private static Color Colour(IBrush? brush) => ((ISolidColorBrush)brush!).Color;

    private static Window Shown(Control content, double width, double height = 400)
    {
        var window = new Window { Content = content, Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void PressEscape(Window window)
    {
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static SpendWindow Spend()
    {
        var ledger = new SpendLedger(
            Path.Combine(TempFolders.Create("d47-modal-spend"), "spend.jsonl"), new StoppedClock(), NullLogger.Instance);
        var session = new SpendTracker(ledger);
        session.Record(new TurnCost(new LlmUsage(1_240, 380, 0, 18_400), 0.0231m, true), true, "anthropic", "claude-opus-5");

        return new SpendWindow(
            session.Last, session, new SpeechSpend(), ledger, TestSurface.Settings().Current, TimeZoneInfo.Utc);
    }

    [AvaloniaFact]
    public void AStatTileInksItsValueByWhatItNames()
    {
        using var look = AppLook.Put();

        foreach (var (ink, key) in new[]
                 {
                     (StatInk.Value, ThemeManager.AKey),
                     (StatInk.Name, ThemeManager.WhiteKey),
                     (StatInk.Here, ThemeManager.CyanKey),
                 })
        {
            var tile = StatTile.Build("System", "Diaguandri", ink);
            var window = Shown(tile, 300);

            var blocks = tile.GetVisualDescendants().OfType<TextBlock>().ToList();

            Assert.Equal("SYSTEM", blocks[0].Text);
            Assert.Equal(Resource(ThemeManager.GreyKey), Colour(blocks[0].Foreground));
            Assert.Equal(Resource(key), Colour(blocks[1].Foreground));
            Assert.Equal(Resource(ThemeManager.SlabKey), Colour(tile.Background));
            Assert.Equal(default, tile.BorderThickness);

            window.Close();
        }
    }

    [AvaloniaFact]
    public void TheStatGridDropsColumnsAsTheWidthFalls()
    {
        using var look = AppLook.Put();

        int ColumnsAt(double width)
        {
            var grid = StatTile.Grid([.. Enumerable.Range(0, 6).Select(i => (Control)StatTile.Build($"T{i}", "1"))]);
            var window = Shown(grid, width);
            var columns = grid.ColumnDefinitions.Count;
            Assert.Equal(StatTile.Gap, grid.ColumnSpacing);
            Assert.Equal(StatTile.Gap, grid.RowSpacing);
            window.Close();
            return columns;
        }

        Assert.Equal(4, ColumnsAt(1280));
        Assert.Equal(3, ColumnsAt(480));
        Assert.Equal(1, ColumnsAt(200));
    }

    [AvaloniaTheory]
    [InlineData(GaugeFill.Progress, ThemeManager.AKey)]
    [InlineData(GaugeFill.Capacity, ThemeManager.YellowKey)]
    [InlineData(GaugeFill.Over, ThemeManager.RedKey)]
    public void AGaugeFillsItsSixPixelTrackInItsKindsColour(GaugeFill kind, string fillKey)
    {
        using var look = AppLook.Put();

        var gauge = Gauge.Build("Cargo", "28 / 32 t", 0.5, kind);
        var window = Shown(gauge, 400);

        var track = gauge.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Height == Gauge.TrackHeight);
        var borders = track.Children.OfType<Border>().ToList();

        Assert.Equal(6, Gauge.TrackHeight);
        Assert.Equal(Resource(ThemeManager.TileKey), Colour(borders[0].Background));
        Assert.Equal(Resource(fillKey), Colour(borders[1].Background));
        Assert.Equal(200, borders[1].Bounds.Width, 0.5);

        var reading = gauge.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == "28 / 32 t");
        Assert.Equal(Resource(fillKey), Colour(reading.Foreground));
        Assert.Equal(400, reading.TranslatePoint(new Point(reading.Bounds.Width, 0), gauge)!.Value.X, 1);

        window.Close();
    }

    [Theory]
    [InlineData(LoadoutTone.Danger, GaugeFill.Over)]
    [InlineData(LoadoutTone.Warn, GaugeFill.Capacity)]
    [InlineData(LoadoutTone.Body, GaugeFill.Progress)]
    [InlineData(LoadoutTone.Good, GaugeFill.Progress)]
    [InlineData(LoadoutTone.Muted, GaugeFill.Progress)]
    public void ALoadoutToneWithNoMatchFillsInA(LoadoutTone tone, GaugeFill fill) =>
        Assert.Equal(fill, LoadoutPages.Fill(tone));

    [AvaloniaFact]
    public void EscClosesTheConfirmDialogAsANo()
    {
        using var look = AppLook.Put();

        var owner = Shown(new Border(), 800, 600);
        var dialog = new ConfirmWindow("Forget this ship?", "Its loadout is removed.", "Forget", "Keep");
        var answer = dialog.AskAsync(owner);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(dialog.GetVisualDescendants().OfType<DockPanel>().SingleOrDefault(panel => panel.Name == "Modal"));
        Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "Forget this ship?");

        PressEscape(dialog);

        Assert.True(answer.IsCompleted);
        Assert.False(answer.Result);

        owner.Close();
    }

    [AvaloniaFact]
    public void TheConfirmDialogStillAnswersYes()
    {
        using var look = AppLook.Put();

        var owner = Shown(new Border(), 800, 600);
        var dialog = new ConfirmWindow("Forget this ship?", "Its loadout is removed.", "Forget", "Keep");
        var answer = dialog.AskAsync(owner);
        Dispatcher.UIThread.RunJobs();

        var forget = dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Forget"));
        forget.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(answer.IsCompleted);
        Assert.True(answer.Result);

        owner.Close();
    }

    [AvaloniaFact]
    public void EscClosesTheSpendDialog()
    {
        using var look = AppLook.Put();

        var dialog = Spend();
        var closed = false;
        dialog.Closed += (_, _) => closed = true;
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(dialog.GetVisualDescendants().OfType<DockPanel>().SingleOrDefault(panel => panel.Name == "Modal"));

        var figure = dialog.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "SpendFigure");
        Assert.Equal(0.0231m.ToString("C4"), figure.Text);

        PressEscape(dialog);

        Assert.True(closed);
    }

    [AvaloniaFact]
    public void TheDialogsAreCaptured()
    {
        using var look = AppLook.Put();

        var confirm = new ConfirmWindow(
            "Forget this ship?", "Its loadout is removed from the logbook.", "Forget", "Keep");
        confirm.Show();
        Dispatcher.UIThread.RunJobs();
        Save(confirm, "modal-confirm.png");
        confirm.Close();

        var spend = Spend();
        spend.Height = 700;
        spend.Show();
        Dispatcher.UIThread.RunJobs();
        Save(spend, "modal-spend.png");
        spend.Close();
    }

#if DEBUG
    [AvaloniaTheory]
    [InlineData(1280)]
    [InlineData(512)]
    public void TheControlKitShowsTilesGaugesAndTheModal(double width)
    {
        using var look = AppLook.Put();

        var kit = new ControlKitWindow { Width = width, Height = 1000 };
        kit.Show();
        Dispatcher.UIThread.RunJobs();

        var grid = kit.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "KitStatGrid");
        Assert.Equal(width > 1000 ? 4 : 2, grid.ColumnDefinitions.Count);

        // Scrolled so the stat tiles' heading sits at the top of the capture.
        var scroller = kit.GetVisualDescendants().OfType<ScrollViewer>().First();
        var top = grid.TranslatePoint(default, (Visual)scroller.Content!)!.Value.Y;
        scroller.Offset = new Vector(0, Math.Max(0, top - 120));
        Dispatcher.UIThread.RunJobs();

        Save(kit, $"control-kit-tiles-gauges-modal-{width:0}.png");
        kit.Close();
    }
#endif

    private static void Save(Window window, string fileName)
    {
        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(TestSurface.CaptureDirectory, fileName), new PngBitmapEncoderOptions());
    }
}
