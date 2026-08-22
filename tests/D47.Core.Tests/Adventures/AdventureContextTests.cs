using D47.Core.Adventures;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// The persona knows what the Commander knows, plus the stake (list.md Phase 47). The turn and the
/// ending enter the block only when their beats have fired; the beats ahead never do.
/// </summary>
public class AdventureContextTests
{
    private static readonly DateTimeOffset Now = Accepted.AddHours(1);

    private static AdventureStanding After(int beats) =>
        WholeRoute(Accepted).Take(beats).Aggregate(AdventureFold.Start(LanternRoute(Accepted)), AdventureFold.Apply);

    private static string Block(AdventureStanding standing) =>
        AdventureContext.Describe([standing], id => id is null ? null : "Archivist", Now)!;

    [Fact]
    public void NothingUnderWayIsNull()
    {
        Assert.Null(AdventureContext.Describe([AdventureFold.Start(LanternRoute())], _ => null, Now));
        Assert.Null(AdventureContext.Describe([After(5)], _ => null, Now));
    }

    [Fact]
    public void ThePremiseWantAndStakeAreAlwaysThere()
    {
        var block = Block(After(0));

        Assert.StartsWith(AdventureContext.Label, block);
        Assert.Contains("An outpost abandoned in 3302", block);
        Assert.Contains("To find out who keeps it running", block);
        Assert.Contains("Whether a place left behind", block);
        Assert.Contains("Opening: Beacons cost money", block);
        Assert.Contains("Now: The Lantern (setup): waiting to arrive at Ossen's Lantern.", block);
    }

    [Fact]
    public void TheTurnAndTheEndingAreWithheldUntilTheirBeats()
    {
        foreach (var reached in new[] { 0, 1, 2 })
        {
            var block = Block(After(reached));

            Assert.DoesNotContain("one person by name", block);
            Assert.DoesNotContain("forty kilometres", block);
        }

        Assert.Contains("The turn, now reached: The beacon speaks to one person by name.", Block(After(3)));
        Assert.DoesNotContain("forty kilometres", Block(After(4)));
    }

    [Fact]
    public void TheBeatsAheadNeverAppear()
    {
        var block = Block(After(1));

        Assert.Contains("So far: The Lantern", block);
        Assert.Contains("Last beat, The Lantern, ", block);
        Assert.Contains("Scoop here.", block);
        Assert.Contains("Now: The Survey (catalyst)", block);

        // Titles and lines of beats three to five.
        Assert.DoesNotContain("The Anchorage", block);
        Assert.DoesNotContain("Veyl 3 c", block);
        Assert.DoesNotContain("To one name.", block);
        Assert.DoesNotContain("Eleven months left.", block);
    }

    [Fact]
    public void WhoWroteItIsSaidInWords()
    {
        Assert.Contains("written without a persona", Block(After(0)));

        var byArchivist = AdventureFold.Start(LanternRoute(Accepted) with { WrittenBy = "archivist" });
        Assert.Contains("written by Archivist", Block(byArchivist));

        var byCommander = AdventureFold.Start(LanternRoute(Accepted) with { Source = AdventureSource.Commander });
        Assert.Contains("written by the Commander", Block(byCommander));
    }
}
