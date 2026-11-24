using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The mini panel's Music read-out draws its fill at its position of the rail it was last laid out at, so the
/// headset's two layout passes a frame at two widths leave it where the track is (#2744).
/// </summary>
public sealed class TheMiniPanelsMusicReadOutFillMatchesItsPositionOnTheHeadsetTests
{
    private static readonly TrackProgress Known = new(TimeSpan.FromSeconds(102), TimeSpan.FromSeconds(258));

    private static double Expected => Known.Elapsed / Known.Length!.Value;

    private static (Control Rail, Control Fill) Parts(AudioReadout readout)
    {
        var parts = readout.GetVisualDescendants().OfType<Avalonia.Controls.Panel>().First(p => p.Children.Count == 2 && p.Children.All(c => c is Border)).Children;

        return (parts[0], parts[1]);
    }

    private static void AssertTheFillIsThePosition(AudioReadout readout, string surface)
    {
        var (rail, fill) = Parts(readout);
        var at = Expected * rail.Bounds.Width;

        Assert.True(rail.Bounds.Width > 100, $"the {surface} rail is {rail.Bounds.Width} px");
        Assert.True(Math.Abs(fill.Bounds.Width - at) < 1, $"the {surface} fill is {fill.Bounds.Width} px of a {rail.Bounds.Width} px rail; {at:0.#} px is 1:42 of 4:18");
    }

    private static void Draw(VrPanelSurface surface)
    {
        surface.Configure();
        surface.ApplyMode();
        var (width, height) = surface.Size;
        var buffer = new byte[width * 4 * height];

        unsafe
        {
            fixed (byte* into = buffer)
            {
                surface.Draw((IntPtr)into, width * 4);
            }
        }

        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void AReadOutLaidOutAtOneWidthAndThenAnotherDrawsItsFillAtTheSecond()
    {
        var readout = new AudioReadout();
        readout.Update(AudioPlayerReadout.From(new MusicState("long-drift-04.mp3", true, false, false, "supercruise"), false));
        readout.Watch(() => Known);
        var window = new Window { Content = readout, Width = 1000, Height = 200 };
        window.Show();

        foreach (var width in new[] { 800.0, 400.0 })
        {
            readout.Measure(new Size(width, 200));
            readout.Arrange(new Rect(0, 0, width, 200));
        }

        var (rail, fill) = Parts(readout);

        Assert.True(rail.Bounds.Width > 100);
        Assert.True(Math.Abs(fill.Bounds.Width - (Expected * rail.Bounds.Width)) < 1, $"the fill is {fill.Bounds.Width} px of a {rail.Bounds.Width} px rail");

        window.Close();
    }

    [AvaloniaFact]
    [Trait("Category", "Integration")]
    public void TheHeadsetDrawsTheMiniPanelsMusicReadOutAtTheTracksPosition()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite);
        var (settings, _, _) = TestSurface.Create();
        settings.Replace(
            "test",
            current => current with
            {
                Vr = current.Vr with
                {
                    Mode = "mini",
                    Panel = current.Vr.Panel with { Pixels = "1600x1443", Zoom = 100 },
                    Mini = current.Vr.Mini with { Pixels = string.Empty, Zoom = 80 },
                },
            });

        var model = new PanelViewModel
        {
            AudioPlayer = AudioPlayerReadout.From(new MusicState("long-drift-04.mp3", true, false, false, "supercruise"), false),
            AudioProgress = () => Known,
        };
        var host = new MusicHost(settings, _ => string.Empty, () => { }, () => Known, _ => true);

        using var surface = new VrPanelSurface(model, settings, _ => null, music: host);
        Assert.True(surface.Nav.Show(MediaFurnishing.MusicRoot));
        Draw(surface);
        Draw(surface);

        var readout = surface.Board.View.GetVisualDescendants().OfType<AudioReadout>().Single();
        surface.Board.Render().SaveCapture("music-readout-headset-mini.png");
        AssertTheFillIsThePosition(readout, "mini panel");
    }
}
