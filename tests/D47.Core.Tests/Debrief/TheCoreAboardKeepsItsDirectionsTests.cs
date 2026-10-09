using D47.Core.Debrief;
using Xunit;

namespace D47.Core.Tests.Debrief;

public class TheCoreAboardKeepsItsDirectionsTests
{
    private static StandingDirectionsSession Session()
    {
        var session = new StandingDirectionsSession();

        session.Begin(
        [
            new StandingDirection("general", "Keep answers short.") { State = DirectionState.Adopted },
            new StandingDirection("for-a", "Call me Commander.") { State = DirectionState.Adopted, Persona = "A" },
        ]);

        return session;
    }

    [Fact]
    public void TheBlockForTheCoreAboardEndsWithItsOverlay()
    {
        var session = Session();

        Assert.Equal("block\n\n" + session.Overlay("A"), session.PersonaBlock("block", "A"));
        Assert.Contains("Call me Commander.", session.PersonaBlock("block", "A"));
    }

    [Fact]
    public void ACoreWithNoOverlayGetsTheBlockAlone() =>
        Assert.Equal("block", Session().PersonaBlock("block", "B"));

    [Fact]
    public void WithPersonalityOffThereIsNoBlock() =>
        Assert.Null(Session().PersonaBlock(null, "A"));
}
