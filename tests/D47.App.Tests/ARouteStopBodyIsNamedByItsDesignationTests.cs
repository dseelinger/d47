using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
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

public class ARouteStopBodyIsNamedByItsDesignationTests
{
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

    private static StoredRoutePlan Riches(string system, string body) => new()
    {
        Kind = RoutePlanKind.Riches,
        Headline = "Road to Riches",
        Riches = new RichesRoute([new RichesStop(system, 1, [new RichesBody(body, "Rocky body") { MappingValue = 1_000, DistanceToArrival = 698 }])]),
    };

    private static StoredRoutePlan Exobiology(string system, string body) => new()
    {
        Kind = RoutePlanKind.Exobiology,
        Headline = "Exobiology",
        Exobiology = new ExobiologyRoute(
        [
            new ExobiologyStop(system, 1, [new ExobiologyBody(body, "Rocky body") { DistanceToArrival = 698, LandmarkValue = 1_000 }]),
        ]),
    };

    [AvaloniaFact]
    public void ARichesBodyLineStartsWithBodyAndItsDesignation()
    {
        Assert.Contains("Body 1 — Rocky body, 698 ls, 1,000 cr", Lines(Riches("Alpha", "Alpha 1")));
    }

    [AvaloniaFact]
    public void AnExobiologyBodyLineStartsWithBodyAndItsDesignation()
    {
        Assert.Contains("Body 1 — Rocky body, 698 ls, 1,000 cr", Lines(Exobiology("Alpha", "Alpha 1")));
        Assert.Contains("Body A 6 — Rocky body, 698 ls, 1,000 cr", Lines(Exobiology("Alpha", "Alpha A 6")));
    }

    [AvaloniaFact]
    public void ABodyNotNamedFromItsSystemKeepsItsFullName()
    {
        Assert.Contains("Earth — Rocky body, 698 ls, 1,000 cr", Lines(Riches("Sol", "Earth")));
        Assert.Contains("Earth — Rocky body, 698 ls, 1,000 cr", Lines(Exobiology("Sol", "Earth")));
    }

    [AvaloniaFact]
    public void ABodyNamedLikeItsSystemKeepsItsFullName()
    {
        Assert.Contains("Alpha — Rocky body, 698 ls, 1,000 cr", Lines(Riches("Alpha", "Alpha")));
        Assert.Contains("Alpha — Rocky body, 698 ls, 1,000 cr", Lines(Exobiology("Alpha", "Alpha")));
    }
}
