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

/// <summary>Neutron, Road to Riches and exobiology plans draw their route as a map, loops closed (#1401).</summary>
public class AJumpRichesOrExobiologyPlanDrawsItsRouteAsAMapTests
{
    private static StarPosition At(int index, int count) =>
        new(1000 * Math.Cos(index * 2 * Math.PI / count), 0, 1000 * Math.Sin(index * 2 * Math.PI / count));

    private static StoredRoutePlan Jump(bool positioned = true) => new()
    {
        Kind = RoutePlanKind.Jump,
        Headline = "Sol to Colonia",
        Reached = 20,
        Jump = new PlottedRoute(
            "Sol",
            "Colonia",
            22_000,
            128,
            [.. Enumerable.Range(0, 128).Select(index => new RouteWaypoint($"Waypoint {index}", 1, 100, index % 3 == 0)
            {
                Position = positioned ? new StarPosition(index * 150, 0, 300 * Math.Sin(index / 9.0)) : null,
            })])
        {
            Start = positioned ? new RouteEndpoint("Sol", new StarPosition(0, 0, 0)) : null,
        },
    };

    private static StoredRoutePlan Riches(bool positioned = true) => new()
    {
        Kind = RoutePlanKind.Riches,
        Headline = "Road to Riches",
        Reached = 3,
        Riches = new RichesRoute(
            [.. Enumerable.Range(0, 10).Select(index => new RichesStop($"Stop {index}", 1, [])
            {
                Position = positioned ? At(index + 1, 11) : null,
            })])
        {
            Start = positioned ? new RouteEndpoint("Home", At(0, 11)) : null,
            End = positioned ? new RouteEndpoint("Home", At(0, 11)) : null,
        },
    };

    private static StoredRoutePlan Exobiology(bool positioned = true) => new()
    {
        Kind = RoutePlanKind.Exobiology,
        Headline = "Exobiology",
        Reached = 3,
        Exobiology = new ExobiologyRoute(
            [.. Enumerable.Range(0, 10).Select(index => new ExobiologyStop($"Stop {index}", 1, [])
            {
                Position = positioned ? At(index + 1, 11) : null,
            })])
        {
            Start = positioned ? new RouteEndpoint("Home", At(0, 11)) : null,
            End = positioned ? new RouteEndpoint("Home", At(0, 11)) : null,
        },
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

    public static TheoryData<string> Kinds => new() { "jump", "riches", "exobiology" };

    private static StoredRoutePlan For(string kind, bool positioned = true) => kind switch
    {
        "jump" => Jump(positioned),
        "riches" => Riches(positioned),
        _ => Exobiology(positioned),
    };

    [AvaloniaTheory]
    [MemberData(nameof(Kinds))]
    public void APositionedPlanShowsTheMapInTheWindow(string kind)
    {
        var (page, window) = Open(For(kind));

        Assert.True(Map(page).IsVisible);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture($"{kind}-map-window.png");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Kinds))]
    public void TheMapFitsTheBigPanel(string kind)
    {
        var (page, window) = Open(For(kind), height: 640);

        Assert.InRange(Map(page).Bounds.Width, RouteMapView.SmallestSide, RouteMapView.LargestSide);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture($"{kind}-map-panel.png");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Kinds))]
    public void APlanSavedWithoutPositionsShowsNoMap(string kind)
    {
        var (page, _) = Open(For(kind, positioned: false));

        Assert.False(Map(page).IsVisible);
    }
}
