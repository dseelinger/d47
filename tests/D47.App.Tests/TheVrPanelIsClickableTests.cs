using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Pressing a control on the panel from inside the headset
/// (remediation.md, "Controls should be clickable in the VR panels").
/// <para>
/// The surface is rasterised out of a window that is never shown, so it receives no input from
/// the desktop at all. What reaches it is a fraction across a quad, from a controller — turned
/// into the routed events every control already understands, so a tab selects and a button
/// presses without either of them knowing where the press came from.
/// </para>
/// </summary>
public class TheVrPanelIsClickableTests
{
    private static readonly PixelSize Quad = new(1024, 640);

    /// <summary>The centre of a control, in the surface's own coordinates.</summary>
    private static Point Centre(Control view, Control target)
    {
        var at = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), view);
        Assert.NotNull(at);
        return at.Value;
    }

    private static Control Tab(PanelView view, string name) =>
        view.GetVisualDescendants().OfType<RadioButton>().First(tab => (tab.Content as string) == name);

    [AvaloniaFact]
    public void PressingATabChangesThePage()
    {
        var model = new PanelViewModel();
        var view = new PanelView { DataContext = model };
        using var surface = new OffscreenSurface(view, Quad);

        surface.Render();

        Assert.Equal(TranscriptPage.Conversation, view.Page);

        Assert.True(surface.Click(Centre(view, Tab(view, "Technical"))), "the press landed on something");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.Technical, view.Page);

        surface.Click(Centre(view, Tab(view, "Conversation")));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.Conversation, view.Page);
    }

    /// <summary>
    /// And the frame that comes back afterwards is the new page, which is the half a Commander
    /// in a headset can actually see.
    /// </summary>
    [AvaloniaFact]
    public void TheSurfaceRedrawsAsThePageChanges()
    {
        var model = new PanelViewModel();
        model.Append("The Commander and the ship's AI.");
        model.Append("[12:00:00] Microphone open, listening.\n", TranscriptKind.Technical);

        var view = new PanelView { DataContext = model };
        using var surface = new OffscreenSurface(view, Quad);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var before = Frame(surface);

        surface.Click(Centre(view, Tab(view, "Technical")));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(before, Frame(surface));
    }

    /// <summary>
    /// A press on nothing in particular is a press on nothing in particular. The panel is mostly
    /// transcript, and a ray landing on it should not do anything at all.
    /// </summary>
    [AvaloniaFact]
    public void PressingTheTranscriptChangesNothing()
    {
        var model = new PanelViewModel();
        model.Append("Fixture One, docked.");

        var view = new PanelView { DataContext = model };
        using var surface = new OffscreenSurface(view, Quad);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        surface.Click(new Point(Quad.Width / 2, Quad.Height / 2));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.Conversation, view.Page);
    }

    private static string Frame(OffscreenSurface surface)
    {
        var rendered = surface.Render();
        var size = rendered.PixelSize;
        var stride = size.Width * 4;
        var pixels = new byte[stride * size.Height];

        unsafe
        {
            fixed (byte* buffer = pixels)
            {
                rendered.CopyPixels(
                    new PixelRect(0, 0, size.Width, size.Height), (IntPtr)buffer, pixels.Length, stride);
            }
        }

        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels));
    }
}
