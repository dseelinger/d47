using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>Pressing a Transcript speaker picture shows it full size over the window; Close or Escape puts it away.</summary>
[Trait("Category", "Integration")]
public sealed class ASpeakerPictureEnlargesWhenPressedTests
{
    private static (Window Window, PanelView Panel, SpeakerPictureFixture Fixture) Opened(string name, int size, double width = 1400, double height = 1000)
    {
        var fixture = new SpeakerPictureFixture(name);
        fixture.Ship("core.warden", Colors.OrangeRed, size);

        var (window, panel) = fixture.Open(width, height);
        fixture.Model.Append("Warden replying.", picture: "core.warden");
        Dispatcher.UIThread.RunJobs();

        return (window, panel, fixture);
    }

    private static IReadOnlyList<SpeakerPictureViewer> Viewers(Visual from) =>
        [.. OverlayLayer.GetOverlayLayer(from)!.Children.OfType<SpeakerPictureViewer>()];

    private static void PressPicture(Window window, PanelView panel)
    {
        var picture = SpeakerPictureFixture.Pictures(panel).Single();
        var at = picture.TranslatePoint(new Point(10, 10), window)!.Value;

        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void PressingATranscriptPictureShowsItOverTheWindow()
    {
        var (window, panel, _) = Opened("d47-enlarge-opens", 512);

        Assert.Empty(Viewers(panel));

        PressPicture(window, panel);

        Assert.Single(Viewers(panel));
    }

    [AvaloniaFact]
    public void EscapePutsTheEnlargedPictureAway()
    {
        var (window, panel, _) = Opened("d47-enlarge-escape", 512);
        PressPicture(window, panel);

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Viewers(panel));
    }

    [AvaloniaFact]
    public void ClosePutsTheEnlargedPictureAway()
    {
        var (window, panel, _) = Opened("d47-enlarge-close", 512);
        PressPicture(window, panel);

        var close = Viewers(panel).Single().GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ClosePicture");

        Assert.True(close.MinHeight >= 44);

        close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Viewers(panel));
    }

    [AvaloniaFact]
    public void AFiveHundredAndTwelvePixelPictureIsDrawnAtFiveHundredAndTwelveInALargeWindow()
    {
        var (window, panel, _) = Opened("d47-enlarge-large", 512);
        PressPicture(window, panel);

        var shown = Viewers(panel).Single().GetVisualDescendants().OfType<Image>().Single(image => image.Name == "ViewedPicture");

        Assert.Equal(512, shown.Bounds.Width, 0.5);
        Assert.Equal(512, shown.Bounds.Height, 0.5);
    }

    [AvaloniaFact]
    public void AFiveHundredAndTwelvePixelPictureIsShrunkToFitASmallWindow()
    {
        var (window, panel, _) = Opened("d47-enlarge-small", 512, 400, 400);
        PressPicture(window, panel);

        var shown = Viewers(panel).Single().GetVisualDescendants().OfType<Image>().Single(image => image.Name == "ViewedPicture");

        Assert.True(shown.Bounds.Width < 400);
        Assert.True(shown.Bounds.Height < 400);
        Assert.Equal(shown.Bounds.Width, shown.Bounds.Height, 0.5);
    }

    [AvaloniaFact]
    public void ASmallPictureIsNeverEnlarged()
    {
        var (window, panel, _) = Opened("d47-enlarge-never", 64);
        PressPicture(window, panel);

        var shown = Viewers(panel).Single().GetVisualDescendants().OfType<Image>().Single(image => image.Name == "ViewedPicture");

        Assert.Equal(64, shown.Bounds.Width, 0.5);
    }
}
