using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.App.Settings;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The nav column down the side of Settings, which is a scroll-spy rather than a tab strip.</summary>
public class SettingsNavTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static (Window Window, PanelView Panel, SettingsView View) OpenLikeTheApp(
        double height = 880)
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var view = new SettingsView();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableSettings(() =>
        {
            view.Attach(settings, viewState, paths);
            return view;
        });

        panel.EnableSearch();

        var window = new Window { Content = panel, Width = 1180, Height = height };
        window.Show();
        Jobs();

        // The click on the tab, after the window is already up on the transcript.
        panel.Tab = PanelTab.Settings;
        Jobs();

        return (window, panel, view);
    }

    private static List<TextBlock> NavLabels(SettingsView view) =>
        [.. ((StackPanel)view.FindControl<Control>("NavItems")!).Children
            .Select(item => item.GetVisualDescendants().OfType<TextBlock>().First())];

    private static Color? Colour(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    /// <summary>
    /// Every section is named in the nav the moment the page opens, and named in ink that draws.
    /// </summary>
    [AvaloniaFact]
    public void TheNavIsReadableAsSoonAsThePageOpens()
    {
        var (window, _, view) = OpenLikeTheApp();

        var labels = NavLabels(view);

        Assert.True(labels.Count > 1, "there is a nav column to read");

        var background = Colour(Application.Current!.FindResource(ThemeManager.BackgroundKey) as IBrush);

        foreach (var label in labels)
        {
            Assert.NotNull(label.Foreground);
            Assert.NotEqual(background, Colour(label.Foreground));
        }

        window.Close();
    }

    /// <summary>
    /// And the active one is marked apart from the rest — the property the fetched brushes were there
    /// to provide, which a binding has to keep.
    /// </summary>
    [AvaloniaFact]
    public void TheActiveSectionIsMarkedAndTheMarkFollowsTheScroller()
    {
        var (window, _, view) = OpenLikeTheApp();

        var labels = NavLabels(view);
        var items = ((StackPanel)view.FindControl<Control>("NavItems")!).Children;
        var scroller = (ScrollViewer)view.FindControl<Control>("Scroller")!;

        Assert.NotEqual(Colour(labels[0].Foreground), Colour(labels[1].Foreground));
        Assert.NotEqual(Colour(((Border)items[0]).Background), Colour(((Border)items[1]).Background));

        var wasInk = Colour(labels[0].Foreground);
        var wasFill = Colour(((Border)items[0]).Background);

        scroller.Offset = new Vector(0, scroller.Extent.Height);
        Jobs();

        // The last section is the one being read now, and it wears what the first one wore.
        Assert.Equal(wasInk, Colour(labels[^1].Foreground));
        Assert.Equal(wasFill, Colour(((Border)items[^1]).Background));

        // The one it left goes back to the muted ink the rest of the column is drawn in.
        Assert.Equal(Colour(labels[1].Foreground), Colour(labels[0].Foreground));

        window.Close();
    }

    /// <summary>The nav scrolls, and the highlight is brought into view rather than being left off the
    /// bottom.</summary>
    [AvaloniaFact]
    public void TheNavScrollsAndTheMarkedEntryIsBroughtIntoView()
    {
        var (window, _, view) = OpenLikeTheApp(height: 500);

        var nav = (ScrollViewer)view.FindControl<Control>("NavScroller")!;
        var items = ((StackPanel)view.FindControl<Control>("NavItems")!).Children;
        var cards = (ScrollViewer)view.FindControl<Control>("Scroller")!;

        Assert.True(
            nav.Extent.Height > nav.Viewport.Height,
            "the nav is longer than the window, which is the reported condition");

        // Nothing has moved it yet: the first section is the one being read.
        Assert.Equal(0, nav.Offset.Y);

        cards.Offset = new Vector(0, cards.Extent.Height);
        Jobs();

        var last = (Border)items[^1];
        var top = last.Bounds.Y;
        var bottom = top + last.Bounds.Height;

        Assert.InRange(top, nav.Offset.Y, nav.Offset.Y + nav.Viewport.Height);
        Assert.InRange(bottom, nav.Offset.Y, nav.Offset.Y + nav.Viewport.Height);

        window.Close();
    }

    [AvaloniaFact]
    public void AThemeSwitchRepaintsTheNav()
    {
        var (window, _, view) = OpenLikeTheApp();

        var labels = NavLabels(view);
        var before = Colour(labels[0].Foreground);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(D47.Core.Interface.ThemeCatalog.Light);

        Jobs();

        Assert.NotEqual(before, Colour(labels[0].Foreground));
        Assert.Equal(
            Colour(Application.Current!.FindResource(ThemeManager.TextKey) as IBrush),
            Colour(labels[0].Foreground));

        window.Close();
    }
}
