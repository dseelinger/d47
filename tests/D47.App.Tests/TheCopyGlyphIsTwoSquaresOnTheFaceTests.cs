using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The copy control is a glyph button whose face is two overlapping 10px squares with 2px outlines,
/// the front one filled with the face ground: Tile at rest, A on hover (kit.css .d47-copy-icon).
/// </summary>
public class TheCopyGlyphIsTwoSquaresOnTheFaceTests
{
    private static Color Resource(string key) =>
        ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    private static Color Of(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static (Border Back, Border Front) Squares(Button button)
    {
        var face = Assert.IsType<Canvas>(button.Content);
        Assert.Equal(CopyGlyph.Size, face.Width);
        Assert.Equal(CopyGlyph.Size, face.Height);
        Assert.Equal(2, face.Children.Count);

        return (Assert.IsType<Border>(face.Children[0]), Assert.IsType<Border>(face.Children[1]));
    }

    [AvaloniaFact]
    public void TheFrontSquareTakesTheFaceGroundAtRestAndOnHover()
    {
        using var kit = AppLook.ControlKit();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var copy = CopyGlyph.For("Shinrarta Dezhra", _ => Task.FromResult(true));

        var window = new Window { Content = copy, Width = 200, Height = 100 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Same(Application.Current!.FindResource("D47.GlyphButton"), copy.Theme);
        Assert.Equal(CopyGlyph.Name, AutomationProperties.GetName(copy));

        var (back, front) = Squares(copy);

        foreach (var square in new[] { back, front })
        {
            Assert.Equal(CopyGlyph.Square, square.Width);
            Assert.Equal(CopyGlyph.Square, square.Height);
            Assert.Equal(new Thickness(CopyGlyph.Stroke), square.BorderThickness);
            Assert.Equal(Resource(ThemeManager.AKey), Of(square.BorderBrush));
        }

        Assert.Equal(CopyGlyph.Size - CopyGlyph.Square, Canvas.GetLeft(front));
        Assert.Equal(CopyGlyph.Size - CopyGlyph.Square, Canvas.GetTop(front));
        Assert.Null(back.Background);
        Assert.Equal(Resource(ThemeManager.TileKey), Of(front.Background));

        window.MouseMove(copy.TranslatePoint(new Point(22, 22), window)!.Value);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Resource(ThemeManager.AKey), Of(front.Background));
        Assert.Equal(Resource(ThemeManager.KnockKey), Of(front.BorderBrush));
        Assert.Equal(Resource(ThemeManager.KnockKey), Of(back.BorderBrush));

        window.Close();
    }

    /// <summary>A hovered copy glyph beside one at rest, captured with its label reading Copy and then Copied.</summary>
    [AvaloniaFact]
    public void TheCopyGlyphIsCapturedHoveredBeforeAndAfterACopy()
    {
        using var kit = AppLook.ControlKit();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var hovered = CopyGlyph.For("Shinrarta Dezhra", _ => Task.FromResult(true));
        var resting = CopyGlyph.For("Shinrarta Dezhra", _ => Task.FromResult(true));

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
        Assert.True(label.IsOpen);

        Save(window, "copy-glyph-hovered.png");

        CopyGlyph.Show(hovered, worked: true);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(CopyGlyph.Copied, AutomationProperties.GetName(hovered));
        Save(window, "copy-glyph-copied.png");

        window.Close();
    }

    /// <summary>The Transcript bar's page copy is the same glyph, captured beside the search field.</summary>
    [AvaloniaFact]
    public void TheTranscriptBarsCopyIsTheGlyph()
    {
        using var kit = AppLook.ControlKit();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var model = new D47.App.Panel.PanelViewModel();
        model.Append("Deciat is two jumps out.");

        var panel = new D47.App.Panel.PanelView { DataContext = model };
        panel.EnableSearch();
        panel.EnableCopy(new D47.Core.Capabilities.Builtin.RecordingClipboard());

        var window = new Window
        {
            Content = panel,
            Width = 900,
            Height = 300,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var copy = panel.GetControl<Button>("CopyButton");

        Assert.True(copy.IsVisible);
        Assert.Equal(CopyGlyph.Name, AutomationProperties.GetName(copy));
        Squares(copy);

        Save(window, "copy-glyph-transcript-bar.png");

        window.Close();
    }

    private static void Save(Window window, string name)
    {
        using var frame = window.CaptureRenderedFrame()!;
        var path = name;
        frame.SaveCapture(path);
    }
}
