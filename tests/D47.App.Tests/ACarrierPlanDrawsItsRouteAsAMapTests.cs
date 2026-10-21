using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A carrier plan's result page draws the carrier's route as a map, restock stops marked (#1397).</summary>
public class ACarrierPlanDrawsItsRouteAsAMapTests
{
    private static StoredRoutePlan Plan(bool positioned = true, int? reached = null) => new()
    {
        Kind = RoutePlanKind.Carrier,
        Headline = "Sol to Colonia",
        Reached = reached,
        Carrier = new CarrierRoute(
        [
            .. Enumerable.Range(0, 12).Select(index => new CarrierWaypoint(
                $"Stop {index}", index == 0 ? 0 : 450, 0, 500, 40, index % 4 == 3, 100, 0, false, false, index == 11)
            {
                Position = positioned ? new StarPosition(index * 450, 0, 200 * Math.Sin(index)) : null,
            }),
        ]),
    };

    private static (RoutePlanResultPage Page, Window Window) Open(StoredRoutePlan plan, double height = 700)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        Application.Current!.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        });

        var page = new RoutePlanResultPage(plan);
        var window = new Window { Content = page, Width = 1000, Height = height, Background = (IBrush?)Application.Current.Resources[ThemeManager.BgKey] };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (page, window);
    }

    private static RouteMapView Map(Control page) => page.GetVisualDescendants().OfType<RouteMapView>().Single();

    [AvaloniaFact]
    public void APositionedCarrierPlanShowsTheMapInTheWindow()
    {
        var (page, window) = Open(Plan(reached: 4));

        Assert.True(Map(page).IsVisible);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("carrier-map-window.png");
    }

    [AvaloniaFact]
    public void TheMapFitsTheBigPanel()
    {
        var (page, window) = Open(Plan(reached: 4), height: 640);

        Assert.InRange(Map(page).Bounds.Width, RouteMapView.SmallestSide, RouteMapView.LargestSide);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("carrier-map-panel.png");
    }

    [AvaloniaFact]
    public void ACarrierPlanWithoutPositionsShowsNoMap()
    {
        var (page, _) = Open(Plan(positioned: false));

        Assert.False(Map(page).IsVisible);
    }

    [AvaloniaFact]
    public void AnotherKindOfPlanShowsNoMap()
    {
        var (page, _) = Open(new StoredRoutePlan { Kind = RoutePlanKind.Trade, Headline = "Trade" });

        Assert.False(Map(page).IsVisible);
    }
}
