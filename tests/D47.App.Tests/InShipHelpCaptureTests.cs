using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>What the In Ship reading's help actually looks like, captured.</summary>
public class InShipHelpCaptureTests
{
    [AvaloniaFact]
    public void TheBandDrawsItsThreeSectionsAndThreeCards()
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

        // The intro, the three headings and the three cards — which together are the whole claim that this
        // page is about this reading rather than about the language model.
        Assert.Contains("The conversations you have with those in your ship.", shown);
        Assert.Contains("Reads like an SMS thread.", shown);
        Assert.Contains("Two ways to input your requests.", shown);
        Assert.Contains("Additional controls.", shown);
        Assert.Contains("Listening", shown);
        Assert.Contains("Language model", shown);
        Assert.Contains("Speech", shown);

        // Every figure measured to something, rather than collapsing to nothing on a surface with no
        // resources — the failure mode a parse test cannot see.
        var figures = view.GetVisualDescendants().OfType<HelpFigureView>().ToList();

        Assert.Equal(3, figures.Count);
        Assert.All(figures, figure => Assert.True(
            figure.Bounds.Width > 100 && figure.Bounds.Height > 40,
            $"a figure measured to {figure.Bounds.Width}x{figure.Bounds.Height}"));

        Dispatcher.UIThread.RunJobs();

        // And the long-form link is the address this page actually has.
        Assert.Contains("https://dseelinger.github.io/d47/in-ship.html", shown);

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "help-in-ship.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }
}
