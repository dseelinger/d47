using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>A capture of the one thing d47 draws over the panel for a headset, for a person to look at.
/// A segment or stepper press changes the value directly and draws no overlay (#274).</summary>
public class VrOverlayCaptureTests
{
    private static readonly PixelSize Quad = new(1024, 640);

    [AvaloniaFact]
    public void TheKeyboardRendersToACapture()
    {
        var model = new PanelViewModel();
        model.Append("Fixture One, docked. Cleared to depart.\n");

        var view = new PanelView { DataContext = model };
        using var surface = new OffscreenSurface(view, Quad);

        surface.Render(view.KeepUp);

        surface.Type(new TextBox { Text = "deciat" });
        surface.Render();

        surface.Render().Save(
            Path.Combine(TestSurface.CaptureDirectory, "vr-keyboard.png"),
            new PngBitmapEncoderOptions());
    }
}
