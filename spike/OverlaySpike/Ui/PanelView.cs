using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace OverlaySpike.Ui;

/// Stand-in for the d47 panel: a coloured rectangle with a ticking counter and a
/// sweeping bar, so a live surface is distinguishable from a one-shot blit.
/// Deliberately code-behind - this is throwaway.
sealed class PanelView : Border
{
    readonly TextBlock _counter, _clock, _fps;
    readonly Rectangle _sweep;
    readonly Canvas _sweepHost;
    public int Frame;

    public PanelView(int w, int h)
    {
        Width = w; Height = h;
        Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x16, 0x24));
        BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x1A));
        BorderThickness = new Thickness(4);
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(24);

        var mono = new FontFamily("Consolas, Courier New, monospace");
        var amber = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x40));
        var dim = new SolidColorBrush(Color.FromRgb(0x7A, 0x9C, 0xC6));

        _counter = new TextBlock { FontFamily = mono, FontSize = 96, Foreground = amber, FontWeight = FontWeight.Bold };
        _clock = new TextBlock { FontFamily = mono, FontSize = 28, Foreground = dim };
        _fps = new TextBlock { FontFamily = mono, FontSize = 22, Foreground = dim };
        _sweep = new Rectangle { Height = 26, Width = 120, Fill = amber, RadiusX = 6, RadiusY = 6 };
        _sweepHost = new Canvas { Height = 26, Margin = new Thickness(0, 18, 0, 0) };
        _sweepHost.Children.Add(_sweep);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = "DIRECTIVE 47", FontFamily = mono, FontSize = 34,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0xE2, 0xFF)),
            FontWeight = FontWeight.Bold, LetterSpacing = 6,
        });
        stack.Children.Add(new TextBlock { Text = "avalonia -> d3d11 shared texture -> IVROverlay", FontFamily = mono, FontSize = 20, Foreground = dim });
        stack.Children.Add(_counter);
        stack.Children.Add(_clock);
        stack.Children.Add(_fps);
        stack.Children.Add(_sweepHost);
        Child = stack;

        Measure(new Size(w, h));
        Arrange(new Rect(0, 0, w, h));
    }

    /// Called once per frame from the tick loop. This is the only mutation - no
    /// animation clock, no dispatcher timer, matching how d47 drives its panel.
    public void Tick(double seconds, double fps)
    {
        Frame++;
        _counter.Text = Frame.ToString("D6");
        _clock.Text = $"t = {seconds,7:F2} s";
        _fps.Text = $"{fps,6:F1} fps submitted";
        var travel = Math.Max(0, Bounds.Width - 180);
        Canvas.SetLeft(_sweep, (Math.Sin(seconds * 1.6) * 0.5 + 0.5) * travel);

        // Layout is only invalidated by what actually changed.
        Measure(new Size(Width, Height));
        Arrange(new Rect(0, 0, Width, Height));
    }
}
