using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>Too narrow for the words on one row, every tab along the top shows its glyph instead (#806).</summary>
public class TabsTurnToGlyphsWhenNarrowTests
{
    private static (Window Window, PanelView Panel) Furnished(double width)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.Furnish(PanelTab.Stories, _ => new TextBlock(), new NavCrumb("stories", "Stories"));
        panel.Furnish(PanelTab.Commander, _ => new TextBlock(), new NavCrumb("checklist", "Checklist"));
        panel.Furnish(PanelTab.Assets, _ => new TextBlock(), new NavCrumb("fleet", "Ships"));
        panel.Furnish(PanelTab.Navigation, _ => new TextBlock(), new NavCrumb("plan", "Plan"));
        panel.EnableSettings(() => new TextBlock());

        // As the desktop window does: HELP and the badge leave the tab row for the title bar.
        panel.MoveChromeToTitleBar();

        var window = new Window { Content = panel, Width = width, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static List<RadioButton> Shown(PanelView panel) =>
        [.. panel.GetControl<WrapPanel>("Tabs").Children.OfType<RadioButton>().Where(t => t.IsVisible)];

    private static bool ShowsGlyph(RadioButton tab)
    {
        var glyph = tab.GetVisualDescendants().OfType<Path>().Single(p => p.Name == "Glyph");
        var word = tab.GetVisualDescendants().OfType<ContentPresenter>().Single(p => p.Name == "PART_ContentPresenter");

        Assert.NotEqual(glyph.IsVisible, word.IsVisible);
        Assert.Equal(glyph.IsVisible, tab.Classes.Contains(TabGlyph.Class));

        return glyph.IsVisible;
    }

    private static void Resize(Window window, double width)
    {
        window.Width = width;
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void AWideStripShowsTheWords()
    {
        var (_, panel) = Furnished(1280);

        Assert.Equal(6, Shown(panel).Count);
        Assert.All(Shown(panel), tab => Assert.False(ShowsGlyph(tab), $"{tab.Name} shows its glyph at 1280"));
    }

    [AvaloniaFact]
    public void ANarrowStripIsOneRowOfGlyphTiles()
    {
        var (_, panel) = Furnished(512);
        var tabs = Shown(panel);

        Assert.All(tabs, tab => Assert.True(ShowsGlyph(tab), $"{tab.Name} shows its word at 512"));
        Assert.Single(tabs.Select(t => t.Bounds.Y).Distinct());
        Assert.All(tabs, tab =>
        {
            Assert.Equal(TabGlyph.Tile, tab.Bounds.Width);
            Assert.Equal(TabGlyph.Tile, tab.Bounds.Height);
        });

        var glyph = tabs[0].GetVisualDescendants().OfType<Path>().Single(p => p.Name == "Glyph");

        Assert.Equal(TabGlyph.Size, glyph.Bounds.Width);
        Assert.Equal(1.6, glyph.StrokeThickness);
        Assert.Equal(PenLineJoin.Miter, glyph.StrokeJoin);
        Assert.Equal(PenLineCap.Square, glyph.StrokeLineCap);
    }

    /// <summary>Every width from narrow to wide, both ways: all tabs agree, and the words never return at a narrower width than they left.</summary>
    [AvaloniaFact]
    public void ResizingSwitchesEveryTabAtOnceAndHolds()
    {
        var (window, panel) = Furnished(512);
        var widths = Enumerable.Range(0, 81).Select(i => 512.0 + (i * 10)).ToList();

        foreach (var pass in new[] { widths, Enumerable.Reverse(widths).ToList() })
        {
            bool? threshold = null;
            var flips = 0;

            foreach (var width in pass)
            {
                Resize(window, width);

                var states = Shown(panel).Select(ShowsGlyph).Distinct().ToList();
                Assert.Single(states);

                // A second layout at the same width changes nothing.
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(states[0], ShowsGlyph(Shown(panel)[0]));

                if (threshold is { } was && was != states[0])
                {
                    flips++;
                }

                threshold = states[0];
            }

            Assert.Equal(1, flips);
        }
    }

    [AvaloniaFact]
    public void ATileSaysItsLabel()
    {
        var (window, panel) = Furnished(512);

        Assert.All(Shown(panel), tab =>
        {
            Assert.Equal(tab.Content, AutomationProperties.GetName(tab));
            Assert.Equal(tab.Content, ToolTip.GetTip(tab));
        });

        Resize(window, 1280);

        Assert.All(Shown(panel), tab => Assert.Equal(tab.Content, AutomationProperties.GetName(tab)));
    }

    [AvaloniaFact]
    public void EveryTabHasAGlyph()
    {
        Assert.All(Enum.GetValues<PanelTab>(), tab =>
        {
            var path = TabGlyph.PathFor(tab);

            Assert.False(string.IsNullOrEmpty(path), $"{tab} has no glyph");
            Assert.True(Geometry.Parse(path).Bounds.Width > 0, $"{tab}'s glyph draws nothing");
        });
    }

    [AvaloniaFact]
    public void TabsDownTheLeftKeepTheirWords()
    {
        var (_, panel) = Furnished(512);

        panel.SetTabsDownTheLeft(true);
        Dispatcher.UIThread.RunJobs();

        Assert.All(Shown(panel), tab => Assert.False(tab.Classes.Contains(TabGlyph.Class), $"{tab.Name} is a glyph in the rail"));
    }

    [AvaloniaFact]
    public void TheGlyphRowIsCaptured()
    {
        using var look = AppLook.Put();

        var (window, panel) = Furnished(512);

        panel.Tab = PanelTab.Commander;
        Dispatcher.UIThread.RunJobs();

        var path = System.IO.Path.Combine(TestSurface.CaptureDirectory, "tab-glyphs-512.png");

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.Save(path, new PngBitmapEncoderOptions());
        }

        Resize(window, 1280);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.Save(System.IO.Path.Combine(TestSurface.CaptureDirectory, "tab-words-1280.png"), new PngBitmapEncoderOptions());
        }

        Assert.True(File.Exists(path));
    }
}
