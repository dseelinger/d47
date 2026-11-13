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
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A ship's page draws a PLAN section while its plan or deliveries still need something (#565, #2405).</summary>
public class AShipPageShowsWhatItsPlansStillNeedTests
{
    private static readonly ChecklistScope Reaper = ChecklistScope.Ship(12);

    private static ShipsMode Fleet(bool planned, bool engineer = true, string modules = "[]", int grade = 1) =>
        Fleet(planned ? new SlotPlan("MainEngines", "Dirty Drive Tuning", grade) : null, engineer, modules).Mode;

    private static (ShipsMode Mode, ShipPlanService Ships, ShipBuild Build) Fleet(
        SlotPlan? plan,
        bool engineer = true,
        string modules = "[]")
    {
        var paths = new D47.Core.AppPaths(TestSurface.MemoryFolder("d47-ship-plan-tests"));


        var store = new GameStateStore();

        var loadout =
            """{"timestamp":"2026-08-19T09:00:00Z","event":"Loadout","Ship":"cobramkv","ShipID":12,"ShipName":"Reaper","CargoCapacity":8,"Modules":MODULES}"""
                .Replace("MODULES", modules, StringComparison.Ordinal);

        var engineers = engineer
            ? """{"timestamp":"2026-08-19T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":5}]}"""
            : """{"timestamp":"2026-08-19T09:00:00Z","event":"EngineerProgress","Engineers":[]}""";

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-19T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     loadout,
                     engineers,
                     """{"timestamp":"2026-08-19T09:00:00Z","event":"ColonisationConstructionDepot","MarketID":3960809986,"ConstructionProgress":0.1,"ConstructionComplete":false,"ConstructionFailed":false,"ResourcesRequired":[{"Name":"$steel_name;","Name_Localised":"Steel","RequiredAmount":500,"ProvidedAmount":80,"Payment":3239}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var live = store.Active!;

        var checklistStore = new ChecklistStore(
            Path.Combine(paths.Data, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance);

        var checklists = new ChecklistService(
            checklistStore,
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => live);

        var ships = TestSurface.ShipPlans(checklists, paths.Data, () => live);

        var build = ships.BuildFor(12, "cobramkv", "Reaper");

        if (plan is not null)
        {
            ships.Plan(build.Id, plan);
        }

        return (new ShipsMode(ships, checklists, () => live), ships, build);
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
            "What this ship's plan still needs · 1 slot · 1 construction delivery",
            PlanSection.AsideText(plan));
        Assert.Equal("✓ MET", PlanSection.ShortText(new ShortfallRow("Steel", D47.Core.Knowledge.MaterialLedger.Cargo, null, 5, 5)));
        Assert.Equal("Need 420 · cap 8 · 53 trips", PlanSection.TripDetail(plan.Trips.Single(trip => trip.Kind == ShortfallTripKind.Cargo)));
    }

    [Fact]
    public void ThePlanIsCostedWithoutPuttingTheBuildOnTheChecklist()
    {
        var (mode, _, _) = Fleet(new SlotPlan("MainEngines", "Dirty Drive Tuning", 1));

        var plan = Assert.IsType<PlanShortfall>(mode.Plan(Assert.Single(mode.Items()).Key));

        Assert.Equal(1, plan.PlanCount);
        Assert.Contains(plan.Rows, row => row.Name.Contains("Legacy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReplanningASlotChangesTheSectionAtOnce()
    {
        var (mode, ships, build) = Fleet(new SlotPlan("MainEngines", "Dirty Drive Tuning", 1));
        var key = Assert.Single(mode.Items()).Key;

        Assert.DoesNotContain(mode.Plan(key)!.Rows, row => row.Name == "Chromium");

        ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 3));

        Assert.Contains(mode.Plan(key)!.Rows, row => row.Name == "Chromium");

        ships.Clear(build.Id, "MainEngines");

        Assert.DoesNotContain(mode.Plan(key)!.Rows, row => row.Name == "Chromium");
        Assert.Equal(0, mode.Plan(key)!.PlanCount);
        Assert.Equal(1, mode.Plan(key)!.DeliveryCount);
    }

    [Fact]
    public void AGradeAnUnlockedEngineerOffersHasNoEngineerRankNoticeOnEitherPage()
    {
        var (mode, _, _) = Fleet(new SlotPlan("MainEngines", "Dirty Drive Tuning", 3), engineer: true);
        var key = Assert.Single(mode.Items()).Key;

        Assert.Empty(mode.Plan(key)!.Blocks);
        Assert.DoesNotContain(mode.Planned(key, "MainEngines"), line => line.Text.Contains("no engineer you have unlocked"));
    }

    [Fact]
    public void AGradeNoUnlockedEngineerOffersHasTheNoticeOnBothPages()
    {
        var (mode, _, _) = Fleet(new SlotPlan("MainEngines", "Dirty Drive Tuning", 3), engineer: false);
        var key = Assert.Single(mode.Items()).Key;

        Assert.Single(mode.Plan(key)!.Blocks);
        Assert.Contains(mode.Planned(key, "MainEngines"), line => line.Text.Contains("no engineer you have unlocked"));
    }

    [Fact]
    public void WhatTheFittedModuleAlreadyHasIsLeftOutOfBothPages()
    {
        const string fitted =
            """[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Priority":1,"Health":1.0,"Engineering":{"Engineer":"Felicity Farseer","EngineerID":300100,"BlueprintID":1,"BlueprintName":"Engine_Dirty","Level":3,"Quality":1.0,"Modifiers":[]}}]""";

        var (mode, _, _) = Fleet(new SlotPlan("MainEngines", "Dirty Drive Tuning", 3), engineer: false, modules: fitted);
        var key = Assert.Single(mode.Items()).Key;

        var plan = mode.Plan(key)!;

        Assert.Equal(0, plan.PlanCount);
        Assert.Empty(plan.Blocks);
        Assert.All(plan.Rows, row => Assert.NotNull(row.Site));
        Assert.DoesNotContain(mode.Planned(key, "MainEngines"), line => line.Text.Contains("no engineer you have unlocked"));
        Assert.DoesNotContain(mode.Planned(key, "MainEngines"), line => line.Text == "What it costs");
    }

    [Fact]
    public void AHullYouDoNotOwnIsNotCostedOnItsPage()
    {
        var (mode, ships, _) = Fleet(plan: null, engineer: false);
        var intended = ships.Intend("python")!;
        ships.Plan(intended.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 3));

        var plan = mode.Plan(intended.Id);

        Assert.True(plan is null || (plan.PlanCount == 0 && plan.Blocks.Count == 0));
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
