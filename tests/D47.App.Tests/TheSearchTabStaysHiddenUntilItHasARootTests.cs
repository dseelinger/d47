using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Configuration;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>SEARCH sits second in the strip, is hidden until a root is registered, and mini never offers it (#822).</summary>
public sealed class TheSearchTabStaysHiddenUntilItHasARootTests
{
    private static readonly NavCrumb StubRoot = new("search.stub", "System");

    private static (PanelView Panel, Window Window) Shown(ViewStateStore? store = null, bool furnishSearch = false)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (furnishSearch)
        {
            panel.Furnish(PanelTab.Search, _ => new TextBlock { Text = "System" }, StubRoot);
        }

        if (store is not null)
        {
            panel.RememberTab(new PanelTabMemory(store));
        }

        var window = new Window { Content = panel, Width = 1180, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    private static List<string?> Strip(PanelView panel) =>
        [.. panel.Tabs.Children.OfType<RadioButton>().Where(tab => tab.IsVisible).Select(tab => tab.Content as string)];

    [AvaloniaFact]
    public void WithNoRootTheStripHasNoSearch()
    {
        var (panel, window) = Shown();

        Assert.False(panel.Nav.Has(PanelTab.Search));
        Assert.DoesNotContain("SEARCH", Strip(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void WithARootSearchShowsSecond()
    {
        var (panel, window) = Shown(furnishSearch: true);

        Assert.Equal(["TRANSCRIPT", "SEARCH"], Strip(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void SayingSearchOpensIt()
    {
        var (panel, window) = Shown(furnishSearch: true);

        Assert.NotNull(PanelPhrases.Apply("search", panel.Nav));
        Assert.Equal(PanelTab.Search, panel.Tab);

        window.Close();
    }

    [AvaloniaFact]
    public void ASavedSearchTabReopensIt()
    {
        var store = new ViewStateStore(
            new D47.Core.AppPaths(TempFolders.Create("d47-search-tab-tests")),
            NullLogger<ViewStateStore>.Instance);

        store.Save(store.Load() with { LastTab = "Search" });

        var (panel, window) = Shown(store, furnishSearch: true);

        Assert.Equal(PanelTab.Search, panel.Tab);

        window.Close();
    }

    [AvaloniaFact]
    public void MiniDoesNotOfferSearch()
    {
        var (panel, window) = Shown(furnishSearch: true);

        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        Assert.False(panel.Nav.Select(PanelTab.Search) && panel.Tab == PanelTab.Search);
        Assert.Equal(PanelTab.Transcript, panel.Tab);

        window.Close();
    }
}
