using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Windowing;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Transcript's page bar lays out on first show, with no resize to set it right (#424).</summary>
public class TheSearchRowFitsOnFirstShowTests
{
    private static Rect InWindow(Window window, Control control) =>
        new(control.TranslatePoint(default, window)!.Value, control.Bounds.Size);

    /// <summary>
    /// Wrapped in the zoom host, as the desktop window is: its first layout measures the panel at infinite
    /// width and then at the window's, and it was the second pass that left the row drawn at its old size.
    /// </summary>
    [AvaloniaFact]
    public void NoActionOverlapsTheSearchFieldAtAnyWidth()
    {
        using var look = AppLook.Put();
        var overlaps = new List<string>();

        for (var width = 480; width <= 1600; width += 20)
        {
            var panel = new PanelView { DataContext = new PanelViewModel() };
            var window = new Window { Content = panel, Width = width, Height = 600 };

            panel.EnableDonation(() => { });
            panel.EnableSearch();
            ZoomHost.Attach(window, TestSurface.Settings());
            window.Show();
            Dispatcher.UIThread.RunJobs();

            if (width == 1000)
            {
                window.CaptureRenderedFrame()!.Save(
                    Path.Combine(TestSurface.CaptureDirectory, "search-row-first-show.png"),
                    new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }

            var bar = InWindow(window, panel.GetControl<DockPanel>("PageBar"));
            var field = InWindow(window, panel.GetControl<TextBox>("SearchInput"));

            if (field.Right > bar.Right + 0.5)
            {
                overlaps.Add($"{width}: the field at {field} runs past the bar at {bar}");
            }

            foreach (var action in panel.GetControl<DockPanel>("SearchRow").Children)
            {
                if (action.Name != "SearchInput" && action.IsVisible && InWindow(window, action).Intersects(field))
                {
                    overlaps.Add($"{width}: {action.Name} at {InWindow(window, action)} overlaps the field at {field}");
                }
            }

            window.Close();
        }

        Assert.True(overlaps.Count == 0, string.Join(Environment.NewLine, overlaps));
    }
}
