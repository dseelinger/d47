using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// What the Transcript page's help actually looks like, captured (asked for 2026-08-23).
/// <para>
/// A band parses and still draws badly: a figure whose labels collide, a section taller than the
/// panel, a diagram that comes out empty because a headless render has none of the application's
/// resources. Nothing in the parse tests can see any of that, so this renders the page the
/// Commander presses through to and leaves the frame beside the other captures.
/// </para>
/// </summary>
public class TranscriptHelpCaptureTests
{
    [AvaloniaFact]
    public void TheBandDrawsItsFourStepsAndThreeCards()
    {
        var view = new PanelView { DataContext = new PanelViewModel() };
        view.EnableSettings(() => new TextBlock { Text = "settings" }, _ => { });

        var window = new Window { Content = view, Width = 1180, Height = 900 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var mark = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "HelpButton");
        mark.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var shown = view.GetVisualDescendants().OfType<TextBlock>()
            .Select(text => text.Text ?? string.Empty)
            .ToList();

        // The lede, the four headings and the three cards — which together are the whole claim
        // that this page is about the page rather than about the language model.
        Assert.Contains(shown, text => text.StartsWith("The page you land on", StringComparison.Ordinal));
        Assert.Contains("Three readings in the drop-down, and a fourth behind a switch.", shown);
        Assert.Contains("Two ways in, and the microphone always says which.", shown);
        Assert.Contains("Finding a line again, and taking it with you.", shown);
        Assert.Contains("Three settings stand behind every answer on this page.", shown);
        Assert.Contains("Listening", shown);
        Assert.Contains("Language model", shown);
        Assert.Contains("Speech", shown);

        // Every figure measured to something, rather than collapsing to nothing on a surface with
        // no resources — the failure mode a parse test cannot see.
        var figures = view.GetVisualDescendants().OfType<HelpFigureView>().ToList();

        Assert.Equal(4, figures.Count);
        Assert.All(figures, figure => Assert.True(
            figure.Bounds.Width > 100 && figure.Bounds.Height > 40,
            $"a figure measured to {figure.Bounds.Width}x{figure.Bounds.Height}"));

        Dispatcher.UIThread.RunJobs();

        // And the long-form link is the address this page actually has. A general page is not
        // under capabilities/, and a card pointing there is a 404 at the foot of every band.
        Assert.Contains("https://dseelinger.github.io/d47/transcript.html", shown);

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "help-transcript.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }
}
