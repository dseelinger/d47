using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A hovered glyph button with its open label, beside one at rest.</summary>
public class AGlyphButtonIsCapturedWithItsLabelTests
{
    [AvaloniaFact]
    public void AHoveredResetButtonIsCaptured()
    {
        using var kit = AppLook.ControlKit();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var hovered = Glyph("Reset ship voices");
        var resting = Glyph("Reset ship voices");

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

        using var frame = window.CaptureRenderedFrame()!;
        var path = Path.Combine(TestSurface.CaptureDirectory, "glyph-button-hovered.png");
        frame.Save(path, new PngBitmapEncoderOptions());
        Assert.True(File.Exists(path));

        window.Close();
    }

    private static Button Glyph(string name)
    {
        var button = new Button
        {
            Theme = (ControlTheme)Application.Current!.FindResource("D47.GlyphButton")!,
            Content = Glyphs.Text(Glyphs.ResetText, TypeScale.Glyph),
        };

        AutomationProperties.SetName(button, name);
        return button;
    }
}
