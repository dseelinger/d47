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
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>An exobiology route values each body by its listed species and lists the richest body first (#2628).</summary>
public class TheExobiologyRouteShowsOnlyItsListedSpeciesTests
{
    private static ExobiologyBody Body(string name, long landmark, params long[] values) =>
        new(name, "Rocky body")
        {
            DistanceToArrival = 100,
            LandmarkValue = landmark,
            Species = [.. values.Select((value, index) => new ExobiologySpecies("Genus", $"{name} species {index + 1}", 1, value))],
        };

    private static StoredRoutePlan Plan() => new()
    {
        Kind = RoutePlanKind.Exobiology,
        Headline = "Exobiology",
        Exobiology = new ExobiologyRoute(
        [
            new ExobiologyStop(
                "Angi",
                2,
                [
                    Body("Angi B 4", 33_952_000, 19_010_800),
                    Body("Angi A 2", 25_869_800, 12_934_900, 12_934_900),
                    Body("Angi B 5", 35_213_600, 19_010_800, 16_202_800),
                ]),
            new ExobiologyStop("Prae", 3, [Body("Prae 3 a", 40_000_000, 19_010_800, 16_202_800)]),
        ]),
    };

    private static Window Show(StoredRoutePlan plan)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);
        Application.Current!.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml") });

        var window = new Window
        {
            Content = new RoutePlanResultPage(plan),
            Width = 900,
            Height = 900,
            Background = (IBrush?)Application.Current.Resources[ThemeManager.BgKey],
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static List<string> Lines(Window window) =>
        [.. window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void EachBodyShowsTheSumOfItsListedSpeciesRichestFirst()
    {
        var window = Show(Plan());

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("exobiology-route-listed-species.png");
        }

        var bodies = Lines(window).Where(line => line.StartsWith("Body ", StringComparison.Ordinal)).ToList();

        Assert.Equal(
            [
                "Body B 5 — Rocky body, 100 ls, 35,213,600 cr",
                "Body A 2 — Rocky body, 100 ls, 25,869,800 cr",
                "Body B 4 — Rocky body, 100 ls, 19,010,800 cr",
                "Body 3 a — Rocky body, 100 ls, 35,213,600 cr",
            ],
            bodies);
    }

    [AvaloniaFact]
    public void TheHeadlineTotalIsTheSumOfTheListedSpecies()
    {
        Assert.Contains("2 stops, 5 jumps, 115,307,800 credits of biology.", Lines(Show(Plan())));
    }

    [AvaloniaFact]
    public void NoLineCountsSpeciesUnderTheMinimum()
    {
        Assert.DoesNotContain(Lines(Show(Plan())), line => line.Contains("under the minimum", StringComparison.Ordinal));
    }
}
