using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// A ship's modules survive the Commander getting out of it (asked for 2026-08-20: <i>"When
/// switching ships everything in the old ship that wasn't planned switches to 'not seen'. It WAS
/// seen. Don't get amnesia"</i>).
/// <para>
/// The constraint that produced the amnesia is real and unchanged — Elite reports the loadout of
/// the ship the Commander is sitting in and no other — but it is about what the game
/// <em>sends</em>, not about what has been <em>seen</em>. These assert the difference.
/// </para>
/// </summary>
public class ShipLoadoutsAreRememberedTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Boarding(string timestamp, string hull, int id, string module) =>
        $$"""
          {"timestamp":"{{timestamp}}","event":"Loadout","Ship":"{{hull}}","ShipID":{{id}},
           "Modules":[{"Slot":"MainEngines","Item":"{{module}}","On":true,"Health":1.0}]}
          """;

    private static CommanderGameState State(params string[] events)
    {
        var store = new GameStateStore();

        store.Apply(Event(
            """{"timestamp":"2026-08-20T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        foreach (var json in events)
        {
            store.Apply(Event(json));
        }

        return store.Active!;
    }

    [Fact]
    public void TheShipLeftBehindIsStillKnown()
    {
        var state = State(
            Boarding("2026-08-20T01:00:00Z", "type9_military", 53, "int_engine_size7_class5"),
            Boarding("2026-08-20T02:00:00Z", "anaconda", 51, "int_engine_size7_class2"));

        // The one being flown, unchanged.
        Assert.Equal(51, state.Ship.ShipId);

        // And the one parked, which used to become unknowable the instant it was left.
        var parked = state.Loadouts.For(53);

        Assert.NotNull(parked);
        Assert.Equal("type9_military", parked.Loadout.Type);
        Assert.Equal("int_engine_size7_class5", Assert.Single(parked.Loadout.Modules).Item);
    }

    [Fact]
    public void WhatIsRememberedIsDatedFromTheEventAndNotFromAClock()
    {
        // Core reads no clock, which is what lets the replay harness drive this at 100x and lets a
        // backfill over month-old files date what it finds correctly rather than as "now".
        var state = State(
            Boarding("2026-07-04T09:30:00Z", "type9_military", 53, "int_engine_size7_class5"),
            Boarding("2026-08-20T02:00:00Z", "anaconda", 51, "int_engine_size7_class2"));

        Assert.Equal(
            DateTimeOffset.Parse("2026-07-04T09:30:00Z", System.Globalization.CultureInfo.InvariantCulture),
            state.Loadouts.For(53)!.SeenAt);
    }

    [Fact]
    public void ARollDoneInTheShipIsRememberedWithIt()
    {
        // The two halves meet here. Elite writes no Loadout when a module is engineered, so
        // without the EngineerCraft fold the remembered ship would be the one from boarding —
        // correct about its modules and wrong about every roll done since.
        var state = State(
            Boarding("2026-08-20T01:00:00Z", "type9_military", 53, "int_engine_size7_class5"),
            """
            {"timestamp":"2026-08-20T01:30:00Z","event":"EngineerCraft","Slot":"MainEngines",
             "Module":"int_engine_size7_class5","Engineer":"Felicity Farseer","EngineerID":300100,
             "BlueprintName":"Engine_Dirty","Level":5,"Quality":1.0,
             "Modifiers":[{"Label":"EngineOptimalMass","Value":1155.0,"OriginalValue":1050.0,"LessIsGood":0}]}
            """,
            Boarding("2026-08-20T02:00:00Z", "anaconda", 51, "int_engine_size7_class2"));

        var thrusters = Assert.Single(state.Loadouts.For(53)!.Loadout.Modules);

        Assert.Equal("Engine_Dirty", thrusters.Blueprint);
        Assert.Equal(5, thrusters.BlueprintLevel);
        Assert.Equal(DateTimeOffset.Parse("2026-08-20T01:30:00Z", System.Globalization.CultureInfo.InvariantCulture),
            state.Loadouts.For(53)!.SeenAt);
    }

    [Fact]
    public void ASoldShipIsForgottenRatherThanKept()
    {
        // The one event that makes a remembered ship wrong rather than merely old. Keeping it
        // would put a hull the Commander no longer owns in front of them.
        var state = State(
            Boarding("2026-08-20T01:00:00Z", "type9_military", 53, "int_engine_size7_class5"),
            Boarding("2026-08-20T02:00:00Z", "anaconda", 51, "int_engine_size7_class2"),
            """
            {"timestamp":"2026-08-20T03:00:00Z","event":"ShipyardSell","ShipType":"type9_military",
             "SellShipID":53,"ShipPrice":121500000}
            """);

        Assert.Null(state.Loadouts.For(53));
        Assert.NotNull(state.Loadouts.For(51));
    }

    [Fact]
    public void AShipNeverSatInIsStillUnknown()
    {
        // The honest remainder, and the reason the rest can be trusted: remembering what was
        // watched is not the same as inventing what was not.
        var state = State(Boarding("2026-08-20T02:00:00Z", "anaconda", 51, "int_engine_size7_class2"));

        Assert.Null(state.Loadouts.For(42));
        Assert.NotNull(state.Loadouts.For(51));
    }

    [Fact]
    public void RememberingCostsNothingOnTheEventsThatAreNotAboutTheShip()
    {
        // ShipLoadout.Apply returns `this` for every event it does not handle, so the registry can
        // tell "nothing changed" by reference and never compares a module list. This asserts the
        // property that makes it free on the ~99% of events that are not about the ship.
        var state = State(Boarding("2026-08-20T02:00:00Z", "anaconda", 51, "int_engine_size7_class2"));

        var before = state.Loadouts;

        for (var i = 0; i < 50; i++)
        {
            state.Apply(Event(
                $$"""{"timestamp":"2026-08-20T02:00:{{i:00}}Z","event":"FuelScoop","Scooped":0.5,"Total":32.0}"""));
        }

        Assert.Same(before, state.Loadouts);
    }
}
