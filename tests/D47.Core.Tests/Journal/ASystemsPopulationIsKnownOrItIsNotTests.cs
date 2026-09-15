using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>What <c>Population</c> does to the tracked location (#230).</summary>
public class ASystemsPopulationIsKnownOrItIsNotTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void UnknownUntilAnEventNamesTheSystem()
    {
        Assert.Null(JournalLocation.Unknown.Population);
    }

    [Fact]
    public void FSDJumpCarriesThePopulation()
    {
        var location = JournalLocation.Unknown.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"FSDJump","StarSystem":"Deciat","StarPos":[0,0,0],"Population":0}"""));

        Assert.Equal(0, location.Population);
    }

    [Fact]
    public void LocationCarriesThePopulation()
    {
        var location = JournalLocation.Unknown.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Sol","Population":22780871309}"""));

        Assert.Equal(22780871309, location.Population);
    }

    [Fact]
    public void CarrierJumpCarriesThePopulation()
    {
        var location = JournalLocation.Unknown.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"CarrierJump","StarSystem":"Colonia","Population":0}"""));

        Assert.Equal(0, location.Population);
    }

    /// <summary>Assigned rather than coalesced, matching <c>ControllingPower</c>: a jump to a system the
    /// event does not describe as populated must not keep wearing the last one's number.</summary>
    [Fact]
    public void APopulatedSystemDoesNotLeaveItsNumberOnTheNextJump()
    {
        var location = JournalLocation.Unknown
            .Apply(Event(
                """{"timestamp":"2026-08-18T09:00:00Z","event":"FSDJump","StarSystem":"Sol","StarPos":[0,0,0],"Population":22780871309}"""))
            .Apply(Event(
                """{"timestamp":"2026-08-18T09:10:00Z","event":"FSDJump","StarSystem":"Deciat","StarPos":[1,1,1],"Population":0}"""));

        Assert.Equal(0, location.Population);
    }
}
