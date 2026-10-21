using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class TheTitleBarShowsTheServerStateTests
{
    private static ThemeManager Themes() => new(Application.Current!, NullLogger<ThemeManager>.Instance);

    private static Window Strip(ServerStatusReadout readout)
    {
        var help = new Button { Content = "HELP" };
        var window = new Window { Content = new TextBlock(), Title = "Directive 47", Width = 640, Height = 120 };
        CaptionStrip.Apply(
            window,
            beforeButtons: new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Children = { readout, help },
            });
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static SolidColorBrush Brush(string key) => (SolidColorBrush)Application.Current!.Resources[key]!;

    private static TextBlock Word(Window window, string text) =>
        window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == text);

    [AvaloniaFact]
    public void TheReadoutIsHiddenUntilAStateIsSupplied()
    {
        using var kit = AppLook.ControlKit();
        Themes().Apply(ThemeCatalog.Elite);
        var readout = new ServerStatusReadout();
        var window = Strip(readout);

        Assert.False(readout.IsVisible);

        readout.Show(ServerState.Online);
        Assert.True(readout.IsVisible);

        readout.Show(null);
        Assert.False(readout.IsVisible);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(ServerState.Online, "ONLINE", ThemeManager.BlueKey)]
    [InlineData(ServerState.Maintenance, "MAINTENANCE", ThemeManager.WarnKey)]
    [InlineData(ServerState.Offline, "OFFLINE", ThemeManager.RedKey)]
    [InlineData(ServerState.Unknown, "UNKNOWN", ThemeManager.GreyKey)]
    public void EachStateDrawsItsWordInItsColour(ServerState state, string word, string key)
    {
        using var kit = AppLook.ControlKit();
        Themes().Apply(ThemeCatalog.Elite);
        var readout = new ServerStatusReadout();
        var window = Strip(readout);

        readout.Show(state);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Brush(key).Color, ((SolidColorBrush)Word(window, word).Foreground!).Color);
        Assert.Equal(Brush(ThemeManager.GreyKey).Color, ((SolidColorBrush)Word(window, "SERVERS").Foreground!).Color);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture($"title-bar-servers-{word.ToLowerInvariant()}.png");
        window.Close();
    }

    [AvaloniaFact]
    public void TheReadoutFollowsTheThemeWithNoRestart()
    {
        using var kit = AppLook.ControlKit();
        var themes = Themes();
        themes.Apply(ThemeCatalog.Elite);
        var readout = new ServerStatusReadout();
        var window = Strip(readout);
        readout.Show(ServerState.Offline);
        Dispatcher.UIThread.RunJobs();
        var elite = ((SolidColorBrush)Word(window, "OFFLINE").Foreground!).Color;

        themes.Apply(ThemeCatalog.Light);
        Dispatcher.UIThread.RunJobs();

        var light = ((SolidColorBrush)Word(window, "OFFLINE").Foreground!).Color;
        Assert.Equal(Brush(ThemeManager.RedKey).Color, light);
        Assert.NotEqual(elite, light);
        window.Close();
    }
}
