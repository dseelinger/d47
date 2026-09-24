using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.App.Tests.SettingsPageReading;

namespace D47.App.Tests;

/// <summary>The nav's mark is on the place whose page is open, at every zoom.</summary>
public class TheOpenPlaceIsMarkedInTheNavTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static (Window Window, SettingsView View) Open(int zoom)
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);
        settings.Apply(InterfaceCapability.ZoomKey, zoom.ToString(), SettingsCaller.Panel);

        // Every place.
        settings.Apply(InterfaceCapability.ShowEverySettingKey, "true", SettingsCaller.Panel);

        var view = new SettingsView();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableSettings(() =>
        {
            view.Attach(settings, viewState, paths);
            return view;
        });

        var window = new Window { Content = panel, Width = 1180, Height = 880 };

        // The same host the app wraps its window in.
        ZoomHost.Attach(window, settings);

        window.Show();
        Jobs();

        panel.Tab = PanelTab.Settings;
        Jobs();

        return (window, view);
    }

    private static Color? Colour(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    /// <summary>The nav item wearing the active fill, or -1 when none is.</summary>
    private static int Active(SettingsView view)
    {
        var items = PlaceItems(view);
        var fill = Colour(Application.Current!.FindResource(ThemeManager.AKey) as IBrush);

        return items.FindIndex(item => Colour(item.Background) == fill);
    }

    [AvaloniaTheory]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(175)]
    public void TheMarkedPlaceIsTheOneWhosePageIsOpen(int zoom)
    {
        var (window, view) = Open(zoom);

        var items = PlaceItems(view);

        Assert.True(items.Count(item => item.IsVisible) > 1, "there are places to walk");

        for (var i = 0; i < items.Count; i++)
        {
            if (!items[i].IsVisible)
            {
                continue;
            }

            view.ShowPlace(i);
            Jobs();

            Assert.Equal(i, Active(view));
            Assert.Equal(Words(Name(items[i])), Words(Title(view)));
        }

        window.Close();
    }
}
