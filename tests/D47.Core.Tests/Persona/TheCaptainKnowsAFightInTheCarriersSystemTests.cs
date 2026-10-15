using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

public class TheCaptainKnowsAFightInTheCarriersSystemTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly FightSnapshot Attacked = FightSnapshot.None with { LastActionAt = Now.AddMinutes(-2) };

    private static CommanderGameState Commander(string system)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Tester"))
        {
            Carrier = new CarrierState { CallSign = "BNH-T2F", StarSystem = "Sol" },
        };

        Assert.True(JournalEvent.TryParse(
            $$"""{"timestamp":"2026-10-15T11:00:00Z","event":"Location","StarSystem":"{{system}}"}""",
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            out var location));
        state.Apply(location!);

        return state;
    }

    [Fact]
    public void TheLiveStateCarriesTheFightWhileTheCarrierIsInTheSystem()
    {
        Assert.Contains("attacked in this system 2 minutes ago", CaptainLine.Knows(Commander("Sol"), Now, Attacked));
    }

    [Fact]
    public void TheLiveStateIsSilentOnAFightWhenTheCarrierIsElsewhere()
    {
        Assert.DoesNotContain("attacked", CaptainLine.Knows(Commander("Achenar"), Now, Attacked));
    }
}
