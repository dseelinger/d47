using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>Navigation › Market adds each commodity from the type-ahead as a tile carrying its tonnes, up to five.</summary>
public sealed class MarketTakesEachCommodityAsATileTests
{
    private static (PanelView Panel, Window Window, RouteMarketPage Page) Furnished()
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(
            new RoutingSurface(
                () => new NavRoute(),
                () => "Sol",
                CapabilityRegistry.Build([]),
                Plans: null,
                LookupsEnabled: () => true,
                OpenSettings: null,
                Commodities: new CommodityBoard())
            {
                SuggestionPause = TimeSpan.Zero,
            },
            plan: false,
            progress: false);

        var window = new Window { Content = panel, Width = 1100, Height = 800 };
        window.Show();
        panel.Tab = PanelTab.Navigation;
        Dispatcher.UIThread.RunJobs();

        return (panel, window, panel.GetVisualDescendants().OfType<RouteMarketPage>().Single());
    }

    private static void Typed(RouteMarketPage page, string typed)
    {
        page.Type(typed);
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(50);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Choose(RouteMarketPage page, string typed, string offered, int? tonnes = null)
    {
        page.Weigh(tonnes);
        Typed(page, typed);
        Assert.Contains(offered, page.Suggestions.Shown);
        page.Suggestions.Choose(offered);
        Dispatcher.UIThread.RunJobs();
    }

    private static List<Button> Tiles(Control root) =>
        [.. root.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains(RouteMarketPage.ItemClass))];

    private static List<string> TextOf(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text ?? string.Empty)];

    private static T Named<T>(Control root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    [AvaloniaFact]
    public void TypingOffersMarketCommoditiesAndNothingElse()
    {
        var (_, window, page) = Furnished();

        Typed(page, "pal");

        Assert.Contains("Palladium", page.Suggestions.Shown);
        Assert.All(page.Suggestions.Shown, name => Assert.True(
            MaterialCatalogue.Find(name) is { Ledger: MaterialLedger.Cargo or MaterialLedger.RareCargo },
            $"{name} is not market cargo."));

        window.Close();
    }

    [AvaloniaFact]
    public void PickingASuggestionAddsATileWithItsTonnesAndClearsBothFields()
    {
        var (panel, window, page) = Furnished();

        Choose(page, "gold", "Gold", tonnes: 20);

        Assert.Equal([new CommodityWant("Gold", 20)], page.Items);
        Assert.Equal(string.Empty, Named<TextBox>(panel, RouteMarketPage.AddName).Text);
        Assert.Null(Named<Amount>(panel, RouteMarketPage.TonnesName).Value);

        var tile = Assert.Single(Tiles(panel));
        Assert.Equal(["GOLD", "·", "20 T", "✕"], TextOf(tile));

        window.Close();
    }

    /// <summary>Tonnes typed and not yet entered is still the tile's: clicking a suggestion takes focus, which commits the box first.</summary>
    [AvaloniaFact]
    public void ClickingASuggestionWhileTonnesIsStillBeingTypedKeepsTheTonnes()
    {
        var (panel, window, page) = Furnished();

        Typed(page, "gold");

        var tonnes = Named<Amount>(panel, RouteMarketPage.TonnesName);
        tonnes.BeginEdit();
        Named<TextBox>(tonnes, "AmountEditor").Text = "20";
        Dispatcher.UIThread.RunJobs();

        var row = page.Suggestions.List.GetVisualDescendants().OfType<Button>()
            .First(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Gold");
        var middle = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;

        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([new CommodityWant("Gold", 20)], page.Items);
        Assert.Null(tonnes.Value);

        window.Close();
    }

    [AvaloniaFact]
    public void ATileWithoutTonnesShowsTheNameAlone()
    {
        var (panel, window, page) = Furnished();

        Choose(page, "silver", "Silver");

        Assert.Equal(["SILVER", "✕"], TextOf(Assert.Single(Tiles(panel))));

        window.Close();
    }

    [AvaloniaFact]
    public void PressingATileRemovesIt()
    {
        var (panel, window, page) = Furnished();

        Choose(page, "gold", "Gold", tonnes: 20);
        Choose(page, "silver", "Silver");

        Tiles(panel).Single(tile => Equals(tile.Tag, "Gold")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([new CommodityWant("Silver", null)], page.Items);
        Assert.Equal(["Silver"], Tiles(panel).Select(tile => tile.Tag));

        window.Close();
    }

    [AvaloniaFact]
    public void ACommodityAlreadyAddedIsNotOfferedAgain()
    {
        var (_, window, page) = Furnished();

        Choose(page, "gold", "Gold");
        Typed(page, "gold");

        Assert.DoesNotContain("Gold", page.Suggestions.Shown);

        window.Close();
    }

    [AvaloniaFact]
    public void AtFiveTilesBothFieldsAreDisabledAndTheLineSaysWhy()
    {
        var (panel, window, page) = Furnished();

        var full = Named<TextBlock>(panel, RouteMarketPage.FullName);
        Assert.False(full.IsEffectivelyVisible);

        foreach (var name in new[] { "Gold", "Silver", "Painite", "Palladium", "Platinum" })
        {
            page.Add(name, null);
        }

        page.Add("Tritium", null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(5, page.Items.Count);
        Assert.False(Named<TextBox>(panel, RouteMarketPage.AddName).IsEnabled);
        Assert.False(Named<Amount>(panel, RouteMarketPage.TonnesName).IsEnabled);
        Assert.True(full.IsEffectivelyVisible);
        Assert.Equal(RouteMarketPage.FullText, full.Text);

        page.Remove("Silver");
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named<TextBox>(panel, RouteMarketPage.AddName).IsEnabled);
        Assert.False(full.IsEffectivelyVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void TheTilesBecomeOneCommodityListWithEachTonnage()
    {
        var (_, window, page) = Furnished();

        page.Add("Gold", 20);
        page.Add("Silver", null);

        var arguments = page.FormArguments();

        Assert.True(arguments.TryGetString("commodity", out var commodity));
        Assert.Equal("Gold: 20, Silver", commodity);
        Assert.False(arguments.TryGetString("tonnes", out _));

        window.Close();
    }
}
