using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Storage;
using D47.Core.Tests.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary><see cref="FighterBayCrewCallout"/>.</summary>
public class AnEmptyFighterBayIsSaidAtACrewLoungeTests
{
    private const string Commander = """{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""";

    private static string Loadout(int shipId, bool bay = true) =>
        $$"""{"timestamp":"3311-01-01T00:00:01Z","event":"Loadout","Ship":"python","ShipID":{{shipId}},"Modules":[{"Slot":"Slot01","Item":"{{(bay ? "int_fighterbay_size5_class1" : "int_cargorack_size5_class1")}}","On":true}]}""";

    private static string Hire(long id, string name) =>
        $$"""{"timestamp":"3311-01-01T00:00:02Z","event":"CrewHire","Name":"{{name}}","CrewID":{{id}},"CombatRank":2}""";

    private static string Assign(long id, string name) =>
        $$"""{"timestamp":"3311-01-01T00:00:03Z","event":"CrewAssign","Name":"{{name}}","CrewID":{{id}},"Role":"Active"}""";

    private static string Swap(int shipId) =>
        $$"""{"timestamp":"3311-01-01T00:00:04Z","event":"ShipyardSwap","ShipType":"python","ShipID":{{shipId}}}""";

    private static string Docked(bool lounge = true, string type = "Coriolis") =>
        $$"""{"timestamp":"3311-01-01T00:00:05Z","event":"Docked","StationName":"Reiter City","StationType":"{{type}}","StationServices":["dock","refuel"{{(lounge ? ",\"crewlounge\"" : "")}}]}""";

    private const string Undocked = """{"timestamp":"3311-01-01T00:00:06Z","event":"Undocked","StationName":"Reiter City"}""";

    private static CommanderGameState State(params string[] lines) => JournalScript.State([Commander, .. lines]);

    private static string[] Said(FighterBayCrewCallout callout, CommanderGameState state, bool priming, params string[] events) =>
        [.. callout.Examine(JournalScript.Context(state, priming, events)).Select(a => a.Text)];

    [Fact]
    public void NoHiredPilotAtAStationSaysTheLoungeCanHireOne()
    {
        var said = Assert.Single(Said(new FighterBayCrewCallout(), State(Loadout(5)), false, Docked()));

        Assert.Equal("No pilot for this ship's fighter bay. The crew lounge here can hire one.", said);
    }

    [Fact]
    public void NoHiredPilotAtAFleetCarrierDoesNotOfferHiring()
    {
        var said = Assert.Single(Said(new FighterBayCrewCallout(), State(Loadout(5)), false, Docked(type: "FleetCarrier")));

        Assert.Equal("No pilot for this ship's fighter bay.", said);
    }

    [Fact]
    public void OneHiredPilotNotPostedHereIsNamed()
    {
        var state = State(Loadout(4), Hire(1, "Ines Varga"), Assign(1, "Ines Varga"), Loadout(5));

        var said = Assert.Single(Said(new FighterBayCrewCallout(), state, false, Docked()));

        Assert.Equal(
            "No pilot in this ship's fighter bay. Make Ines Varga active in the crew lounge, or hire one here.",
            said);
    }

    [Fact]
    public void TwoHiredPilotsNotPostedHereAreCounted()
    {
        var state = State(Loadout(4), Hire(1, "Ines Varga"), Hire(2, "Tomas Reyes"), Assign(1, "Ines Varga"), Loadout(5));

        var said = Assert.Single(Said(new FighterBayCrewCallout(), state, false, Docked()));

        Assert.Equal(
            "No pilot in this ship's fighter bay. Make one of your 2 pilots active in the crew lounge, or hire one here.",
            said);
    }

    [Fact]
    public void AtAFleetCarrierTheReminderDropsTheHireOffer()
    {
        var state = State(Loadout(4), Hire(1, "Ines Varga"), Hire(2, "Tomas Reyes"), Assign(1, "Ines Varga"), Loadout(5));

        var said = Assert.Single(Said(new FighterBayCrewCallout(), state, false, Docked(type: "FleetCarrier")));

        Assert.Equal("No pilot in this ship's fighter bay. Make one of your 2 pilots active in the crew lounge.", said);
    }

    [Fact]
    public void APilotPostedHereAndNotSwappedAwayIsSilent()
    {
        var state = State(Loadout(5), Hire(1, "Ines Varga"), Assign(1, "Ines Varga"));

        Assert.Empty(Said(new FighterBayCrewCallout(), state, false, Docked()));
    }

