using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// With the setting on, a derived item that reaches Done removes itself rather than staying ticked; an
/// authored line is never touched, on or off (#255).
/// </summary>
public class AFulfilledDerivedItemRemovesItselfTests
{
    private const int ShipId = 21;

    /// <summary>The key <see cref="EngineeringPlan"/> mints for the blueprint intent on MainEngines.</summary>
    private const string BlueprintKey = "bp/mainengines";

    private sealed record Bench(GameStateStore Game, ChecklistService Checklists, ShipPlanService Ships);

    private static Bench Set(TempInstall install, bool removeFulfilled)
    {
        var game = new GameStateStore();

        game.Apply(Event("""{"timestamp":"2026-09-10T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        game.Apply(Loadout());

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(install.Root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => game.Active,
            removeFulfilled: () => removeFulfilled);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(install.Root, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => game.Active);

        var build = ships.BuildFor(ShipId, "python");

        ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));
        ships.Promote(build.Id);

        checklists.AddNote(ChecklistScope.Ship(ShipId), "Buy a paint job");

        return new Bench(game, checklists, ships);
    }

    [Fact]
    public void ADerivedItemThatReachesDoneIsRemovedWithTheSettingOn()
    {
        using var install = new TempInstall();
        var (game, checklists, _) = Set(install, removeFulfilled: true);

        game.Apply(Engineered());
        checklists.Poll();

        Assert.DoesNotContain(checklists.Document.Items, item => item.Key == BlueprintKey);
    }

    [Fact]
    public void ADerivedItemThatReachesDoneIsKeptTickedWithTheSettingOff()
    {
        using var install = new TempInstall();
        var (game, checklists, _) = Set(install, removeFulfilled: false);

        game.Apply(Engineered());
        checklists.Poll();

        var derived = Assert.Single(checklists.Document.Items, item => item.Key == BlueprintKey);

        Assert.Equal(ChecklistState.Done, derived.State);
    }

    [Fact]
    public void AnAuthoredLineTickedByHandStaysOnTheListWithTheSettingOn()
    {
        using var install = new TempInstall();
        var (game, checklists, _) = Set(install, removeFulfilled: true);

        var note = checklists.Document.Items.Single(item => item.Kind == ChecklistItemKind.Authored);
        checklists.Complete(note.Id);

        game.Apply(Engineered());
        checklists.Poll();

        var kept = checklists.Document.Find(note.Id);

        Assert.NotNull(kept);
        Assert.Equal(ChecklistState.Done, kept!.State);
    }

    private static JournalEvent Loadout() => Event(
        $$"""
        {"timestamp":"2026-09-10T09:05:00Z","event":"Loadout","Ship":"python","ShipID":{{ShipId}},
         "Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true}]}
        """);

    private static JournalEvent Engineered() => Event(
        """
        {"timestamp":"2026-09-10T09:10:00Z","event":"Loadout","Ship":"python","ShipID":SHIPID,
         "Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,
           "Engineering":{"BlueprintName":"Engine_Dirty","Level":5,"Quality":1.0,
                          "Engineer":"Felicity Farseer","EngineerID":300100}}]}
        """.Replace("SHIPID", ShipId.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
