using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Numbers, costs, times and dates are JetBrains Mono; names and words are not (#508).</summary>
public class NumbersAndTimesAreDrawnInMonoTests
{
    private const string Mono = "JetBrains Mono";
    private const string Saira = "Saira";

    private static readonly DateTimeOffset Now = new(2026, 9, 26, 20, 15, 0, TimeSpan.Zero);

    private static TextBlock Value(Border tile) => (TextBlock)((StackPanel)tile.Child!).Children[1];

    [AvaloniaFact]
    public void ANumberIsMonoAndASystemNameIsSairaBothInA()
    {
        var number = Value(StatTile.Build("Jump range", "500 ly", StatInk.Number));
        var system = Value(StatTile.Build("Workshop", "Diaguandri"));

        Assert.Equal(Mono, number.FontFamily.Name);
        Assert.Equal(Saira, system.FontFamily.Name);
        Assert.Equal(ThemeManager.AKey, StatTile.InkKey(StatInk.Number));
        Assert.Equal(ThemeManager.AKey, StatTile.InkKey(StatInk.Value));
    }

    [AvaloniaFact]
    public void TheUtilitiesClocksDrawTheirTimesAndDatesInMono()
    {
        using var look = AppLook.Put();

        var root = TempFolders.Create("d47-mono-clocks");
        var alarms = new AlarmStore(Path.Combine(root, "alarms.json"), NullLogger<AlarmStore>.Instance);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableUtilities(new Timekeeper(alarms), alarms, () => Now, () => TimeZoneInfo.Utc);

        var window = new Window
        {
            Content = panel,
            Width = 900,
            Height = 700,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();

        panel.Tab = PanelTab.Utilities;
        Dispatcher.UIThread.RunJobs();

        var clocks = panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => block.Text is "GALACTIC" or "LOCAL")
            .Select(caption => (StackPanel)caption.Parent!)
            .ToList();

        Assert.Equal(2, clocks.Count);

        foreach (var clock in clocks)
        {
            var time = (TextBlock)clock.Children[1];
            var date = (TextBlock)clock.Children[2];

            Assert.False(string.IsNullOrEmpty(time.Text));
            Assert.Equal(Mono, time.FontFamily.Name);
            Assert.Equal(Mono, date.FontFamily.Name);
        }

        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(TestSurface.CaptureDirectory, "mono-utilities-clocks.png"), new PngBitmapEncoderOptions());

        window.Close();
    }

    [AvaloniaFact]
    public void TheStatTilesAreCaptured()
    {
        using var look = AppLook.Put();

        var grid = StatTile.Grid(
        [
            StatTile.Build("Current system", "Meene", StatInk.Here),
            StatTile.Build("Workshop", "Diaguandri"),
            StatTile.Build("Jump range", "500 ly", StatInk.Number),
            StatTile.Build("Total", "1,180 t", StatInk.Number),
            StatTile.Build("Balance", "4,312,009,120 cr", StatInk.Number),
            StatTile.Build("Docking", "all"),
        ], maxColumns: 3);

        grid.Margin = new Thickness(24);

        var window = new Window
        {
            Content = grid,
            Width = 720,
            Height = 240,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        var path = Path.Combine(TestSurface.CaptureDirectory, "mono-stat-tiles.png");
        frame.Save(path, new PngBitmapEncoderOptions());
        Assert.True(File.Exists(path));

        window.Close();
    }
}
