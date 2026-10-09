using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Loadout;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Fleet › Crew: each hired pilot's rank, fighter-bay duty and posted ship (#564).</summary>
public class TheCrewPageListsEveryHiredPilotTests
{
    private sealed record Surface(Window Window, PanelView Panel, GameStateStore Store);

    private static readonly string[] Journal =
    [
        """{"timestamp":"2026-10-05T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
        """{"timestamp":"2026-10-05T09:00:01Z","event":"Loadout","Ship":"anaconda","Modules":[],"ShipName":"Sacred Wings","ShipIdent":"SW-01"}""",
        """{"timestamp":"2026-10-05T09:01:00Z","event":"CrewHire","Name":"Samira Voss","CrewID":1,"CombatRank":"Dangerous"}""",
        """{"timestamp":"2026-10-05T09:01:01Z","event":"CrewHire","Name":"Oren Make","CrewID":2,"CombatRank":"Master"}""",
        """{"timestamp":"2026-10-05T09:02:00Z","event":"CrewAssign","Name":"Samira Voss","CrewID":1,"Role":"Active"}""",
    ];

    private static Surface Open(IEnumerable<string> journal)
    {
        var root = TempFolders.Create("d47-crew-tests");
        var store = new GameStateStore();

        foreach (var line in journal)
        {
            Apply(store, line);
        }

        CommanderGameState? State() => store.Active;

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            State);
        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance), checklists, State);
        var kit = new OnFootPlanService(
            new OnFootBuildStore(Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance), checklists, State);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableLoadout(ships, checklists, State, kit);
        panel.EnableSearch();

        var window = new Window { Content = panel, Width = 1280, Height = 1100 };
        window.Show();

        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();
        panel.Nav.SelectRoot(CrewPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, store);
    }

    private static void Apply(GameStateStore store, string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        store.Apply(parsed!);
    }

    private static CrewPage Page(PanelView panel) => panel.GetVisualDescendants().OfType<CrewPage>().Single();

    private static List<string> Text(Control page) =>
        [.. page.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void EachPilotShowsRankDutyAndPosting()
    {
        var surface = Open(Journal);
        var text = Text(Page(surface.Panel));

        Assert.Contains("CREW", text);
        Assert.Contains("Samira Voss", text);
        Assert.Contains("DANGEROUS", text);
        Assert.Contains("MASTER", text);
        Assert.Contains(CrewPage.OnDuty, text);
        Assert.Contains(CrewPage.OffDuty, text);
        Assert.Contains("SACRED WINGS", text);

        surface.Window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void DutyIsYellowAndAnUnpostedPilotIsGrey()
    {
        using var look = AppLook.Put();

        var surface = Open(Journal);
        var page = Page(surface.Panel);

        Assert.Equal(Brush(ThemeManager.YellowKey), Ink(page, CrewPage.OnDuty));
        Assert.Equal(Brush(ThemeManager.GreyKey), Ink(page, CrewPage.OffDuty));
        Assert.Equal(Brush(ThemeManager.GreyKey), Ink(page, CrewPage.NotPosted));

        surface.Window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void AHireRedrawsTheOpenPage()
    {
        var surface = Open(Journal.Take(1));

        Assert.Contains(CrewPage.Unseen, Text(Page(surface.Panel)));

        foreach (var line in Journal.Skip(1))
        {
            Apply(surface.Store, line);
        }

        Assert.True(surface.Panel.TickLoadout());
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Oren Make", Text(Page(surface.Panel)));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void HelpSaysCrewHasAPageOnThePanel()
    {
        var crew = CrewCapability.Create(() => null);

        Assert.True(crew.Display.ShowOnPanel);
        Assert.Equal("Crew", crew.Display.PanelTitle);
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void TheCrewPageIsCaptured()
    {
        using var look = AppLook.Put();

        var surface = Open(Journal);
        var path = "fleet-crew.png";

        using (var frame = surface.Window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture(path);
        }

        surface.Window.Close();
    }

    private static IBrush? Ink(CrewPage page, string text) =>
        page.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == text).Foreground;

    private static IBrush? Brush(string key) =>
        Avalonia.Application.Current!.Resources.TryGetResource(key, null, out var brush) ? brush as IBrush : null;
}
