using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>What <c>StationAllegiance</c> does to the tracked location (#68).</summary>
public class AnEmpireStationNamesItsAllegianceTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void DockedAtAnEmpireStationCarriesTheAllegiance()
    {
        var location = JournalLocation.Unknown.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"Docked","StarSystem":"Achenar","StationName":"Zeppelin Depot","StationAllegiance":"Empire"}"""));

        Assert.Equal("Empire", location.StationAllegiance);
    }

    /// <summary>Most stations name no allegiance at all, which is the majority case this feature must leave alone.</summary>
    [Fact]
    public void DockedWithNoAllegianceFieldCarriesNone()
    {
        var location = JournalLocation.Unknown.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"Docked","StarSystem":"Colonia","StationName":"Metz Enterprise"}"""));

        Assert.Null(location.StationAllegiance);
    }

    [Fact]
    public void UndockingDropsTheAllegiance()
    {
        var location = JournalLocation.Unknown
            .Apply(Event("""{"timestamp":"2026-08-18T09:00:00Z","event":"Docked","StarSystem":"Achenar","StationName":"Zeppelin Depot","StationAllegiance":"Empire"}"""))
            .Apply(Event("""{"timestamp":"2026-08-18T09:05:00Z","event":"Undocked","StationName":"Zeppelin Depot"}"""));

        Assert.Null(location.StationAllegiance);
    }

    /// <summary>Jumping away leaves nothing of the last dock behind, allegiance included.</summary>
    [Fact]
    public void JumpingAwayDropsTheAllegiance()
    {
        var location = JournalLocation.Unknown
            .Apply(Event("""{"timestamp":"2026-08-18T09:00:00Z","event":"Docked","StarSystem":"Achenar","StationName":"Zeppelin Depot","StationAllegiance":"Empire"}"""))
            .Apply(Event("""{"timestamp":"2026-08-18T09:10:00Z","event":"FSDJump","StarSystem":"Sol","StarPos":[0,0,0]}"""));

        Assert.Null(location.StationAllegiance);
    }

    /// <summary>A second dock at a station with no allegiance is not left wearing the previous one's.</summary>
    [Fact]
    public void ADockWithNoAllegianceIsNotTheLastStationsCarriedOver()
    {
        var location = JournalLocation.Unknown
            .Apply(Event("""{"timestamp":"2026-08-18T09:00:00Z","event":"Docked","StarSystem":"Achenar","StationName":"Zeppelin Depot","StationAllegiance":"Empire"}"""))
            .Apply(Event("""{"timestamp":"2026-08-18T09:05:00Z","event":"Undocked","StationName":"Zeppelin Depot"}"""))
            .Apply(Event("""{"timestamp":"2026-08-18T09:10:00Z","event":"Docked","StarSystem":"Achenar","StationName":"Metz Enterprise"}"""));

        Assert.Null(location.StationAllegiance);
    }
}
