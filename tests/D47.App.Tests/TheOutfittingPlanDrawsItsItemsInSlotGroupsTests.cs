using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>An outfitting plan draws figures, a heading per stop and the stop's items in slot groups (#2324).</summary>
[Trait("Category", "Integration")]
public class TheOutfittingPlanDrawsItsItemsInSlotGroupsTests
{
    private static OutfittingRoute Route(DateTimeOffset? seen = null) => new(
        [
            new OutfittingStop("Eravate", "Ackerman Market", 12.4, ["6A Fuel Scoop", "Python", "5A Thrusters"]) { StockLastSeen = seen },
            new OutfittingStop("Wolf 359", "Hub", 3.1, ["Fuel Scoop"]) { StockLastSeen = seen },
        ],
        [])
    {
        Reference = "LHS 3447",
    };

    private static RoutePlanResultPage Page(OutfittingRoute route, params string[] bought)
    {
        new ThemeManager(Avalonia.Application.Current!, Microsoft.Extensions.Logging.Abstractions.NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var plan = new StoredRoutePlan { Kind = RoutePlanKind.Outfitting, Headline = "2 stops", Outfitting = route, Bought = bought };
        var page = new RoutePlanResultPage(plan);
        var window = new Window { Content = page, Width = 1100, Height = 900 };
        window.Show();

        return page;
    }

    private static List<string> Texts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void TheFiguresAndGroupsAreDrawnInOrder()
    {
        using var kit = AppLook.ControlKit();
        var texts = Texts(Page(Route()));

        Assert.Equal(["STOPS", "ITEMS", "ROUTE", "BOUGHT"], texts.Where(text => text is "STOPS" or "ITEMS" or "ROUTE" or "BOUGHT"));
        Assert.Contains("15.5 ly", texts);
        Assert.Contains("0 of 4", texts);
        Assert.Contains("01", texts);
        Assert.Contains("02", texts);

        var heads = texts.Where(text => text is "OPTIONAL INTERNAL" or "HARDPOINTS AND UTILITY" or "SHIPS" or "OTHER" or "CORE INTERNAL");

        Assert.Equal(["CORE INTERNAL", "OPTIONAL INTERNAL", "SHIPS", "OPTIONAL INTERNAL"], heads);
    }

    [AvaloniaFact]
    public void AShipLandsInShipsAndASizelessScoopInOptionalInternal()
    {
        using var kit = AppLook.ControlKit();
        var texts = Texts(Page(Route()));

        var ships = texts.IndexOf("SHIPS");

        Assert.True(ships >= 0);
        Assert.Equal("Python", texts[ships + 3]);

        var second = texts.LastIndexOf("OPTIONAL INTERNAL");

        Assert.Equal("Fuel Scoop", texts[second + 3]);
    }

    [AvaloniaFact]
    public void ABoughtItemShowsTheWordAndRaisesTheCount()
    {
        using var kit = AppLook.ControlKit();
        var texts = Texts(Page(Route(), "Python"));

        Assert.Contains("✓ BOUGHT", texts);
        Assert.Contains("1 of 4", texts);
    }

    [AvaloniaFact]
    public void NoItemRowRespondsToAPress()
    {
        using var kit = AppLook.ControlKit();
        var page = Page(Route());

        Assert.Empty(page.GetVisualDescendants().OfType<CheckBox>());
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), button => button.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void AReportOlderThanThirtyDaysIsFlaggedAndOneYoungerIsNot()
    {
        using var kit = AppLook.ControlKit();

        Assert.Contains("OLD REPORT", Texts(Page(Route(DateTimeOffset.UtcNow.AddDays(-31)))));
        Assert.DoesNotContain("OLD REPORT", Texts(Page(Route(DateTimeOffset.UtcNow.AddDays(-29)))));
    }
}