    [Fact]
    public void ThePilotsOwnHullAfterASwapAwayIsAPlainReminder()
    {
        var state = State(Loadout(5), Hire(1, "Ines Varga"), Assign(1, "Ines Varga"), Swap(6), Loadout(6), Swap(5), Loadout(5));

        var said = Assert.Single(Said(new FighterBayCrewCallout(), state, false, Docked()));

        Assert.Equal("Make Ines Varga active in the crew lounge.", said);
    }

    [Fact]
    public void AHullWithoutAFighterBayIsSilent()
    {
        Assert.Empty(Said(new FighterBayCrewCallout(), State(Loadout(5, bay: false)), false, Docked()));
    }

    [Fact]
    public void AStationWithoutACrewLoungeIsSilent()
    {
        Assert.Empty(Said(new FighterBayCrewCallout(), State(Loadout(5)), false, Docked(lounge: false)));
    }

    [Fact]
    public void PrimingIsSilent()
    {
        Assert.Empty(Said(new FighterBayCrewCallout(), State(Loadout(5)), true, Docked()));
    }

    [Fact]
    public void CrewingAnotherCommandersShipIsSilent()
    {
        var context = new CalloutContext(
            DateTimeOffset.UnixEpoch,
            false,
            State(Loadout(5)),
            new GameStatus { Flags2 = (uint)StatusFlags2.InMulticrew },
            NavRoute.None,
            [JournalScript.Parse(Docked())]);

        Assert.Empty(new FighterBayCrewCallout().Examine(context));
    }

    [Fact]
    public void SwappingIntoAFighterBayHullWhileDockedIsCheckedOnTheLoadoutThatFollows()
    {
        var callout = new FighterBayCrewCallout();

        Assert.Empty(Said(callout, State(Loadout(4, bay: false)), false, Docked()));

        var state = State(Loadout(4, bay: false), Swap(5), Loadout(5));
        var said = Assert.Single(Said(callout, state, false, Swap(5), Loadout(5)));

        Assert.Equal("No pilot for this ship's fighter bay. The crew lounge here can hire one.", said);
    }

    [Fact]
    public void SwappingAwayAndBackWhileDockedDoesNotRepeatIt()
    {
        var callout = new FighterBayCrewCallout();
        var state = State(Loadout(5));

        Assert.Single(Said(callout, state, false, Docked()));
        Assert.Empty(Said(callout, State(Loadout(6, bay: false)), false, Swap(6), Loadout(6, bay: false)));
        Assert.Empty(Said(callout, state, false, Swap(5), Loadout(5)));
    }

    [Fact]
    public void ADockingAfterUndockingIsSaidAgain()
    {
        var callout = new FighterBayCrewCallout();
        var state = State(Loadout(5));

        Assert.Single(Said(callout, state, false, Docked()));
        Assert.Empty(Said(callout, state, false, Undocked));
        Assert.Single(Said(callout, state, false, Docked()));
    }

    [Fact]
    public void ALoadoutWithoutASwapSaysNothing()
    {
        var callout = new FighterBayCrewCallout();

        Assert.Empty(Said(callout, State(Loadout(5)), false, Docked(lounge: false)));
        Assert.Empty(Said(callout, State(Loadout(5)), false, Loadout(5)));
    }

    private static string Restock(string type) =>
        $$"""{"timestamp":"3311-01-01T00:00:07Z","event":"RestockVehicle","Type":"{{type}}","Loadout":"starter","ID":0,"Cost":1030,"Count":1}""";

    private const string Lander = "lander01";

    private const string Fighter = "independent_fighter";

    private const string FirstLogin = """{"timestamp":"3311-01-01T00:00:00Z","event":"LoadGame","FID":"F1","Commander":"Jameson","Ship":"python","ShipID":5}""";

    private const string SecondLogin = """{"timestamp":"3311-01-02T00:00:00Z","event":"LoadGame","FID":"F2","Commander":"Vance","Ship":"python","ShipID":5}""";

