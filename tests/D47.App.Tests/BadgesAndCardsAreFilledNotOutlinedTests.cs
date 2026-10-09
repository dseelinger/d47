using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Badges and cards are filled with no outline, as <c>kit.css</c> draws them (#517).</summary>
public class BadgesAndCardsAreFilledNotOutlinedTests
{
    private static object? Resource(string key) => Application.Current!.Resources[key];

    private static Window Show(Control content, double width = 600, double height = 400)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        Application.Current!.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        });

        var window = new Window { Content = content, Width = width, Height = height, Background = (Avalonia.Media.IBrush?)Resource(ThemeManager.BgKey) };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    [AvaloniaTheory]
    [InlineData(false, ThemeManager.TileKey)]
    [InlineData(true, ThemeManager.AKey)]
    public void ACardIsAFilledTileWithNoBorder(bool selected, string groundKey)
    {
        var card = new Border { Child = new TextBlock { Text = "Card" } };
        var window = Show(card);

        CardChrome.Card(card, selected);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(default, card.BorderThickness);
        Assert.Equal(Resource(groundKey), card.Background);

        window.Close();
    }

    [AvaloniaFact]
    public void TheCurrentShipBadgeIsACyanChipWithKnockInk()
    {
        var card = (Button)LoadoutPages.Card(
            "Bad Idea (Python)", "Python\nJameson Memorial", marked: false, () => { }, LoadoutStanding.Active, headline: "Bad Idea");
        var window = Show(new StackPanel { Width = 320, Margin = new Thickness(16), Children = { card } });

        var text = card.GetVisualDescendants().OfType<TextBlock>()
            .Single(block => block.Foreground == Resource(ThemeManager.KnockKey));
        var badge = text.GetVisualAncestors().OfType<Border>().First();

        Assert.Equal(Resource(ThemeManager.CyanKey), badge.Background);
        Assert.Equal(default, badge.BorderThickness);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("current-ship-badge.png");

        window.Close();
    }

    [AvaloniaFact]
    public void TheStatusRowAndUpdateBannerDrawNoOutline()
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = Show(panel, 900, 700);

        foreach (var name in new[] { "PreReleaseBadge", "StartupRow", "SwitchRow", "UpdateBanner" })
        {
            Assert.Equal(default, panel.FindControl<Border>(name)!.BorderThickness);
        }

        Assert.Equal(Resource(ThemeManager.AKey), panel.FindControl<Border>("PreReleaseBadge")!.Background);
        Assert.Equal(Resource(ThemeManager.KnockKey), panel.FindControl<TextBlock>("PreReleaseBadgeText")!.Foreground);

        window.Close();
    }
}
