using Avalonia;
using Avalonia.Controls;
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

/// <summary>Exobiology and Road to Riches stops show the straight-line leg, body distance and unlisted species value (#1800).</summary>
public class ARoutePlanStopShowsItsDistancesTests
{
    private static readonly StarPosition Origin = new(0, 0, 0);

    private static List<string> Lines(StoredRoutePlan plan)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        Application.Current!.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        });

        var window = new Window { Content = new RoutePlanResultPage(plan), Width = 1000, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return [.. window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];
    }

    private static StoredRoutePlan Riches(RouteEndpoint? start, StarPosition? first, StarPosition? second, double? ls) => new()
    {
        Kind = RoutePlanKind.Riches,
        Headline = "Road to Riches",
        Riches = new RichesRoute(
        [
            new RichesStop("Alpha", 1, [new RichesBody("Alpha 1", "Rocky body") { MappingValue = 1_000, DistanceToArrival = ls }]) { Position = first },
            new RichesStop("Beta", 2, []) { Position = second },
        ])
        {
            Start = start,
        },
    };

    private static StoredRoutePlan Exobiology(RouteEndpoint? start, StarPosition? first, StarPosition? second, double? ls, long landmark) => new()
    {
        Kind = RoutePlanKind.Exobiology,
        Headline = "Exobiology",
        Exobiology = new ExobiologyRoute(
        [
            new ExobiologyStop(
                "Alpha",
                6,
                [
                    new ExobiologyBody("Alpha 1", "Rocky body")
                    {
                        DistanceToArrival = ls,
                        LandmarkValue = landmark,
                        Species = [new ExobiologySpecies("Bacterium", "Bacterium Aurasus", 1, 1_000)],
                    },
                ])
            {
                Position = first,
            },
            new ExobiologyStop("Beta", 2, []) { Position = second },
        ])
        {
            Start = start,
        },
    };

    [AvaloniaFact]
    public void ARichesStopShowsTheLegFromThePreviousPosition()
    {
        var lines = Lines(Riches(new RouteEndpoint("Home", Origin), new StarPosition(3, 4, 0), new StarPosition(3, 4, 12), 698));

        Assert.Contains("1 jump · 5 ly · 1 body", lines);
        Assert.Contains("2 jumps · 12 ly · 0 bodies", lines);
    }

    [AvaloniaFact]
    public void AnExobiologyStopShowsTheLegFromThePreviousPosition()
    {
        var lines = Lines(Exobiology(new RouteEndpoint("Home", Origin), new StarPosition(3, 4, 0), new StarPosition(3, 4, 12), 698, 1_000));

        Assert.Contains("6 jumps · 5 ly · 1 body", lines);
        Assert.Contains("2 jumps · 12 ly · 0 bodies", lines);
    }

    [AvaloniaFact]
    public void ALegIsLeftOutWhenEitherPositionIsMissing()
    {
        var lines = Lines(Exobiology(null, new StarPosition(3, 4, 0), null, 698, 1_000));

        Assert.Contains("6 jumps · 1 body", lines);
        Assert.Contains("2 jumps · 0 bodies", lines);

        lines = Lines(Riches(null, new StarPosition(3, 4, 0), new StarPosition(3, 4, 12), 698));

        Assert.Contains("1 jump · 1 body", lines);
        Assert.Contains("2 jumps · 12 ly · 0 bodies", lines);
    }

    [AvaloniaFact]
    public void ABodyShowsItsDistanceFromArrivalWhenKnown()
    {
        Assert.Contains("Alpha 1 — Rocky body, 698 ls, 1,000 cr", Lines(Riches(null, null, null, 698)));
        Assert.Contains("Alpha 1 — Rocky body, 1,000 cr", Lines(Riches(null, null, null, null)));
        Assert.Contains("Alpha 1 — Rocky body, 698 ls, 1,000 cr", Lines(Exobiology(null, null, null, 698, 1_000)));
        Assert.Contains("Alpha 1 — Rocky body, 1,000 cr", Lines(Exobiology(null, null, null, null, 1_000)));
    }

    [AvaloniaFact]
    public void ASpeciesTotalAboveTheListedSpeciesShowsTheDifference()
    {
        Assert.Contains("species under the minimum — 23,607,700 cr", Lines(Exobiology(null, null, null, 698, 23_608_700)));
        Assert.DoesNotContain(Lines(Exobiology(null, null, null, 698, 1_000)), line => line.StartsWith("species under", StringComparison.Ordinal));
    }
}
