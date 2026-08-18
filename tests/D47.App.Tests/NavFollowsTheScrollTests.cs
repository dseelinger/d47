using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.App.Settings;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The nav highlight against what is actually at the top of the page
/// (remediation.md, "Nav highlight does not track the settings scroll").
/// <para>
/// The existing nav tests assert that the mark <em>moves</em> when the scroller does. That is a
/// weaker claim than the one a Commander is making when they say it does not line up, and it
/// passed throughout: what has to be asserted is which section, for a scroll position derived
/// from the card rather than from the same arithmetic the surface uses.
/// </para>
/// </summary>
public class NavFollowsTheScrollTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static (Window Window, SettingsView View) Open(int zoom)
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);
        settings.Apply(InterfaceCapability.ZoomKey, zoom.ToString(), SettingsCaller.Panel);

        var view = new SettingsView();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableSettings(() =>
        {
            view.Attach(settings, viewState, paths);
            return view;
        });

        var window = new Window { Content = panel, Width = 1180, Height = 880 };

        // The same host the app wraps its window in. Zoom is a layout transform over the whole
        // tree, so a page tested without one is a page tested at a scale the Commander is not at.
        ZoomHost.Attach(window, settings);

        window.Show();
        Jobs();

        panel.Tab = PanelTab.Settings;
        Jobs();

        return (window, view);
    }

    private static Color? Colour(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    private static IReadOnlyList<Border> Cards(SettingsView view) =>
        [.. ((StackPanel)view.FindControl<Control>("Cards")!).Children.OfType<Border>()];

    private static IReadOnlyList<Border> NavItems(SettingsView view) =>
        [.. ((StackPanel)view.FindControl<Control>("NavItems")!).Children.OfType<Border>()];

    /// <summary>The nav item wearing the active fill, or -1 when none is.</summary>
    private static int Active(SettingsView view)
    {
        var items = NavItems(view);
        var fill = Colour(Application.Current!.FindResource(ThemeManager.SurfaceAltKey) as IBrush);

        for (var i = 0; i < items.Count; i++)
        {
            if (Colour(items[i].Background) == fill)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Puts a card's top edge at the top of the viewport, measured off the card rather than
    /// computed the way the surface computes it — otherwise the test agrees with the bug.
    /// </summary>
    private static void ScrollTo(ScrollViewer scroller, Border card)
    {
        var above = card.TranslatePoint(new Point(0, 0), scroller)!.Value.Y;
        scroller.Offset = new Vector(0, scroller.Offset.Y + above);
        Jobs();
    }

    [AvaloniaTheory]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(175)]
    public void TheMarkedSectionIsTheOneAtTheTopOfThePage(int zoom)
    {
        var (window, view) = Open(zoom);

        var scroller = (ScrollViewer)view.FindControl<Control>("Scroller")!;
        var cards = Cards(view);

        Assert.True(cards.Count > 3, "there are sections to walk");

        for (var i = 0; i < cards.Count; i++)
        {
            ScrollTo(scroller, cards[i]);

            // The last card can be shorter than the viewport, so the scroller runs out before
            // its top reaches the edge. That is the scroll-spy end case and it has its own rule.
            var reached = cards[i].TranslatePoint(new Point(0, 0), scroller)!.Value.Y <= 1;

            if (!reached)
            {
                continue;
            }

            Assert.Equal(i, Active(view));
        }

        window.Close();
    }

}
