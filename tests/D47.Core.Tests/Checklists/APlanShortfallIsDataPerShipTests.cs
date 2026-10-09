using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>The shortfall is a record per ship, and the spoken answer is written from it (#546).</summary>
[Trait("Category", "Integration")]
public class APlanShortfallIsDataPerShipTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-shortfall-tests", Guid.NewGuid().ToString("N"));

    private static readonly ChecklistScope Krait = ChecklistScope.Ship(12);

    private const string Loadout =
        """{ "timestamp":"2026-08-16T08:00:01Z", "event":"Loadout", "Ship":"krait_mkii", "ShipID":12, "ShipName":"Nightjar", "CargoCapacity":128, "Modules":[] }""";

    private const string Depot =
        """
        { "timestamp":"2026-08-16T10:00:00Z", "event":"ColonisationConstructionDepot",
          "MarketID":3960809986, "ConstructionProgress":0.1,
          "ConstructionComplete":false, "ConstructionFailed":false,
          "ResourcesRequired":[
            { "Name":"$steel_name;", "Name_Localised":"Steel",
              "RequiredAmount":500, "ProvidedAmount":80, "Payment":3239 } ] }
        """;

    private static string Felicity(int rank) =>
        $$"""
          { "timestamp":"2026-08-16T09:00:00Z", "event":"EngineerProgress",
            "Engineers":[ {"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":{{rank}}} ] }
          """;

    private static string Palin(int rank) =>
        $$"""
          { "timestamp":"2026-08-16T09:00:00Z", "event":"EngineerProgress",
            "Engineers":[ {"Engineer":"Professor Palin","EngineerID":300220,"Progress":"Unlocked","Rank":{{rank}}} ] }
          """;

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static CommanderGameState State(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-16T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                 }.Concat(lines))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private ChecklistService Service(CommanderGameState state, params ChecklistItem[] items)
    {
        Directory.CreateDirectory(_folder);

        var store = new ChecklistStore(
            Path.Combine(_folder, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance);

        store.Apply("F1", "Jameson", document =>
            new ChecklistChange(document with { Items = [.. document.Items, .. items] }, true, string.Empty));

        return new ChecklistService(
            store,
            new ChecklistProposalStore(
                Path.Combine(_folder, "proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            () => state);
    }

    private static ChecklistItem[] Plan(ChecklistScope scope, params BuildRequest[] requests) =>
        [.. EngineeringPlan.Items(scope, "krait_mkii", requests)];

    [Fact]
    public void AShipWithTwoPlansAndADeliveryHasRowsForAllThree()
    {
        var service = Service(
            State(Loadout, Felicity(5), Depot),
            Plan(Krait,
                new BuildRequest("MainEngines", "Dirty Drive Tuning", 1),
                new BuildRequest("TinyHardpoint4", "Heavy Duty", 1)));

        var shortfall = service.Shortfall(Krait);

        Assert.Equal(2, shortfall.PlanCount);
        Assert.Equal(1, shortfall.DeliveryCount);

        var firmware = MaterialCatalogue.Find("legacyfirmware")!.Name;
        Assert.Contains(shortfall.Rows, row => row.Name == firmware && row.Site is null);
        Assert.True(shortfall.Rows.Count(row => row.Site is null) >= 2);

        var steel = Assert.Single(shortfall.Rows, row => row.Site is not null);
        Assert.Equal("Steel", steel.Name);
        Assert.Equal(MaterialLedger.Cargo, steel.Ledger);
        Assert.Equal(420, steel.Short);
    }

    [Fact]
    public void ANeedOverTheGradeCapIsCountedInTrips()
    {
        var trip = ShortfallTrip.Storage("Imperial Shielding", 130, 100);

        Assert.Equal((130, 100, 2), (trip.Need, trip.Cap, trip.Trips));
        Assert.Contains("at least 2 trips", trip.Sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeliveryLargerThanTheHoldIsCountedInCargoTrips()
    {
        var shortfall = Service(State(Loadout, Depot)).Shortfall(Krait);

        var trip = Assert.Single(shortfall.Trips);

        Assert.Equal(ShortfallTripKind.Cargo, trip.Kind);
        Assert.Equal((420, 128, 4), (trip.Need, trip.Cap, trip.Trips));
    }

    [Fact]
    public void CargoTripsAreOnlyCountedForAShip()
    {
        Assert.Empty(Service(State(Loadout, Depot)).Shortfall().Trips);
    }

    [Fact]
    public void ABlockNamesTheBestUnlockedEngineerAndTheirRank()
    {
        var shortfall = Service(
            State(Loadout, Palin(3)),
            Plan(Krait, new BuildRequest("MainEngines", "Dirty Drive Tuning", 5))).Shortfall(Krait);

        var block = Assert.Single(shortfall.Blocks);

        Assert.Equal(5, block.Grade);
        Assert.Equal("Professor Palin, rank 3", block.Reach);
    }

    [Fact]
    public void ABlockWithNoUnlockedEngineerSaysNotUnlocked()
    {
        var shortfall = Service(
            State(Loadout),
            Plan(Krait, new BuildRequest("MainEngines", "Dirty Drive Tuning", 5))).Shortfall(Krait);

        var block = Assert.Single(shortfall.Blocks);

        Assert.False(block.Unlocked);
        Assert.Equal("not unlocked", block.Reach);
    }

    [Fact]
    public void AnotherShipASuitAndAWeaponAreNotInAShipsRecord()
    {
        var elsewhere = new[] { ChecklistScope.Ship(3), ChecklistScope.Suit(7), ChecklistScope.Weapon(9) }
            .SelectMany(scope => Plan(scope, new BuildRequest("MainEngines", "Dirty Drive Tuning", 1)))
            .ToArray();

        var service = Service(State(Loadout, Felicity(5)), elsewhere);

        var shortfall = service.Shortfall(Krait);

        Assert.Equal(0, shortfall.PlanCount);
        Assert.Empty(shortfall.Rows);
        Assert.Equal(3, service.Shortfall().PlanCount);
    }

    [Fact]
    public void AnOriginsKindIsTheTextBeforeItsParenthesis()
    {
        Assert.Equal("Signal source", OneTripSite.KindOf("Signal source (High grade emissions, Boom)"));
        Assert.Equal("Mission reward", OneTripSite.KindOf("Mission reward"));
    }

    [Fact]
    public void TheSpokenShortfallIsWrittenFromTheRecord()
    {
        var service = Service(
            State(Loadout, Felicity(3), Depot),
            Plan(Krait, new BuildRequest("MainEngines", "Dirty Drive Tuning", 5)));

        var report = service.ShortfallReport();
        var shortfall = service.Shortfall();

        Assert.StartsWith(shortfall.Blocks[0].Sentence, report, StringComparison.Ordinal);
        Assert.Contains("Still to haul, as of your last visit to each site:", report, StringComparison.Ordinal);
        Assert.Contains("  Steel: 420 of 500 to ", report, StringComparison.Ordinal);

        foreach (var row in shortfall.Rows.Where(row => row.Site is null))
        {
            Assert.Contains($"  {row.Name}: {row.Short} short ({row.Held} of {row.Needed}).", report, StringComparison.Ordinal);
        }
    }
}
