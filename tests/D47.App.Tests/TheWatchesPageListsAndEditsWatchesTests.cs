using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Watches;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Watches lists the watches, shows the gates that are off, and adds, edits and deletes (#2102).</summary>
[Trait("Category", "Integration")]
public class TheWatchesPageListsAndEditsWatchesTests
{
    private static readonly DateTimeOffset Fired = new(2026, 11, 3, 14, 7, 0, TimeSpan.Zero);

    private sealed record Rig(PanelView Panel, Window Window, WatchStore Store, SettingsService Settings);

    private static Rig Open(bool gatesOn)
    {
        var (settings, _, paths, _, _) = TestSurface.CreateFull(coverage: () => "x");
        var fs = new MemoryFileSystem();
        var store = new WatchStore(Path.Combine(paths.Data, "watches.json"), fs, NullLogger<WatchStore>.Instance);

        var stock = new Watch(
            Guid.NewGuid(), "Soontill Relics at Cheranovsky City", WatchKind.Stock,
            MarketId: 3225348096, Station: "Cheranovsky City", System: "Ngurii", Commodity: "Soontill Relics",
            Comparison: StockComparison.Above, Threshold: 200);
        store.Add(stock);
        store.MarkFired(stock.Id, Fired);
        store.Add(new Watch(Guid.NewGuid(), "Tritium at Jameson Memorial", WatchKind.Stock, false, 128666762, "Jameson Memorial", "Shinrarta Dezhra", "Tritium", StockComparison.Below, 5000));
        store.Add(new Watch(Guid.NewGuid(), "New Community Goals", WatchKind.NewCommunityGoal));

        settings.Apply(GalaxyCapability.EnabledKey, gatesOn ? "true" : "false", SettingsCaller.Panel);
        settings.Apply(GalaxyCapability.BgsTickKey, gatesOn ? "true" : "false", SettingsCaller.Panel);
        settings.Apply(GalaxyCapability.GalNetKey, gatesOn ? "true" : "false", SettingsCaller.Panel);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableWatches(new WatchesDesk(
            store,
            new WatchSetup(store, new StubGalaxyService(), new StubTradePlanService(), () => settings.Current.Knowledge.GalaxySearch),
            settings));

        var window = new Window { Content = panel, Width = 1280, Height = 1300 };
        window.Show();
        panel.Tab = PanelTab.Commander;
        Dispatcher.UIThread.RunJobs();

        return new Rig(panel, window, store, settings);
    }

    private static string Texts(Control root) =>
        string.Join("\n", root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));

    private static void Press(PanelView panel, string label)
    {
        panel.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, label))
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    [AvaloniaFact]
    public void TheListShowsEachWatchWithItsConditionAndWhenItFired()
    {
        var rig = Open(gatesOn: true);
        var page = rig.Panel.GetVisualDescendants().OfType<WatchesPage>().Single();
        var text = Texts(page);

        Assert.Contains("Soontill Relics at Cheranovsky City", text, StringComparison.Ordinal);
        Assert.Contains("Above 200 · Cheranovsky City, Ngurii", text, StringComparison.Ordinal);
        Assert.Contains(WatchFired.Text(WatchStoreStamp(), TimeZoneInfo.Local), text, StringComparison.Ordinal);
        Assert.Contains("Not fired yet", text, StringComparison.Ordinal);
        Assert.Contains("3 OF 32", text, StringComparison.Ordinal);
        Assert.Empty(page.GetVisualDescendants().OfType<Notice>());

        Save(rig.Window, "watches-list.png");
    }

    private static string WatchStoreStamp() => BgsTickWatch.Format(Fired);

    [AvaloniaFact]
    public void EachGateThatIsOffShowsANoticeWithATurnOnTile()
    {
        var rig = Open(gatesOn: false);
        var page = rig.Panel.GetVisualDescendants().OfType<WatchesPage>().Single();
        var text = Texts(page);

        Assert.Contains(WatchesPage.BgsTickLabel, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(WatchesPage.GalaxyLabel, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(WatchesPage.GalNetLabel, text, StringComparison.OrdinalIgnoreCase);

        Save(rig.Window, "watches-gates-off.png");

        var turnOn = page.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, WatchesPage.TurnOn));
        turnOn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(rig.Settings.Current.Knowledge.BgsTick);
    }

    [AvaloniaFact]
    public void DeletingAWatchRemovesItFromTheFile()
    {
        var rig = Open(gatesOn: true);
        var page = rig.Panel.GetVisualDescendants().OfType<WatchesPage>().Single();

        page.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, "DELETE"))
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, rig.Store.Watches.Count);
    }

    [AvaloniaFact]
    public void TheNewStockWatchFormOpensWithItsFields()
    {
        var rig = Open(gatesOn: true);
        Press(rig.Panel, "ADD STOCK WATCH");

        var form = rig.Panel.GetVisualDescendants().OfType<WatchFormPage>().Single();
        var text = Texts(form);

        Assert.Contains("STATION FROM", text, StringComparison.Ordinal);
        Assert.Contains("UNITS", text, StringComparison.Ordinal);
        Assert.Contains(WatchFormPage.WhereHelp, text, StringComparison.Ordinal);

        Save(rig.Window, "watches-new-stock.png");
    }

    [AvaloniaFact]
    public void AnAnyStationChoiceIsDisabledWhileGalaxySearchIsOff()
    {
        var rig = Open(gatesOn: false);
        Press(rig.Panel, "ADD STOCK WATCH");

        var form = rig.Panel.GetVisualDescendants().OfType<WatchFormPage>().Single();

        Assert.Contains(WatchFormPage.AnyStationOff, Texts(form), StringComparison.Ordinal);

        Save(rig.Window, "watches-new-stock-gates-off.png");
    }
}
