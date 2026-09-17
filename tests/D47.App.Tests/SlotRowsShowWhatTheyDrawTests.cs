using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The megawatts a slot row carries beside what is fitted and what is planned (#252).</summary>
public class SlotRowsShowWhatTheyDrawTests
{
    private sealed record Surface(Window Window, PanelView Panel, ShipPlanService Ships);

    private static Surface Open()
    {
        var root = TempFolders.Create("d47-slot-draw-tests");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);

        var state = Flying();
        var ships = new ShipPlanService(store, checklists, () => state);

        new Theming.ThemeManager(Application.Current!, NullLogger<Theming.ThemeManager>.Instance)
            .Apply(ThemeCatalog.Elite);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => state);

        var window = new Window { Content = panel, Width = 900, Height = 700 };

        window.Show();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, ships);
    }

    /// <summary>A Cobra Mk V with an engine, a drive and a hull with nothing to draw.</summary>
    private static CommanderGameState Flying()
    {
        var store = new GameStateStore();

        var lines = new List<string>
        {
            """{"timestamp":"2026-08-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            """{"timestamp":"2026-08-20T09:00:00Z","event":"Loadout","Ship":"cobramkv","ShipID":12,"ShipName":"Predestinatio","MaxJumpRange":30.0,"CargoCapacity":0,"UnladenMass":190.0,"FuelCapacity":{"Main":16.0,"Reserve":0.49},"Modules":[{"Slot":"PowerPlant","Item":"int_powerplant_size4_class5","On":true,"Priority":1,"Health":1.0},{"Slot":"FrameShiftDrive","Item":"int_hyperdrive_overcharge_size4_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"MainEngines","Item":"int_engine_size4_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"Armour","Item":"cobramkv_armour_grade1","On":true,"Priority":1,"Health":1.0},{"Slot":"FuelTank","Item":"int_fueltank_size4_class3","On":true,"Priority":1,"Health":1.0}]}""",
        };

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static Button Row(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<Button>()
            .First(button => AutomationProperties.GetName(button) == label
                             || button.GetVisualDescendants().OfType<TextBlock>()
                                 .Any(text => text.Text == label));

    private static void OpenTheShip(Surface surface)
    {
        Row(surface.Panel, "Predestinatio (Cobra MkV)")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Dispatcher.UIThread.RunJobs();
    }

    private static string? DrawOn(PanelView panel, string slot)
    {
        var row = Row(panel, slot);

        return row.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text)
            .LastOrDefault(text => text is { Length: > 0 } && text.EndsWith("MW", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void AFittedSlotShowsWhatItDraws()
    {
        var surface = Open();

        OpenTheShip(surface);

        Assert.Equal("0.45 MW", DrawOn(surface.Panel, "Frame Shift Drive"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void AModuleThatDrawsNothingShowsNoFigureAtAll()
    {
        var surface = Open();

        OpenTheShip(surface);

        Assert.Null(DrawOn(surface.Panel, "Armour"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void APlannedRollShowsTheModelledFigureInTheInfoTone()
    {
        var surface = Open();

        OpenTheShip(surface);

        var build = surface.Ships.Fleet().First(entry => entry.Build is not null).Build!;

        surface.Ships.Plan(build.Id, new SlotPlan("FrameShiftDrive", "Increased FSD Range", 5));
        Dispatcher.UIThread.RunJobs();

        var reading = DrawOn(surface.Panel, "Frame Shift Drive");

        Assert.NotNull(reading);
        Assert.StartsWith("~ ", reading, StringComparison.Ordinal);
        Assert.NotEqual("~ 0.45 MW", reading);

        surface.Window.Close();
    }
}
