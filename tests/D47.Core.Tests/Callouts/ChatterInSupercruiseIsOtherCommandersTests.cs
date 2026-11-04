using D47.Core.Callouts;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class ChatterInSupercruiseIsOtherCommandersTests
{
    [Fact]
    public void TheInstructionSaysTheShipIsInSupercruiseAndNotInNormalSpace()
    {
        foreach (var kind in new[] { NpcChatterKind.Passersby, NpcChatterKind.Hail })
        {
            var prompt = NpcChatter.Instruction(kind, exchangeIndex: 5, supercruise: true);

            Assert.Contains("The Commander's ship is in supercruise, crossing a system between destinations.", prompt, StringComparison.Ordinal);
            Assert.Contains("other commanders in their own ships, also in supercruise in this system", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("normal space", prompt, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ThePassersbyAreDrawnOnlyFromTheSupercruiseCasts()
    {
        var casts = NpcChatter.SupercruiseCasts;

        for (var exchange = 0; exchange < 60; exchange++)
        {
            var prompt = NpcChatter.Instruction(NpcChatterKind.Passersby, exchangeIndex: exchange, supercruise: true);

            Assert.Contains(casts, cast => prompt.Contains($"— {cast} —", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void TheSupercruiseCastsNameNoCrewOrStationStaff()
    {
        Assert.NotEmpty(NpcChatter.SupercruiseCasts);

        foreach (var cast in NpcChatter.SupercruiseCasts)
        {
            Assert.Contains("commander", cast, StringComparison.Ordinal);

            foreach (var banned in new[] { "crew", "dock", "hand", "miner", "controller", "worker", "refinery", "tower" })
            {
                Assert.DoesNotContain(banned, cast, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
