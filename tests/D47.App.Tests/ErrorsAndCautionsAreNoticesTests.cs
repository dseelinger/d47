using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A failure is a red notice and a caution an amber one: a 3px bar in the level colour on a 12% ground of
/// it, with the warn colours taken from the design's palette.
/// </summary>
public partial class ErrorsAndCautionsAreNoticesTests
{
    private static ThemeManager Manager() => new(Application.Current!, NullLogger<ThemeManager>.Instance);

    private static Color Published(string key) => ((SolidColorBrush)Application.Current!.Resources[key]!).Color;

    private static Color Ink(IBrush? brush) => ((ISolidColorBrush)brush!).Color;

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No d47.slnx above the test binary.");
    }

    [GeneratedRegex(@"\[data-d47-theme='(?<theme>[a-z-]+)'\]\s*\{(?<body>[^}]*)\}")]
    private static partial Regex ThemeBlock();

    [GeneratedRegex(@"--d47-warn:\s*(?<hex>#[0-9A-Fa-f]{6});")]
    private static partial Regex WarnToken();

    /// <summary>The design's warn value for each theme id, read from its palette.</summary>
    private static Dictionary<string, Color> DesignWarns()
    {
        var css = File.ReadAllText(Path.Combine(RepositoryRoot(), "design", "system", "tokens", "palette.css"));

        return ThemeBlock().Matches(css)
            .Select(block => (Theme: block.Groups["theme"].Value, Warn: WarnToken().Match(block.Groups["body"].Value)))
            .Where(found => found.Warn.Success)
            .ToDictionary(found => found.Theme, found => Color.Parse(found.Warn.Groups["hex"].Value));
    }

    [Trait("Category", "Gate")]
    [Fact]
    public void EachThemesWarnIsTheDesignsWarn()
    {
        var warns = DesignWarns();

        Assert.Equal(4, warns.Count);
        Assert.Equal(warns["elite"], Palettes.Elite.Warn);
        Assert.Equal(warns["dark"], Palettes.Dark.Warn);
        Assert.Equal(warns["light"], Palettes.Light.Warn);

        // Match my Elite colours is Elite's table before the HUD matrix moves it.
        Assert.Equal(warns[ThemeCatalog.ElitePaletteId], Palettes.For(ThemeCatalog.ElitePaletteId).Warn);
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    [InlineData(ThemeCatalog.ElitePaletteId)]
    public void TheGroundsAreTheLevelColourAtTwelvePercentOntoBg(string themeId)
    {
        Manager().Apply(themeId);

        var bg = Published(ThemeManager.BgKey);

        Assert.Equal(Palette.Mix(bg, Published(ThemeManager.RedKey), 0.12), Published(ThemeManager.RedGroundKey));
        Assert.Equal(Palette.Mix(bg, Published(ThemeManager.WarnKey), 0.12), Published(ThemeManager.WarnGroundKey));
    }

    [AvaloniaFact]
    public void AnErrorIsARedBarOnTheRedGroundUnderAnErrorLabel()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var notice = Shown(new Notice { Text = "Groq rejected the key.", Detail = "http 401 · groq" });

        Assert.Equal(new Thickness(3, 0, 0, 0), notice.BorderThickness);
        Assert.Equal(Published(ThemeManager.RedKey), Ink(notice.BorderBrush));
        Assert.Equal(Published(ThemeManager.RedGroundKey), Ink(notice.Background));
        Assert.Equal(new Thickness(13, 10, 14, 10), notice.Padding);
        Assert.Equal(TypeScale.MinimumTarget, notice.MinHeight);

        var label = Block(notice, "ERROR");
        Assert.Equal(Published(ThemeManager.RedKey), Ink(label.Foreground));

        Assert.Equal(Published(ThemeManager.WhiteKey), Ink(Block(notice, "Groq rejected the key.").Foreground));
        Assert.Equal(Published(ThemeManager.GreyKey), Ink(Block(notice, "HTTP 401 · GROQ").Foreground));
    }

    [AvaloniaFact]
    public void AWarningIsAnAmberBarOnTheWarnGround()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var notice = Shown(new Notice(NoticeLevel.Warning, inline: true) { Label = "Journal not found", Text = "Start Elite." });

        Assert.Equal(Published(ThemeManager.WarnKey), Ink(notice.BorderBrush));
        Assert.Equal(Published(ThemeManager.WarnGroundKey), Ink(notice.Background));
        Assert.Equal(Published(ThemeManager.WarnKey), Ink(Block(notice, "JOURNAL NOT FOUND").Foreground));
        Assert.Equal(new Thickness(11, 6, 10, 6), notice.Padding);
        Assert.Equal(0, notice.MinHeight);
    }

    [AvaloniaFact]
    public void TheSettingsErrorBannerIsARedNoticeWithDismissInItsActions()
    {
        var view = new PanelView { DataContext = new PanelViewModel { ErrorText = "Settings could not be read." } };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var banner = view.GetVisualDescendants().OfType<Notice>().Single(notice => notice.Name == "ErrorBanner");

        Assert.Equal(NoticeLevel.Error, banner.Level);
        Assert.Equal("Settings could not be read.", banner.Text);
        Assert.Contains(banner.Actions, action => action.Name == "DismissErrorButton");

        window.Close();
    }

    [AvaloniaFact]
    public void AFailedTurnIsARedBarWithANoticeInIt()
    {
        Manager().Apply(ThemeCatalog.Elite);

        var model = new PanelViewModel();
        var view = new PanelView { DataContext = model };
        var window = new Window { Content = view, Width = 900, Height = 700 };
        window.Show();

        model.AppendError("I couldn't answer that. The details are on the Log File reading.");
        Dispatcher.UIThread.RunJobs();

        var bubbles = view.GetControl<StackPanel>("Bubbles");
        var turn = bubbles.Children.OfType<Border>().Last();

        Assert.Equal(Published(ThemeManager.RedKey), Ink(turn.BorderBrush));

        var notice = Assert.Single(turn.GetVisualDescendants().OfType<Notice>());
        Assert.Contains(
            notice.GetVisualDescendants().OfType<TextBlock>(),
            block => (block.Inlines?.Text ?? block.Text ?? string.Empty).Contains("I couldn't answer that.", StringComparison.Ordinal));

        window.Close();
    }

#if DEBUG
    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Light)]
    public void TheNoticesAreCaptured(string themeId)
    {
        using var look = AppLook.Put(themeId);

        var kit = new ControlKitWindow { Width = 900, Height = 900 };
        kit.Show();
        Dispatcher.UIThread.RunJobs();

        var first = kit.GetVisualDescendants().OfType<Border>().Single(border => border.Name == ControlKitWindow.CardPrefix + "Notice");
        var scroller = kit.GetVisualDescendants().OfType<ScrollViewer>().First();
        var top = first.TranslatePoint(default, (Visual)scroller.Content!)!.Value.Y;
        scroller.Offset = new Vector(0, Math.Max(0, top - 160));
        Dispatcher.UIThread.RunJobs();

        using var frame = kit.CaptureRenderedFrame()!;
        var path = Path.Combine(TestSurface.CaptureDirectory, $"notices-{themeId}.png");
        frame.Save(path, new PngBitmapEncoderOptions());
        Assert.True(File.Exists(path));

        kit.Close();
    }
#endif

    private static Notice Shown(Notice notice)
    {
        var window = new Window { Content = notice };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return notice;
    }

    private static TextBlock Block(Control within, string text) =>
        within.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == text);
}