    /// <summary>Module events as Elite writes them when hull 5's hangar is refitted.</summary>
    private static readonly string[] RefitLines =
    [
        """{"timestamp":"3311-01-01T00:00:08Z","event":"ModuleBuy","Slot":"Slot04_Size5","BuyItem":"$int_fighterbaymk2_size5_class1_free_name;","BuyItem_Localised":"Fighter Hangar","MarketID":128771628,"BuyPrice":0,"Ship":"python","ShipID":5}""",
        """{"timestamp":"3311-01-01T00:00:08Z","event":"ModuleBuy","Slot":"Slot04_Size5","BuyItem":"$int_cargorack_size5_class1_name;","SellItem":"$int_fighterbay_size5_class1_name;","MarketID":128771628,"BuyPrice":100,"Ship":"python","ShipID":5}""",
        """{"timestamp":"3311-01-01T00:00:08Z","event":"ModuleSell","MarketID":4213021187,"Slot":"Slot05_Size5","SellItem":"$int_fighterbay_size5_class1_name;","SellItem_Localised":"Fighter Hangar","SellPrice":576141,"Ship":"python","ShipID":5}""",
        """{"timestamp":"3311-01-01T00:00:08Z","event":"ModuleStore","MarketID":128734402,"Slot":"Slot02_Size7","StoredItem":"$int_fighterbaymk2_size7_class1_free_name;","Ship":"python","ShipID":5,"Hot":false}""",
        """{"timestamp":"3311-01-01T00:00:08Z","event":"ModuleSwap","MarketID":128771628,"FromSlot":"Slot04_Size5","ToSlot":"FighterBay01","FromItem":"$int_fighterbaymk2_size5_class1_free_name;","ToItem":"Null","Ship":"python","ShipID":5}""",
    ];

    public static TheoryData<string> Refits() => new(RefitLines);

    [Fact]
    public void AHangarHoldingOnlyTheNomadIsSilent()
    {
        Assert.Empty(Said(new FighterBayCrewCallout(), State(Loadout(5), Restock(Lander)), false, Docked()));
    }

    [Fact]
    public void AHangarHoldingTheNomadAndAFighterIsStillSaid()
    {
        var state = State(Loadout(5), Restock(Lander), Restock(Fighter));

        var said = Assert.Single(Said(new FighterBayCrewCallout(), state, false, Docked()));

        Assert.Equal("No pilot for this ship's fighter bay. The crew lounge here can hire one.", said);
    }

    [Fact]
    public void AFighterStockedBeforeTheNomadStillCounts()
    {
        var state = State(Loadout(5), Restock("empire_fighter"), Restock(Lander));

        Assert.Single(Said(new FighterBayCrewCallout(), state, false, Docked()));
    }

    [Fact]
    public void AHangarHoldingOnlyAFighterIsStillSaid()
    {
        Assert.Single(Said(new FighterBayCrewCallout(), State(Loadout(5), Restock(Fighter)), false, Docked()));
    }

    [Fact]
    public void AHangarWithNoRestockSeenIsStillSaid()
    {
        var state = State(Loadout(5));

        Assert.Null(state.Hangars.For(5));
        Assert.Single(Said(new FighterBayCrewCallout(), state, false, Docked()));
    }

    [Theory]
    [InlineData("testbuggy")]
    [InlineData("combat_multicrew_srv_01")]
    [InlineData("mev_rhino")]
    public void AnSrvRestockLeavesTheHangarAsItWas(string srv)
    {
        Assert.Empty(Said(new FighterBayCrewCallout(), State(Loadout(5), Restock(Lander), Restock(srv)), false, Docked()));
        Assert.Null(State(Loadout(5), Restock(srv)).Hangars.For(5));
    }

    [Fact]
    public void AVehicleD47DoesNotKnowKeepsTheReminder()
    {
        Assert.Single(Said(new FighterBayCrewCallout(), State(Loadout(5), Restock(Lander), Restock("lander02")), false, Docked()));
        Assert.Single(Said(new FighterBayCrewCallout(), State(Loadout(5), Restock("lander02"), Restock(Lander)), false, Docked()));
    }

    [Fact]
    public void AFighterInTheWalkedHistoryOutlastsALiveNomadRestock()
    {
        ShipHangars? walked = null;
        var store = new GameStateStore { RestoreHangars = _ => walked };

        foreach (var line in new[] { Commander, Loadout(5), Restock(Lander) })
        {
            store.Apply(JournalScript.Parse(line), null, priming: false);
        }

        walked = ShipHangars.Empty
            .Apply(JournalScript.Parse(Restock(Lander)), 5)
            .Apply(JournalScript.Parse(Restock(Fighter)), 5);
        store.RestoreLate();

        Assert.Equal(new HangarHolds(Nomad: true, Fighter: true), store.Active!.Hangars.For(5));
        Assert.Single(Said(new FighterBayCrewCallout(), store.Active!, false, Docked()));
    }

    [Theory]
    [MemberData(nameof(Refits))]
    public void RefittingTheHangarMakesItNotKnown(string refit)
    {
        var state = State(Loadout(5), Restock(Lander), refit);

        Assert.Equal(HangarHolds.NotKnown, state.Hangars.For(5));
        Assert.Single(Said(new FighterBayCrewCallout(), state, false, Docked()));
    }

