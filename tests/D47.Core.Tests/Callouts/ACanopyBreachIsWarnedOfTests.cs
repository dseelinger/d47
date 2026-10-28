using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A broken canopy is warned of with the oxygen time left, and a dock with it still broken says to repair it (#1790).</summary>
public class ACanopyBreachIsWarnedOfTests
{
    private const string Breached =
        """{"timestamp":"3311-01-01T12:00:00Z","event":"CockpitBreached"}""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState Flying(string? lifeSupport)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"3311-01-01T11:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        var modules = lifeSupport is null
            ? string.Empty
            : $$"""{"Slot":"LifeSupport","Item":"{{lifeSupport}}","On":true,"Priority":0,"Health":1.0}""";

        store.Apply(Event(
            $$"""{"timestamp":"3311-01-01T11:00:01Z","event":"Loadout","Ship":"krait_mkii","ShipID":52,"ShipName":"A","ShipIdent":"KR-01","Modules":[{{modules}}]}"""));

        return store.Active!;
    }

    private static List<Announcement> Say(CommanderGameState? state, string json, bool priming = false) =>
        [.. new DangerCallout().Examine(
            new CalloutContext(DateTimeOffset.UnixEpoch, priming, state, GameStatus.Unknown, NavRoute.None, [Event(json)]))];

    [Fact]
    public void ABreachNamesTheOxygenTimeOfTheFittedLifeSupport()
    {
        var said = Assert.Single(Say(Flying("int_lifesupport_size1_class3"), Breached));

        Assert.Equal(CalloutUrgency.Urgent, said.Urgency);
        Assert.Equal("Canopy breach. Ten minutes of oxygen.", said.Text);
    }

    [Fact]
    public void AHalfMinuteIsSaidAsAndAHalf()
    {
        var said = Assert.Single(Say(Flying("int_lifesupport_size1_class2"), Breached));

        Assert.Equal("Canopy breach. Seven and a half minutes of oxygen.", said.Text);
    }

    [Fact]
    public void WithoutAKnownLifeSupportItSaysEmergencyOxygen()
    {
        Assert.Equal("Canopy breach. We are on emergency oxygen.", Assert.Single(Say(Flying(null), Breached)).Text);
        Assert.Equal("Canopy breach. We are on emergency oxygen.", Assert.Single(Say(null, Breached)).Text);
    }

    [Fact]
    public void DockingWithTheCanopyBrokenSaysToRepairIt()
    {
        var said = Assert.Single(Say(
            null,
            """{"timestamp":"3311-01-01T12:10:00Z","event":"Docked","StationName":"S","CockpitBreach":true}"""));

        Assert.Equal("The canopy is still breached. Repair it before launching.", said.Text);
    }

    [Fact]
    public void DockingWithoutTheFlagSaysNothing() =>
        Assert.Empty(Say(
            null,
            """{"timestamp":"3311-01-01T12:10:00Z","event":"Docked","StationName":"S"}"""));

    [Fact]
    public void ABreachInTheStartupReplayIsNotSpoken() =>
        Assert.Empty(Say(null, Breached, priming: true));

    [Fact]
    public void TheBreachIsNarrated() =>
        Assert.Equal("The canopy was breached", JournalSentence.For(Event(Breached)));
}
