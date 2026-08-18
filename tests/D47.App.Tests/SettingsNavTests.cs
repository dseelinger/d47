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

/// <summary>
/// The nav column down the side of Settings, which is a scroll-spy rather than a tab strip
/// (list.md Phase 4, "Settings Nav Menu").
/// <para>
/// Opened the way the app opens it: the window is up and showing the transcript, and the page
/// is built when the tab is clicked. That order is the whole point — the surface paints its nav
/// inside <c>Attach</c>, which runs before the control is handed to the pane that hosts it, so
/// anything resolved against the visual tree at that moment resolves against no tree at all
/// (bugs.md 3).
/// </para>
/// </summary>
public class SettingsNavTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static (Window Window, PanelView Panel, SettingsView View) OpenLikeTheApp()
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

        var window = new Window { Content = panel, Width = 1180, Height = 880 };
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
    /// A null foreground is text that is laid out, counted and hit-tested and never seen, so
    /// counting the items would have passed all the way through this.
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
    /// And the active one is marked apart from the rest — the property the fetched brushes were
    /// there to provide, which a binding has to keep. The mark follows the scroller, because the
    /// nav says which section is being read rather than which was last clicked.
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

    /// <summary>
    /// A theme switch repaints the column. This used to be the job of a re-run from
    /// <see cref="SettingsView.Refresh"/>; it is now the binding's, and the binding is what has
    /// to be shown to do it.
    /// </summary>
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
