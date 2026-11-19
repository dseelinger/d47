using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>The catalogue pages as the panel routes them: a crumb per level, BACK and the crumbs, and one filter shared by every level (#2608).</summary>
public class TheCodexCatalogueKeepsItsFilterAcrossLevelsTests
{
    private static CommanderGameState State()
    {
        var at = GalacticRegions.LabelAt(18)!.Value;

        return JournalScript.Commander(
            "F735466",
            "John Deparagon",
            "{\"timestamp\":\"2026-11-01T09:00:00Z\",\"event\":\"FSDJump\",\"StarSystem\":\"Jump\",\"SystemAddress\":77,\"StarPos\":["
            + at.X.ToString("R", CultureInfo.InvariantCulture) + ",0," + at.Z.ToString("R", CultureInfo.InvariantCulture) + "]}");
    }

    private static (PanelView Panel, Window Window) Shown(CommanderGameState state)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableCodex(() => state, () => HistoryState.Done, () => DateTimeOffset.UtcNow);

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Commander;
        Assert.True(panel.Nav.GoTo(PanelTab.Commander, LedgersPage.Root, new NavCrumb(CodexPage.PageKey, "Codex"), CodexPage.CatalogueCrumb()));
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    private static CodexCataloguePage Showing(Control panel) => panel.GetVisualDescendants().OfType<CodexCataloguePage>().Single();

    private static void Press(Control root, string name)
    {
        root.GetVisualDescendants().OfType<Button>().First(button => button.Name == name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Pick(Control page, string token, int option)
    {
        Press(page, "CodexCatalogueToken." + token);
        page.GetVisualDescendants().OfType<Segment>().Single(segment => AutomationProperties.GetName(segment) == token + " options")
            .GetVisualDescendants().OfType<RadioButton>().ElementAt(option).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private static string Token(Control page, string token) =>
        page.GetVisualDescendants().OfType<Button>().First(button => button.Name == "CodexCatalogueToken." + token)
            .GetVisualDescendants().OfType<TextBlock>().First().Text ?? string.Empty;

    [AvaloniaFact]
    public void AFamilyRowOpensItsPageAndTheCrumbsNameEachLevelAndReturn()
    {
        var (panel, window) = Shown(State());

        Assert.StartsWith("Every kind of object in the Codex.", Showing(panel).Summary, StringComparison.Ordinal);

        Press(Showing(panel), "CodexGroup.terrestrial_planets");
        Press(Showing(panel), "CodexGroup.planet.rocky_body");

        Assert.Equal("The kinds under Rocky body.", Showing(panel).Summary);
        Assert.Equal(["Codex", "Catalogue", "Terrestrial planets", "Rocky body"], panel.Nav.Trail.Skip(1).Select(crumb => crumb.Word));

        Assert.True(panel.Nav.Back());
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("The kinds under Terrestrial planets.", Showing(panel).Summary);

        Assert.True(panel.Nav.JumpTo(2));
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("Every kind of object in the Codex.", Showing(panel).Summary, StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public void BothValuesAreStillSetAfterDrillingInAndBack()
    {
        var (panel, window) = Shown(State());

        Pick(Showing(panel), "Status", 1);
        Pick(Showing(panel), "Region", 1);

        Press(Showing(panel), "CodexGroup.stars");

        Assert.Equal("The kinds under Stars.", Showing(panel).Summary);
        Assert.Equal("MISSING", Token(Showing(panel), "Status"));
        Assert.Equal("INNER ORION SPUR", Token(Showing(panel), "Region"));

        Pick(Showing(panel), "Status", 2);

        Assert.True(panel.Nav.Back());
        Dispatcher.UIThread.RunJobs();

        Assert.StartsWith("Every kind of object in the Codex.", Showing(panel).Summary, StringComparison.Ordinal);
        Assert.Equal("ON RECORD", Token(Showing(panel), "Status"));
        Assert.Equal("INNER ORION SPUR", Token(Showing(panel), "Region"));

        window.Close();
    }
}
