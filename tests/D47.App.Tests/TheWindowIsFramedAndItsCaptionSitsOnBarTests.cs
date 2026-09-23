using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>The caption strip is on <c>bar</c> over a <c>line2</c> rule (#392).</summary>
public class TheWindowIsFramedAndItsCaptionSitsOnBarTests
{
    private static Color Token(string key) => ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    [AvaloniaFact]
    public void TheStripsGroundIsBarAndItsRuleIsLine2()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite);

        var window = new Window { Content = new TextBlock(), Width = 400, Height = 300 };
        CaptionStrip.Apply(window);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var strip = (Grid)window.GetVisualDescendants().Single(c => c.Name == "CaptionStrip");
        var rule = strip.Children.OfType<Border>().Single(b => b.Height == 1);

        Assert.Equal(Token(ThemeManager.BarKey), ((ISolidColorBrush)strip.Background!).Color);
        Assert.Equal(Token(ThemeManager.Line2Key), ((ISolidColorBrush)rule.Background!).Color);

        window.Close();
    }

    [AvaloniaFact]
    public void TheMainWindowAndADialogAreCaptured()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite);

        var main = new MainWindow(host: null) { Width = AppLook.CaptureWidth, Height = AppLook.CaptureHeight };
        main.Show();
        Dispatcher.UIThread.RunJobs();
        Save(main, "window-frame-main.png");

        var dialog = new ConfirmWindow("Forget this ship?", "Its loadout is removed from the logbook.", "Forget", "Keep")
        {
            Width = 560,
            Height = 280,
        };
        CaptionStrip.Apply(dialog);
        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        Save(dialog, "window-frame-dialog.png");

        dialog.Close();
        main.Close();
    }

    private static void Save(Window window, string fileName)
    {
        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(TestSurface.CaptureDirectory, fileName), new PngBitmapEncoderOptions());
    }
}
