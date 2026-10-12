using D47.Core.Adventures;
using D47.Core.Ships;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>Adaptive thinking is charged to the output ceiling, so the ceilings stay high enough to leave room for the JSON answer.</summary>
public sealed class AThinkingModelStillHasRoomToAnswerTests
{
    private const int Floor = 16000;

    [Fact]
    public void TheSpineTurnCeilingIsAtLeastTheFloor() => Assert.True(AdventureGenerator.SpineOutputCeiling >= Floor);

    [Fact]
    public void TheBeatsTurnCeilingIsAtLeastTheFloor() => Assert.True(AdventureGenerator.BeatsOutputCeiling >= Floor);

    [Fact]
    public void TheShipPlanCeilingIsAtLeastTheFloor() => Assert.True(ShipPlanAdvisor.OutputCeiling >= Floor);
}
