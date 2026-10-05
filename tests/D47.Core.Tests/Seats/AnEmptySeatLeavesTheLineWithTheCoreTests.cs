using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Seats;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Seats;

public class AnEmptySeatLeavesTheLineWithTheCoreTests
{
    private static readonly DateTimeOffset Start = new(3311, 4, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly CrewSeat Ilo = new("0000000c", CrewRole.Navigation, null, "Ilo Varga");

    private sealed class RouteCallout : ICallout
    {
        private int _said;

        public string Id => "route";

        public IEnumerable<Announcement> Examine(CalloutContext context) =>
            [new Announcement($"route.progress.{_said++}", "Three jumps left.")];
    }

    private static CalloutContext Context(int second) =>
        new(Start.AddSeconds(second), false, null, GameStatus.Unknown, NavRoute.None, []);

    [Fact]
    public void WithTheNavigationSeatEmptyTheRouteLineIsTheCores()
    {
        var helm = new CrewSeat("0000000f", CrewRole.Helm, null, "Sam Ode");
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance)
        {
            SeatsFlown = () => new ShipSeats("F1", 7, "python", [helm]),
        }.Add(new RouteCallout());

        engine.Tick(Context(0));

        var said = Assert.Single(engine.Drain());
        Assert.Equal(VoiceRole.ShipAi, said.Voice);
        Assert.Null(said.Speaker);
        Assert.Null(said.Seat);
    }

    [Fact]
    public void SwappingToAShipWithNoSeatsReturnsTheLineToTheCoreOnTheNextTick()
    {
        ShipSeats? flown = new("F1", 7, "python", [Ilo]);

        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance) { SeatsFlown = () => flown }
            .Add(new RouteCallout());

        engine.Tick(Context(0));
        Assert.Equal("Ilo Varga", Assert.Single(engine.Drain()).Speaker);

        flown = null;
        engine.Tick(Context(1));

        var said = Assert.Single(engine.Drain());
        Assert.Equal(VoiceRole.ShipAi, said.Voice);
        Assert.Null(said.Speaker);
    }

    [Fact]
    public void AnEngineGivenNoSeatsSpeaksEverythingAsTheCore()
    {
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(new RouteCallout());

        engine.Tick(Context(0));

        Assert.Equal(VoiceRole.ShipAi, Assert.Single(engine.Drain()).Voice);
    }
}
