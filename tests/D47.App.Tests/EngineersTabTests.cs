using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Engineers tab (list.md Phase 28).
/// <para>
/// Two roots and one level: the <b>Directory</b>, ordered by what the Commander can act on today,
/// and the <b>Route</b>, which ranks the ways in and shows its work. The claims asserted here are
/// the item's own — that the ordering is by reach rather than by name, that the summary carries
/// the count belonging to the Loadout tab, and that the ranking never appears without the working
/// that produced it.
/// </para>
/// </summary>
public class EngineersTabTests
{
    private sealed record Surface(
        Window Window,
        PanelView Panel,
        ShipPlanService Ships,
        ChecklistService Checklists);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>A Commander in Sol, flying 30 ly a jump, unlocked with Liz Ryder alone.</summary>
    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Sol","StarPos":[0.0,0.0,0.0],"Docked":true,"StationName":"Abraham Lincoln"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","MaxJumpRange":30.0,"Modules":[]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Liz Ryder","EngineerID":300080,"Progress":"Unlocked","Rank":5}]}""",
                 })
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static Surface Open(bool planned = true)
    {
        var root = TempFolders.Create("d47-engineers-tab-tests");
        var state = State();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        var builds = new ShipBuildStore(
            Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);

        if (planned)
        {
            builds.Save([
                new ShipBuild("ship-1", "python", 12, "Bad Idea",
                    [new SlotPlan("FrameShiftDrive", "Increased FSD Range", 3)]),
            ]);
        }

        var kit = new OnFootBuildStore(
            Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance);

        var ships = new ShipPlanService(builds, checklists, () => state);
        var onFoot = new OnFootPlanService(kit, checklists, () => state);
        var unlocks = new EngineerPlanService(builds, kit, checklists, () => state);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableEngineers(unlocks, ships, () => state, onFoot);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Engineers;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, ships, checklists);
    }

    private static IReadOnlyList<string> Text(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static Button Press(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text is { } said && said.Contains(label, StringComparison.Ordinal)));

    /// <summary>
    /// Two roots of one tab, on the same reading as Loadout's three: the Directory and the Route
    /// are two answers to <em>which engineer</em>, not two destinations.
    /// </summary>
    [AvaloniaFact]
    public void TheTabHasTwoModes()
    {
        var surface = Open();

        Assert.Equal(
            ["Directory", "Route"],
            surface.Panel.Nav.Roots(PanelTab.Engineers).Select(root => root.Word));

        surface.Window.Close();
    }

    /// <summary>
    /// The directory leads with what can be acted on today, and its summary carries the count that
    /// belongs to the other tab: what is waiting on a person rather than on materials.
    /// </summary>
    [AvaloniaFact]
    public void TheDirectoryLeadsWithWhatIsReachable()
    {
        var surface = Open();
        var shown = Text(surface.Panel);

        Assert.Contains(shown, line => line.Contains("1 of 38 unlocked", StringComparison.Ordinal));
        Assert.Contains(shown, line => line.Contains(
            "waiting on somebody you have not unlocked", StringComparison.Ordinal));

        Assert.Contains("You can go and get these now", shown);
        Assert.Contains("Already yours", shown);
        Assert.Contains("Behind somebody else", shown);

        // The heading that can be acted on comes before the one that cannot.
        Assert.True(
            shown.ToList().IndexOf("You can go and get these now")
            < shown.ToList().IndexOf("Behind somebody else"));

        // And the say-line, on this level as on every other.
        Assert.Contains(shown, line => line.StartsWith("Say:", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// Distances are on the page, which is the whole point of the coordinates shipping in the
    /// table: this is arithmetic against where the Commander is, not a lookup.
    /// </summary>
    [AvaloniaFact]
    public void EveryRowSaysHowFarAndHowManyJumps()
    {
        var surface = Open();

        Assert.Contains(
            Text(surface.Panel),
            line => line.Contains("ly, about", StringComparison.Ordinal)
                    && line.Contains("jump", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// Drilling a row opens the one engineer behind it, with the way in spelled out stop by stop
    /// rather than summarised — a summary is what a Commander cannot act on.
    /// </summary>
    [AvaloniaFact]
    public void DrillingOpensOneEngineer()
    {
        var surface = Open();

        Press(surface.Panel, "Felicity Farseer").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            ["Directory", "Felicity Farseer"],
            surface.Panel.Nav.Trail.Select(crumb => crumb.Word));

        var shown = Text(surface.Panel);

        Assert.Contains("Farseer Inc in Deciat", shown);
        Assert.Contains("The way in", shown);
        Assert.Contains(shown, line => line.Contains("Frame Shift Drive to 5", StringComparison.Ordinal));

        // Frontier's own sentence, printed rather than summarised away.
        Assert.Contains(shown, line => line.Contains("exploration rank Scout", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// The Route shows its work. A ranking nobody can inspect is an oracle, so the sentence that
    /// produced each place in the order is on the page beside it, and so is what it was measured
    /// from.
    /// </summary>
    [AvaloniaFact]
    public void TheRouteShowsItsWork()
    {
        var surface = Open();

        Assert.True(surface.Panel.Nav.SelectRoot(EngineersPages.RouteRoot));
        Dispatcher.UIThread.RunJobs();

        var shown = Text(surface.Panel);

        Assert.Contains(shown, line => line.Contains("Measured from Sol", StringComparison.Ordinal));
        Assert.Contains(shown, line => line.Contains("planned thing covered", StringComparison.Ordinal));
        Assert.Contains(shown, line => line.Contains("hand over:", StringComparison.Ordinal));
        Assert.Contains(shown, line => line.Contains("first:", StringComparison.Ordinal));
        Assert.Contains(shown, line => line.Contains("covers:", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>
    /// Promotion offers the whole chain, and offers it — the checklist gains nothing until the
    /// Commander accepts, which is the same path every other plan takes.
    /// </summary>
    [AvaloniaFact]
    public void PromotingOffersTheChain()
    {
        var surface = Open();

        surface.Panel.Nav.SelectRoot(EngineersPages.RouteRoot);
        Dispatcher.UIThread.RunJobs();

        Press(surface.Panel, "Put this route on my checklist")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Dispatcher.UIThread.RunJobs();

        var proposal = Assert.Single(surface.Checklists.Proposals.Pending);

        Assert.Equal(ProposalKind.Plan, proposal.Kind);
        Assert.All(proposal.Items, item =>
            Assert.Equal(ChecklistIntentKind.EngineerAccess, item.Intent!.Kind));

        // Nothing on the list itself until it is accepted.
        Assert.Empty(surface.Checklists.Document.Items);

        surface.Window.Close();
    }

    /// <summary>
    /// With nothing planned the page still answers, and says so rather than showing an empty
    /// ranking — who is nearest is the right answer to a question with no plans behind it.
    /// </summary>
    [AvaloniaFact]
    public void WithNothingPlannedItStillAnswers()
    {
        var surface = Open(planned: false);
        var shown = Text(surface.Panel);

        Assert.Contains(shown, line => line.Contains("1 of 38 unlocked", StringComparison.Ordinal));
        Assert.DoesNotContain(shown, line => line.Contains("waiting on somebody", StringComparison.Ordinal));

        surface.Window.Close();
    }
}
