using D47.Core.Journal;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>The state an overheard exchange is written from names the system and the station, and nothing of the ship.</summary>
public class OverheardChatterKnowsOnlyWhereTheCommanderIsTests
{
    private static readonly string[] ShipDetails = ["fuel", "cargo", "hull", "tonnes", "credits"];

    private static void NamesNothingOfTheShip(string state)
    {
        foreach (var detail in ShipDetails)
        {
            Assert.DoesNotContain(detail, state, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ADockedCommanderIsLocatedByTheSystemAndTheStation()
    {
        var state = OffShipKnowledge.Of(new JournalLocation("Anlave", null, true, "Suri Park"));

        Assert.Contains("Anlave", state, StringComparison.Ordinal);
        Assert.Contains("Suri Park", state, StringComparison.Ordinal);
        NamesNothingOfTheShip(state);
    }

    [Fact]
    public void ACommanderInSpaceIsLocatedByTheSystemAlone()
    {
        var state = OffShipKnowledge.Of(new JournalLocation("Anlave", null, false, "Suri Park"));

        Assert.Contains("Anlave", state, StringComparison.Ordinal);
        Assert.DoesNotContain("Suri Park", state, StringComparison.Ordinal);
    }
}
