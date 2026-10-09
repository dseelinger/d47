using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A ship's page draws a PLAN section while its plans or deliveries still need something (#565).</summary>
[Trait("Category", "Integration")]
public class AShipPageShowsWhatItsPlansStillNeedTests
{
    private static readonly ChecklistScope Reaper = ChecklistScope.Ship(12);

    private static ShipsMode Fleet(bool planned)
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-ship-plan-tests"));

        paths.EnsureCreated();

        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-19T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-19T09:00:00Z","event":"Loadout","Ship":"cobramkv","ShipID":12,"ShipName":"Reaper","CargoCapacity":8,"Modules":[]}""",
                     """{"timestamp":"2026-08-19T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":5}]}""",
                     """{"timestamp":"2026-08-19T09:00:00Z","event":"ColonisationConstructionDepot","MarketID":3960809986,"ConstructionProgress":0.1,"ConstructionComplete":false,"ConstructionFailed":false,"ResourcesRequired":[{"Name":"$steel_name;","Name_Localised":"Steel","RequiredAmount":500,"ProvidedAmount":80,"Payment":3239}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var live = store.Active!;

        var checklistStore = new ChecklistStore(
            Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance);

        if (planned)
        {
            checklistStore.Apply("F1", "Jameson", document => new ChecklistChange(
                document with
                {
                    Items =
                    [
                        .. document.Items,
                        .. EngineeringPlan.Items(
                            Reaper,
                            "cobramkv",
                            [new BuildRequest("MainEngines", "Dirty Drive Tuning", 1)]),
                    ],
                },
                true,
                string.Empty));
        }

        var checklists = new ChecklistService(
            checklistStore,
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => live);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => live);

        return new ShipsMode(ships, checklists, () => live);
    }

    [Fact]
    public void AShipWithAPlanAndADeliveryHasAPlanSection()
    {
        var mode = Fleet(planned: true);
        var reaper = Assert.Single(mode.Items());

        var plan = Assert.IsType<PlanShortfall>(mode.Plan(reaper.Key));

        Assert.Equal(1, plan.PlanCount);
        Assert.Equal(1, plan.DeliveryCount);
        Assert.Contains(plan.Rows, row => row.Name == "Steel");
        Assert.Contains(plan.Trips, trip => trip.Kind == ShortfallTripKind.Cargo);
    }

    [Fact]
    public void ThePlanSectionSaysWhatItCounts()
    {
        var mode = Fleet(planned: true);
        var reaper = Assert.Single(mode.Items());
        var plan = mode.Plan(reaper.Key)!;

        Assert.Equal(
            "What the live plans for this ship still need · 1 plan · 1 construction delivery",
            PlanSection.AsideText(plan));
        Assert.Equal("✓ MET", PlanSection.ShortText(new ShortfallRow("Steel", D47.Core.Knowledge.MaterialLedger.Cargo, null, 5, 5)));
        Assert.Equal("Need 420 · cap 8 · 53 trips", PlanSection.TripDetail(plan.Trips.Single(trip => trip.Kind == ShortfallTripKind.Cargo)));
    }

    [AvaloniaFact]
    public void ThePlanSectionIsCaptured()
    {
        using var kit = AppLook.ControlKit();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var mode = Fleet(planned: true);
        var plan = mode.Plan(Assert.Single(mode.Items()).Key)!;

        var window = new Window
        {
            Content = new ScrollViewer { Content = PlanSection.Build(plan) },
            Width = 1000,
            Height = 700,
            Background = (Avalonia.Media.IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        var path = "ship-plan-section.png";
        frame.SaveCapture(path);

        window.Close();
    }
}
