using D47.Core.Debrief;
using Xunit;

namespace D47.Core.Tests.Debrief;

[Trait("Category", "Integration")]
public sealed class DirectionsAreReadAgainAtEachBeginTests
{
    [Fact]
    public void ADirectionAdoptedSinceTheLastBeginIsLatchedAndAProposalIsNot()
    {
        using var debrief = new ADebriefOnDisk();

        debrief.Host.Begin();
        Assert.Empty(debrief.Host.Directions.Latched);

        var elsewhere = debrief.Open();
        elsewhere.Write(ADebriefOnDisk.Commander, new StandingDirection("adopted", "Keep answers short.") { State = DirectionState.Adopted });
        elsewhere.Write(ADebriefOnDisk.Commander, new StandingDirection("proposed", "Never mention my rank."));

        debrief.Host.Begin();

        var latched = Assert.Single(debrief.Host.Directions.Latched);
        Assert.Equal("adopted", latched.Key);
    }
}
