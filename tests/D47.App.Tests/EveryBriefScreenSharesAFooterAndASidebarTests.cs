using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The brief screens share a footer — the say-line and when the journal last moved the page on, above a
/// <c>line2</c> rule — and the kit's sidebar.
/// </summary>
public class EveryBriefScreenSharesAFooterAndASidebarTests
{
    private static void Apply(string themeId) =>
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(themeId);

    private static Color Published(string key) => ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    private static Color Ink(IBrush? brush) => ((ISolidColorBrush)brush!).Color;

    private static Window Show(Control content, double width = 700, double height = 200)
    {
        var window = new Window
        {
            Content = content,
            Width = width,
            Height = height,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static string Local(DateTimeOffset at) => at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    [AvaloniaFact]
    public void BeforeAnyEventTheFooterDrawsOnlyTheSayLine()
    {
        Apply(ThemeCatalog.Elite);

        var footer = new PageFooter("what's on this body", new JournalClock(() => null));
        var window = Show(footer);

        Assert.Null(footer.Time);
        Assert.Contains(
            footer.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "Say: “what's on this body”");

        window.Close();
    }

    [AvaloniaFact]
    public void TheFooterSitsOnALine2RuleWithTheTimeInMonoCyan()
    {
        Apply(ThemeCatalog.Elite);

        var at = new DateTimeOffset(2026, 9, 27, 19, 42, 0, TimeSpan.Zero);
        var footer = new PageFooter("what's on this body", new JournalClock(() => at));
        var window = Show(footer);

        Assert.Equal(new Thickness(0, 1, 0, 0), footer.BorderThickness);
        Assert.Equal(Published(ThemeManager.Line2Key), Ink(footer.BorderBrush));
        Assert.Equal(Local(at), footer.Time);

        var time = footer.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == Local(at));
        Assert.Equal(Published(ThemeManager.CyanKey), Ink(time.Foreground));
        Assert.Contains("JetBrains", time.FontFamily.Name, StringComparison.Ordinal);

        Assert.Contains(
            footer.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "KEPT CURRENT BY THE JOURNAL · ");

        window.Close();
    }

    [AvaloniaFact]
    public void TheFootersTimeMovesWhenAnEventIsAppliedWithoutReopeningThePage()
    {
        Apply(ThemeCatalog.Elite);

        DateTimeOffset? last = null;
        var clock = new JournalClock(() => last);
        var footer = new PageFooter("how far to go", clock);
        var window = Show(footer);

        Assert.False(clock.Tick());
        Assert.Null(footer.Time);

        last = new DateTimeOffset(2026, 9, 27, 19, 42, 0, TimeSpan.Zero);
        Assert.True(clock.Tick());
        Assert.Equal(Local(last.Value), footer.Time);

        last = last.Value.AddMinutes(9);
        Assert.True(clock.Tick());
        Assert.Equal(Local(last.Value), footer.Time);

        window.Close();
    }

    private static Sidebar Materials(Action<string>? select = null) => new(
        [
            new SidebarGroup("Plans", [new SidebarItem("needed", "Needed by plans", "7")]),
            new SidebarGroup("Ship", [
                new SidebarItem("raw", "Raw", "212"),
                new SidebarItem("manufactured", "Manufactured", "348"),
                new SidebarItem("encoded", "Encoded", "190"),
            ]),
            new SidebarGroup("On foot", [new SidebarItem("locker", "Ship locker", "64")]),
        ],
        selected: "raw",
        select: select);

    [AvaloniaFact]
    public void TheSidebarIs230WideWithALine2RuleAnd28BeforeTheContent()
    {
        Apply(ThemeCatalog.Elite);

        var sidebar = Materials();
        var window = Show(sidebar, height: 500);

        Assert.Equal(230, sidebar.Bounds.Width);
        Assert.Equal(28, sidebar.Margin.Right);
        Assert.Equal(new Thickness(0, 0, 1, 0), sidebar.BorderThickness);
        Assert.Equal(Published(ThemeManager.Line2Key), Ink(sidebar.BorderBrush));
        Assert.All(sidebar.Items, item => Assert.Equal(44, item.Bounds.Height));

        window.Close();
    }

    [AvaloniaFact]
    public void TheSelectedItemIsSolidAWithKnockTextAndHoverIsTile2()
    {
        Apply(ThemeCatalog.Elite);

        var sidebar = Materials();
        var window = Show(sidebar, height: 500);

        TextBlock Label(Border item) => item.GetVisualDescendants().OfType<TextBlock>().First();

        var raw = sidebar.Items[1];
        Assert.Equal(Published(ThemeManager.AKey), Ink(raw.Background));
        Assert.Equal(Published(ThemeManager.KnockKey), Ink(Label(raw).Foreground));
        Assert.Equal("RAW", Label(raw).Text);

        var encoded = sidebar.Items[3];
        Assert.Equal(Published(ThemeManager.AKey), Ink(Label(encoded).Foreground));

        window.MouseMove(encoded.TranslatePoint(new Point(20, 20), window)!.Value);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Published(ThemeManager.Tile2Key), Ink(encoded.Background));
        Assert.Equal(Published(ThemeManager.WhiteKey), Ink(Label(encoded).Foreground));

        window.Close();
    }

    [AvaloniaFact]
    public void PressingAnItemSelectsItAndHandsBackItsKey()
    {
        Apply(ThemeCatalog.Elite);

        string? chosen = null;
        var sidebar = Materials(key => chosen = key);
        var window = Show(sidebar, height: 500);

        var point = sidebar.Items[4].TranslatePoint(new Point(20, 20), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("locker", chosen);
        Assert.Equal("locker", sidebar.Selected);
        Assert.Equal(Published(ThemeManager.AKey), Ink(sidebar.Items[4].Background));
        Assert.Equal(Brushes.Transparent, sidebar.Items[1].Background);

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    [InlineData(ThemeCatalog.ElitePaletteId)]
    public void TheSidebarAndFooterAreCapturedInEveryTheme(string themeId)
    {
        using var kit = AppLook.ControlKit();
        Apply(themeId);

        var sidebar = Materials();
        var at = new DateTimeOffset(2026, 9, 27, 19, 42, 0, TimeSpan.Zero);
        var footer = new PageFooter("what do I need for my plans", new JournalClock(() => at));

        var content = new TextBlock { Text = "Content", Margin = new Thickness(0, 4, 0, 0) };
        content[!TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(ThemeManager.WhiteKey);

        var body = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(sidebar, Dock.Left);
        body.Children.Add(sidebar);
        body.Children.Add(content);

        var page = new DockPanel { Margin = new Thickness(28, 20) };
        DockPanel.SetDock(footer, Dock.Bottom);
        page.Children.Add(footer);
        page.Children.Add(body);

        var window = Show(page, width: 900, height: 480);

        window.MouseMove(sidebar.Items[3].TranslatePoint(new Point(20, 20), window)!.Value);
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        var path = $"sidebar-footer-{themeId}.png";
        frame.SaveCapture(path);

        window.Close();
    }
}