    [Fact]
    public void RefittingAnotherHullLeavesThisOneKnown()
    {
        var refit = RefitLines[0].Replace("\"ShipID\":5", "\"ShipID\":6", StringComparison.Ordinal);

        Assert.Empty(Said(new FighterBayCrewCallout(), State(Loadout(5), Restock(Lander), refit), false, Docked()));
    }

    [Fact]
    public void TheRestockGoesToTheHullFlownThen()
    {
        var state = State(Loadout(5), Restock(Lander), Swap(6), Loadout(6), Restock(Fighter));

        Assert.Equal(new HangarHolds(Nomad: true, Fighter: false), state.Hangars.For(5));
        Assert.Equal(new HangarHolds(Nomad: false, Fighter: true), state.Hangars.For(6));
    }

    [Fact]
    public void ARestockThatChangesNothingReportsNoHangarChange()
    {
        var store = JournalScript.Store(Commander, Loadout(5), Restock(Lander));

        var receipt = store.Apply(JournalScript.Parse(Restock(Lander)), null, priming: false);

        Assert.DoesNotContain(receipt.Changes, change => change.Part == nameof(CommanderGameState.Hangars));
    }

    [Fact]
    public void ALiveRefitOutlastsARestoreThatFinishesLate()
    {
        ShipHangars? walked = null;
        var store = new GameStateStore { RestoreHangars = _ => walked };

        foreach (var line in new[] { Commander, Loadout(5), RefitLines[0] })
        {
            store.Apply(JournalScript.Parse(line), null, priming: false);
        }

        walked = ShipHangars.Empty.Apply(JournalScript.Parse(Restock(Lander)), 5);
        store.RestoreLate();

        Assert.Equal(HangarHolds.NotKnown, store.Active!.Hangars.For(5));
        Assert.Single(Said(new FighterBayCrewCallout(), store.Active!, false, Docked()));
    }

    [Fact]
    public void TwoCommandersWhoseHullsShareAShipIdAreKeptApart()
    {
        var files = new MemoryFileSystem();
        files.WriteText("a.log", string.Join('\n', FirstLogin, Loadout(5), Restock(Lander)));
        files.WriteText("b.log", string.Join('\n', SecondLogin, Loadout(5), Restock(Fighter)));

        var walked = HangarBackfill.FromHistory(files, ["a.log", "b.log"], NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Equal(new HangarHolds(Nomad: true, Fighter: false), walked["F1"].For(5));
        Assert.Equal(new HangarHolds(Nomad: false, Fighter: true), walked["F2"].For(5));

        var store = new GameStateStore { RestoreHangars = walked.GetValueOrDefault };

        store.Apply(JournalScript.Parse(FirstLogin), null, priming: false);
        store.Apply(JournalScript.Parse(Loadout(5)), null, priming: false);
        Assert.Empty(Said(new FighterBayCrewCallout(), store.Active!, false, Docked()));

        store.Apply(JournalScript.Parse(SecondLogin), null, priming: false);
        store.Apply(JournalScript.Parse(Loadout(5)), null, priming: false);
        Assert.Single(Said(new FighterBayCrewCallout(), store.Active!, false, Docked()));
    }

    [Fact]
    public void TheBackfillFollowsTheHullThroughASwap()
    {
        var files = new MemoryFileSystem();
        files.WriteText("a.log", string.Join('\n', FirstLogin, Restock(Lander), Swap(6), Loadout(6), Restock(Fighter), Swap(5), Loadout(5)));
        files.WriteText("b.log", string.Join('\n', FirstLogin, Restock(Fighter)));

        var first = HangarBackfill.FromHistory(files, ["a.log"], NullLogger.Instance, TestContext.Current.CancellationToken)["F1"];

        Assert.Equal(new HangarHolds(Nomad: true, Fighter: false), first.For(5));
        Assert.Equal(new HangarHolds(Nomad: false, Fighter: true), first.For(6));

        var both = HangarBackfill.FromHistory(files, ["a.log", "b.log"], NullLogger.Instance, TestContext.Current.CancellationToken)["F1"];

        Assert.Equal(new HangarHolds(Nomad: true, Fighter: true), both.For(5));
    }

    [Fact]
    public void TheSettingIsOnByDefault()
    {
        Assert.True(new D47.Core.Configuration.CalloutSettings().FighterCrew);
    }
}
