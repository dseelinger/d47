using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class PowerplaySalvageTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private const string Pledge =
        """{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"Li Yong-Rui","Rank":3,"Merits":10}""";

    private const string StartHyperspace =
        """{"timestamp":"3311-01-01T00:10:00Z","event":"StartJump","JumpType":"Hyperspace","StarSystem":"Sol"}""";

    private const string StartSupercruise =
        """{"timestamp":"3311-01-01T00:10:00Z","event":"StartJump","JumpType":"Supercruise"}""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Arrive(string system, string power, string state, string kind = "FSDJump") =>
        "{\"timestamp\":\"3311-01-01T00:01:00Z\",\"event\":\"" + kind + "\",\"StarSystem\":\"" + system
        + "\",\"ControllingPower\":\"" + power + "\",\"PowerplayState\":\"" + state + "\"}";

    private static string Scoop(string type) =>
        "{\"timestamp\":\"3311-01-01T00:05:00Z\",\"event\":\"CollectCargo\",\"Type\":\"" + type + "\",\"Stolen\":false}";

    private static string Eject(string type, int count) =>
        "{\"timestamp\":\"3311-01-01T00:06:00Z\",\"event\":\"EjectCargo\",\"Type\":\"" + type + "\",\"Count\":" + count + "}";

    private static string HandIn(string name, int count) =>
        "{\"timestamp\":\"3311-01-01T00:07:00Z\",\"event\":\"SearchAndRescue\",\"MarketID\":1,\"Name\":\"" + name + "\",\"Count\":" + count + ",\"Reward\":1}";

    private static string Repeat(string line, int times) => string.Join("\n", Enumerable.Repeat(line, times));

    private static CommanderGameState StateFrom(params string[] lines)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        foreach (var line in lines.SelectMany(block => block.Split('\n')))
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static CalloutContext Context(CommanderGameState state, params string[] events) =>
        new(Start, false, state, GameStatus.Unknown, NavRoute.None, [.. events.Select(Event)]);

    private static string[] Own(params string[] after) =>
        [Pledge, Arrive("47 Arietis", "Li Yong-Rui", "Fortified"), .. after];

    [Fact]
    public void TheCountsFollowThe2026January7HandIns()
    {
        var state = StateFrom(Own(
            Repeat(Scoop("WreckageComponents"), 9),
            Scoop("USSCargoBlackBox"),
            Repeat(Scoop("OccupiedCryoPod"), 7)));

        Assert.Equal(9, state.Salvage.Counts["wreckagecomponents"]);
        Assert.Equal(1, state.Salvage.Counts["usscargoblackbox"]);
        Assert.Equal(7, state.Salvage.Counts["occupiedcryopod"]);

        state = StateFrom(Own(
            Repeat(Scoop("WreckageComponents"), 9),
            Scoop("USSCargoBlackBox"),
            HandIn("usscargoblackbox", 1),
            HandIn("wreckagecomponents", 9)));

        Assert.Empty(state.Salvage.Counts);
    }

    [Fact]
    public void SalvageScoopedInYourOwnSystemIsWarnedAboutOnAHyperspaceJump()
    {
        var state = StateFrom(Own(Repeat(Scoop("WreckageComponents"), 9), Scoop("USSCargoBlackBox"), StartHyperspace));

        var spoken = Assert.Single(new PowerplaySalvageCallout().Examine(Context(state, StartHyperspace)));

        Assert.Equal(
            "You are leaving 47 Arietis with nine Wreckage Components and one Black Box scooped here. They only earn merits handed in at a Power contact in this system.",
            spoken.Text);
    }

    [Fact]
    public void ASupercruiseJumpSaysNothing()
    {
        var state = StateFrom(Own(Scoop("USSCargoBlackBox"), StartSupercruise));

        Assert.Empty(new PowerplaySalvageCallout().Examine(Context(state, StartSupercruise)));
    }

    [Fact]
    public void NothingIsSaidWithoutAJump()
    {
        var state = StateFrom(Own(Scoop("USSCargoBlackBox")));

        Assert.Empty(new PowerplaySalvageCallout().Examine(Context(state)));
    }

    [Fact]
    public void NothingIsSaidWhilePriming()
    {
        var state = StateFrom(Own(Scoop("USSCargoBlackBox"), StartHyperspace));
        var priming = new CalloutContext(Start, true, state, GameStatus.Unknown, NavRoute.None, [Event(StartHyperspace)]);

        Assert.Empty(new PowerplaySalvageCallout().Examine(priming));
    }

    [Theory]
    [InlineData("Li Yong-Rui", "Unoccupied")]
    [InlineData("Yuri Grom", "Exploited")]
    public void SalvageScoopedInAnUnoccupiedOrRivalSystemSaysNothing(string power, string state)
    {
        var game = StateFrom(Pledge, Arrive("Cubeo", power, state), Scoop("USSCargoBlackBox"), StartHyperspace);

        Assert.Empty(game.Salvage.Counts);
        Assert.Empty(new PowerplaySalvageCallout().Examine(Context(game, StartHyperspace)));
    }

    [Fact]
    public void EscapePodsAloneSayNothing()
    {
        var state = StateFrom(Own(Repeat(Scoop("OccupiedCryoPod"), 3), Scoop("DamagedEscapePod"), StartHyperspace));

        Assert.Equal(4, state.Salvage.Counts.Values.Sum());
        Assert.Empty(new PowerplaySalvageCallout().Examine(Context(state, StartHyperspace)));
    }

    [Fact]
    public void EjectingClearsTheCount()
    {
        var state = StateFrom(Own(Repeat(Scoop("USSCargoBlackBox"), 2), Eject("usscargoblackbox", 2), StartHyperspace));

        Assert.Empty(state.Salvage.Counts);
        Assert.Empty(new PowerplaySalvageCallout().Examine(Context(state, StartHyperspace)));
    }

    [Fact]
    public void DyingClearsTheCount()
    {
        var state = StateFrom(Own(Scoop("USSCargoBlackBox"), """{"timestamp":"3311-01-01T00:08:00Z","event":"Died"}"""));

        Assert.Empty(state.Salvage.Counts);
    }

    [Fact]
    public void ArrivingInAnotherSystemClearsTheCount()
    {
        var jumped = StateFrom(Own(Scoop("USSCargoBlackBox"), Arrive("Sol", "Li Yong-Rui", "Fortified")));
        var carried = StateFrom(Own(Scoop("USSCargoBlackBox"), Arrive("Sol", "Li Yong-Rui", "Fortified", "CarrierJump")));

        Assert.Empty(jumped.Salvage.Counts);
        Assert.Empty(carried.Salvage.Counts);
    }

    [Fact]
    public void TheCountNeverExceedsWhatTheHoldCarries()
    {
        var state = StateFrom(Own(Repeat(Scoop("WreckageComponents"), 9)));
        var hold = new CargoHold
        {
            ReadAt = Start,
            Items = [new CargoItem("wreckagecomponents", 4)],
        };

        var unclaimed = Assert.Single(state.Salvage.Unclaimed(hold));

        Assert.Equal(4, unclaimed.Count);
    }

    [Fact]
    public void MoreThanThreeKindsAreNamedAsACount()
    {
        var state = StateFrom(Own(
            Repeat(Scoop("WreckageComponents"), 4),
            Repeat(Scoop("USSCargoBlackBox"), 3),
            Repeat(Scoop("AIRelics"), 2),
            Scoop("AncientKey"),
            Scoop("AncientOrb"),
            StartHyperspace));

        var spoken = Assert.Single(new PowerplaySalvageCallout().Examine(Context(state, StartHyperspace)));

        Assert.StartsWith("You are leaving 47 Arietis with four Wreckage Components, three Black Box, two AI Relics and two more kinds scooped here.", spoken.Text);
    }

    [Fact]
    public void WithTheRowOffNothingIsSaid()
    {
        var state = StateFrom(Own(Scoop("USSCargoBlackBox"), StartHyperspace));
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance);
        engine.Add(new PowerplaySalvageCallout());
        engine.SetEnabled("powerplay-salvage", false);

        engine.Tick(Context(state, StartHyperspace));

        Assert.Empty(engine.Drain());
    }

    [Fact]
    public void TheRowIsOnByDefault() => Assert.True(new CalloutSettings().PowerplaySalvage);
}
