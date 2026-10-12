using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>The stations and factions tables on the star system page sort from their column headers (#1114).</summary>
[Trait("Category", "Integration")]
public sealed class TheStarSystemTablesSortFromTheirHeadersTests
{
    private static void Press(Control root, string name)
    {
        root.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static List<string> StationNames(Control root) =>
        [.. root.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Child is Grid { ColumnDefinitions.Count: 7 } && AutomationProperties.GetName(border) is { Length: > 0 })
            .Select(border => AutomationProperties.GetName(border)!)];

    private static List<string> FactionNames(Control root) =>
        [.. root.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Child is Grid { ColumnDefinitions.Count: 6 } && border.BorderThickness.Left == 3)
            .Select(border => border.GetVisualDescendants().OfType<TextBlock>().First().Text!)];

    private static int? Pad(StarSystemProfile profile, string station) =>
        (int?)profile.Stations.Single(candidate => candidate.Name == station).LargestPad;

    [AvaloniaFact]
    public void PressingPadOrdersStationsByLargestPadAndAgainReversesIt()
    {
        var profile = TheSystemPageListsItsStationsTests.Ltt7786Profile();
        var (panel, window, _) = TheSystemPageListsItsStationsTests.Shown();
        TheSystemPageListsItsStationsTests.Section(panel, "Stations");

        var defaultOrder = StationNames(panel);

        Press(panel, "StationHead.Pad");
        var largest = StationNames(panel).Select(name => Pad(profile, name)).ToList();
        Assert.Equal(2, largest[0]);
        Assert.Equal(largest.Where(pad => pad is not null).OrderByDescending(pad => pad), largest.Where(pad => pad is not null));
        Assert.True(largest.SkipWhile(pad => pad is not null).All(pad => pad is null));

        Press(panel, "StationHead.Pad");
        var smallest = StationNames(panel).Select(name => Pad(profile, name)).Where(pad => pad is not null).ToList();
        Assert.Equal(smallest.OrderBy(pad => pad), smallest);
        Assert.Equal(0, smallest[0]);

        Press(panel, "StationHead.Pad");
        Assert.Equal(defaultOrder, StationNames(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void TheStationsKindFilterKeepsTheChosenSort()
    {
        var (panel, window, _) = TheSystemPageListsItsStationsTests.Shown();
        TheSystemPageListsItsStationsTests.Section(panel, "Stations");

        Press(panel, "StationHead.Station");
        var sorted = StationNames(panel);
        Assert.Equal(sorted.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase), sorted);

        var sidebar = panel.GetVisualDescendants().OfType<Sidebar>().Single();
        var row = sidebar.Items[StationGroup.Kinds.Select(group => group.Key).ToList().IndexOf("outpost")];
        row.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(
            row,
            new Avalonia.Input.Pointer(0, Avalonia.Input.PointerType.Mouse, true),
            row,
            default,
            0,
            new Avalonia.Input.PointerPointProperties(Avalonia.Input.RawInputModifiers.LeftMouseButton, Avalonia.Input.PointerUpdateKind.LeftButtonPressed),
            Avalonia.Input.KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();

        var outposts = StationNames(panel);
        Assert.Equal(2, outposts.Count);
        Assert.Equal(outposts.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase), outposts);
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "STATION ▲");

        window.Close();
    }

    [AvaloniaFact]
    public void TheStationsTableCaptionStatesNoOrder()
    {
        var (panel, window, _) = TheSystemPageListsItsStationsTests.Shown();
        TheSystemPageListsItsStationsTests.Section(panel, "Stations");

        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "· nearest first");
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "ARRIVAL ▲");

        Press(panel, "StationHead.Pad");
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "ARRIVAL ▲");
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "PAD ▼");

        window.Close();
    }

    [AvaloniaFact]
    public void PressingFactionOrdersTheFactionsAToZ()
    {
        var (panel, window, _) = TheSystemPageListsItsStationsTests.Shown();

        var byInfluence = FactionNames(panel);
        Assert.True(byInfluence.Count > 1);
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "INFLUENCE ▼");

        Press(panel, "FactionHead.Faction");
        var named = FactionNames(panel);
        Assert.Equal(byInfluence.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase), named);

        Press(panel, "FactionHead.Faction");
        Assert.Equal(named.AsEnumerable().Reverse(), FactionNames(panel));

        Press(panel, "FactionHead.Faction");
        Assert.Equal(byInfluence, FactionNames(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void NoFactionsCaptionStatesAnOrder()
    {
        var (panel, window, _) = TheSystemPageListsItsStationsTests.Shown();

        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "Most influence first.");

        window.Close();
    }

    [AvaloniaFact]
    public void OpeningThePageAgainShowsTheDefaultOrders()
    {
        var (panel, window, _) = TheSystemPageListsItsStationsTests.Shown();
        var byInfluence = FactionNames(panel);

        Press(panel, "FactionHead.Faction");
        TheSystemPageListsItsStationsTests.Section(panel, "Stations");
        Press(panel, "StationHead.Pad");

        var page = panel.GetVisualDescendants().OfType<StarSystemPage>().Single();
        var host = (Decorator)page.Parent!;
        host.Child = null;
        Dispatcher.UIThread.RunJobs();
        host.Child = page;
        Dispatcher.UIThread.RunJobs();

        TheSystemPageListsItsStationsTests.Section(panel, "Stations");
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "ARRIVAL ▲");
        TheSystemPageListsItsStationsTests.Section(panel, "Overview");
        Assert.Equal(byInfluence, FactionNames(panel));
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "INFLUENCE ▼");

        window.Close();
    }

    [AvaloniaFact]
    public void EveryColumnOfTheThreeTablesSorts()
    {
        Assert.All(StarSystemPage.StationHeads, head => Assert.True(head.Sortable));
        Assert.All(StarSystemPage.FactionHeads, head => Assert.True(head.Sortable));
        Assert.All(StarSystemPage.NeighbourHeads, head => Assert.True(head.Sortable));
    }
}
