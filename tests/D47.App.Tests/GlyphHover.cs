using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Theming;

namespace D47.App.Tests;

/// <summary>Hovers one of two glyph controls side by side and returns the window and the label popup.</summary>
internal static class GlyphHover
{
    public static (Window Window, Popup Label) Open(Control hovered, Control resting)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { hovered, resting },
        };

        var window = new Window
        {
            Content = row,
            Width = 480,
            Height = 100,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.MouseMove(hovered.TranslatePoint(new Point(22, 22), window)!.Value);
        Dispatcher.UIThread.RunJobs();

        var label = hovered.GetVisualDescendants().OfType<Popup>().Single(popup => popup.Name == "Label");
        return (window, label);
    }
}
