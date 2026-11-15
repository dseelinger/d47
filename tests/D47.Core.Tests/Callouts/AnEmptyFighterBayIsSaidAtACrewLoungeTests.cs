using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Tests.Journal;
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

    [Fact]
    public void TheSettingIsOnByDefault()
    {
        Assert.True(new D47.Core.Configuration.CalloutSettings().FighterCrew);
    }
}
