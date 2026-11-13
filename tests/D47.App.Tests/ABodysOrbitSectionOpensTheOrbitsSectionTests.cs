using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Knowledge;
using Xunit;
using static D47.App.Tests.UiWait;

namespace D47.App.Tests;

/// <summary>A body page's Orbit section has a SHOW IN ORBITS tile that returns to the System page on its Orbits section, focused on the body's nearest parent (#2392).</summary>
[Trait("Category", "Integration")]
public sealed class ABodysOrbitSectionOpensTheOrbitsSectionTests
{
    private const long Ltt7786 = 633608311522;

    private static (PanelView Panel, Window Window, SystemBodyPage Page) BodyPage(string name)
    {
        var (panel, window, _) = TheSystemPageDrawsItsOrbitsTests.Shown();
        var profile = TheSystemPageListsItsStationsTests.Ltt7786Profile();
        var body = profile.Bodies.Single(candidate => candidate.Name == name);

        panel.Nav.Drill(PanelTab.Search, SystemBodyPage.Crumb(Ltt7786, body));
        Until(() => panel.GetVisualDescendants().OfType<SystemBodyPage>().Any(page => page.Body is not null));

        var page = panel.GetVisualDescendants().OfType<SystemBodyPage>().Single();
        page.Show(SystemBodyPage.Section.Orbit);
        Dispatcher.UIThread.RunJobs();

        return (panel, window, page);
    }

    private static Button? Tile(Control root) =>
        root.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == SystemBodyPage.ShowInOrbitsName);

    private static List<string> Texts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Empty)];

    private static List<string> RowNames(Control root) =>
        [.. root.GetVisualDescendants().OfType<Control>().Where(control => control.Classes.Contains(StarSystemPage.OrbitRowClass)).Select(row => AutomationProperties.GetName(row)!)];

    private static List<string> Crumbs(Control root) =>
        [.. root.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains(StarSystemPage.OrbitCrumbClass)).Select(crumb => (string)crumb.Content!)];

    [AvaloniaFact]
    public void ABodyAroundABarycentreOpensOrbitsFocusedOnTheBarycentre()
    {
        var (panel, window, page) = BodyPage("LTT 7786 4");

        Assert.NotNull(Tile(page));
        Assert.Contains("Opens the System page's Orbits section on barycentre 14, where 4 is drawn.", Texts(page));

        TheSystemPageDrawsItsOrbitsTests.Press(Tile(page)!);
        Until(() => RowNames(panel).Count > 0);

        Assert.Equal([StarSystemPage.RootKey], panel.Nav.TrailOf(PanelTab.Search).Select(crumb => crumb.Key));
        Assert.Equal(["LTT 7786", "Barycentre 14"], Crumbs(panel));
        Assert.Equal(["LTT 7786 4", "LTT 7786 5"], RowNames(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void AMoonOpensOrbitsFocusedOnItsPlanet()
    {
        var (panel, window, page) = BodyPage("LTT 7786 6 a");

        Assert.Contains("Opens the System page's Orbits section on LTT 7786 6, where 6 a is drawn.", Texts(page));

        TheSystemPageDrawsItsOrbitsTests.Press(Tile(page)!);
        Until(() => RowNames(panel).Count > 0);

        Assert.Equal(["LTT 7786", "LTT 7786 6"], Crumbs(panel));
        Assert.Contains("LTT 7786 6 a", RowNames(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void TheRootBodyHasNoTile()
    {
        var (_, window, page) = BodyPage("LTT 7786");

        Assert.Null(Tile(page));

        window.Close();
    }
}
