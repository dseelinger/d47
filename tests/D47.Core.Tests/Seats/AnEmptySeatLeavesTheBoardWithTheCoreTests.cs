using D47.Core.Storage;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Seats;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Seats.TheFirstOfficerReadsTheBoardTests;

namespace D47.Core.Tests.Seats;

/// <summary>With no First Officer on the ship flown, or when the model asks for the board, the core reads it.</summary>
public class AnEmptySeatLeavesTheBoardWithTheCoreTests
{
    private static readonly CrewSeat Teo = new("0000000b", CrewRole.ScienceOfficer, null, "Teo Marsh");

    [Fact]
    public async Task NoSeatsMeansTheCoreAnswers()
    {
        var events = await RunAsync(Build(() => null), "mission board");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(MissionsCapability.Describe(Board(), Now), Spoken(events));
    }

    [Fact]
    public async Task ASeatOfAnotherRoleDoesNotReadTheBoard()
    {
        var events = await RunAsync(Build(() => Aboard(7, Teo)), "mission board");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
    }

    [Fact]
    public async Task AFirstOfficerOnlyOnAnotherShipDoesNotReadTheBoard()
    {
        var install = new MemoryInstall();
        var store = new CrewSeatStore(Path.Combine(install.Root, "crew-seats.json"), new MemoryFileSystem(), NullLogger<CrewSeatStore>.Instance);
        store.Set(Aboard(9, Ilo));
        store.Set(Aboard(7, Teo));

        var events = await RunAsync(Build(() => store.For("F100", 7)), "mission board");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
    }

    [Fact]
    public async Task AModelTurnThatCallsTheBoardIsAnsweredByTheCore()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("call_1", MissionsCapability.Tool, "{}"),
            RoundScriptedLlmProvider.Saying("One delivery to Yoru."));

        var events = await RunAsync(Build(() => Aboard(7, Ilo), provider), "is there anything urgent to hand in");

        Assert.Equal(TurnRoute.Model, events.OfType<TurnEvent.Completed>().Single().Result.Route);
        Assert.Empty(events.OfType<TurnEvent.Addressed>());
    }
}
