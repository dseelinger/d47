using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

public class ATargetPilotKnowsWhoFliesOnTheCommandersSideTests
{
    private static readonly TargetedShip Pilot = new("Cobra Mk III", "Jessa Corvane");

    private static readonly DateTimeOffset At = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static async Task<string> BriefAsync(CommandersSide? side)
    {
        var channels = new CommsChannels();
        channels.Update(new CommsInputs { Target = Pilot });
        var line = new TargetLine(() => Pilot, side: side is null ? null : () => side);

        var decision = await line.RouteAsync(channels.Selected!, "who are you", TestContext.Current.CancellationToken);

        return Assert.IsType<LineDecision.Taken>(decision).Speaker.Brief;
    }

    [Fact]
    public async Task AFighterTheCrewFliesNamesTheCrewMember()
    {
        var brief = await BriefAsync(new CommandersSide(new LaunchedFighter(1, false, At), "Ilse Moravec", 0));

        Assert.Contains("The Commander has launched a fighter, flown by their crew member Ilse Moravec.", brief);
    }

    [Fact]
    public async Task AFighterTheCommanderFliesSaysTheShipIsNearby()
    {
        var brief = await BriefAsync(new CommandersSide(new LaunchedFighter(1, true, At), "Ilse Moravec", 0));

        Assert.Contains("The Commander is flying a ship-launched fighter. Their own ship is nearby, flown by their crew member Ilse Moravec.", brief);
    }

    [Fact]
    public async Task WithNoCrewMemberNamedTheCrewStandsIn()
    {
        var brief = await BriefAsync(new CommandersSide(new LaunchedFighter(1, false, At), null, 0));

        Assert.Contains("flown by their crew.", brief);
    }

    [Fact]
    public async Task WithNoFighterAndNoWingTheBriefSaysNothingOfTheirSide()
    {
        foreach (var side in new CommandersSide?[] { null, new CommandersSide(null, "Ilse Moravec", 0) })
        {
            var brief = await BriefAsync(side);

            Assert.DoesNotContain("fighter", brief);
            Assert.DoesNotContain("wing with", brief);
            Assert.DoesNotContain("Ilse", brief);
        }
    }

    [Fact]
    public async Task WingmatesAreCountedAndNeverNamed()
    {
        Assert.True(JournalEvent.TryParse(
            """{ "timestamp":"2026-11-10T12:00:00Z", "event":"WingJoin", "Others":["Wren Halloway","Odo Brandt"] }""",
            NullLogger.Instance,
            out var join));

        var roster = WingRoster.Empty.Apply(join!, "Tester");

        var brief = await BriefAsync(new CommandersSide(null, null, roster.Members.Count));

        Assert.Contains("The Commander flies in a wing with up to 2 other pilots; you cannot tell whether any of them are here.", brief);
        Assert.DoesNotContain("Wren", brief);
        Assert.DoesNotContain("Odo", brief);
    }
}
