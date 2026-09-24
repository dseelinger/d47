using System.Globalization;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// Between <c>LoadGame</c> and <c>Loadout</c> the ship aboard has an id and no modules. A done item on it
/// stays done and is not announced again when the loadout arrives; a real change still gets through (#452).
/// </summary>
public class AnUnknownLoadoutNeverReopensADoneItemTests
{
    private const int ShipId = 37;

    private const string BlueprintKey = "bp/radar";

    [Fact]
    public void ADoneItemStaysDoneAndSilentAcrossAStart()
    {
        using var install = new TempInstall();
        Finish(install);

        var (game, checklists) = Start(install);

        Assert.Empty(checklists.Poll(announce: false));
        Assert.Equal(ChecklistState.Done, Radar(checklists).State);

        for (var tick = 0; tick < 3; tick++)
        {
            Assert.Empty(checklists.Poll());
            Assert.Equal(ChecklistState.Done, Radar(checklists).State);
        }

        game.Apply(Loadout("2026-09-24T13:15:47Z", grade: 5));

        Assert.Empty(checklists.Poll());
        Assert.Empty(checklists.Drain());
        Assert.Equal(ChecklistState.Done, Radar(checklists).State);
    }

    [Fact]
    public void ALowerGradeInTheArrivingLoadoutStillReopensTheItem()
    {
        using var install = new TempInstall();
        Finish(install);

        var (game, checklists) = Start(install);

        checklists.Poll(announce: false);
        checklists.Poll();

        game.Apply(Loadout("2026-09-24T13:15:47Z", grade: 3));

        var news = checklists.Poll();

        Assert.Equal(ChecklistState.Open, Radar(checklists).State);
        Assert.Contains(news, item => item.Key.StartsWith("checklist.undone.", StringComparison.Ordinal));
    }

    /// <summary>The previous session: the radar is planned at grade 5 and reaches it.</summary>
    private static void Finish(TempInstall install)
    {
        var game = new GameStateStore();

        game.Apply(Event("""{"timestamp":"2026-09-23T20:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        game.Apply(Loadout("2026-09-23T20:00:05Z", grade: null));

        var checklists = Checklists(install, game);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(install.Root, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => game.Active);

        var build = ships.BuildFor(ShipId, "cobramkv");

        ships.Plan(build.Id, new SlotPlan("Radar", "Long Range Scanner", 5));
        ships.Promote(build.Id);

        game.Apply(Loadout("2026-09-23T20:32:15Z", grade: 5));
        checklists.Poll();

        Assert.Equal(ChecklistState.Done, Radar(checklists).State);
    }

    /// <summary>A fresh d47 on the same files, with the game at <c>LoadGame</c> and no loadout yet.</summary>
    private static (GameStateStore Game, ChecklistService Checklists) Start(TempInstall install)
    {
        var game = new GameStateStore();

        game.Apply(Event("""{"timestamp":"2026-09-24T13:15:20Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        game.Apply(Event(
            $$"""
            {"timestamp":"2026-09-24T13:15:27Z","event":"LoadGame","FID":"F1","Commander":"Jameson",
             "Ship":"cobramkv","ShipID":{{ShipId}}}
            """));

        return (game, Checklists(install, game));
    }

    private static ChecklistService Checklists(TempInstall install, GameStateStore game) => new(
        new ChecklistStore(Path.Combine(install.Root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
        new ChecklistProposalStore(
            Path.Combine(install.Root, "checklist-proposals.json"),
            NullLogger<ChecklistProposalStore>.Instance),
        () => game.Active,
        removeFulfilled: () => false);

    private static ChecklistItem Radar(ChecklistService checklists) =>
        Assert.Single(checklists.Document.Items, item => item.Key == BlueprintKey);

    private static JournalEvent Loadout(string timestamp, int? grade)
    {
        var engineering = grade is { } level
            ? ",\"Engineering\":{\"BlueprintName\":\"Sensor_LongRange\",\"Level\":"
              + level.ToString(CultureInfo.InvariantCulture)
              + ",\"Quality\":1.0,\"Engineer\":\"Juri Ishmaak\",\"EngineerID\":300250}"
            : string.Empty;

        return Event(
            $$"""
            {"timestamp":"{{timestamp}}","event":"Loadout","Ship":"cobramkv","ShipID":{{ShipId}},
             "Modules":[{"Slot":"Radar","Item":"int_sensors_size3_class5","On":true{{engineering}}}]}
            """);
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
