using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Tests;

/// <summary>Opens a panel in a 1280 by 960 window on the Search tab; a null <c>rootKey</c> leaves the tab alone.</summary>
public static class SearchPanelHosting
{
    public static (PanelView Panel, Window Window) Show(
        StarSystemSurface surface,
        string? rootKey,
        bool headset = false,
        PanelRouting? routing = null,
        IClipboard? copy = null)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        if (copy is not null)
        {
            panel.EnableCopy(copy);
        }

        panel.EnableStarSystem(surface);
        routing?.RouteNavigation(panel.Nav, action => action());

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Bind(Window.BackgroundProperty, Avalonia.Application.Current!.Resources.GetResourceObservable(ThemeManager.BgKey));
        window.Show();
        Dispatcher.UIThread.RunJobs();

        if (rootKey is not null)
        {
            panel.Tab = PanelTab.Search;
            panel.Nav.SelectRoot(rootKey);
            Dispatcher.UIThread.RunJobs();
        }

        return (panel, window);
    }

    public static (PanelView Panel, Window Window) ShowSearchForm(
        SettingsService settings,
        IGalaxyService galaxy,
        GalaxySearchBoard board,
        IStarSystemService systems,
        string root)
    {
        var history = new SearchHistory("search-history.json", new MemoryFileSystem(), NullLogger<SearchHistory>.Instance);
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(galaxy, () => "LTT 7786", settings, searches: board, history: history)]);
        var store = new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance);

        var surface = new StarSystemSurface(
            systems,
            () => new JournalLocation("LTT 7786", null, false, null),
            () => settings.Current.Knowledge.GalaxySearch,
            () => { })
        {
            Searches = new GalaxySearchSurface(registry, board, store, () => "F1", () => [])
            {
                PrefixNames = systems.PrefixNamesAsync,
                SuggestionPause = TimeSpan.Zero,
                History = history,
            },
        };

        return Show(surface, root);
    }
}
