using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class TheCarrierCaptainAnnouncesArrivalTests
{
    private const string CallSign = "K7Q-B4X";

    private const long CarrierId = 3700000000L;

    private static JournalEvent Event(string kind, string time, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?> { ["timestamp"] = time, ["event"] = kind };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState WithCarrier()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        state.Apply(Event("CarrierStats", "2026-02-10T09:00:00Z",
            ("Callsign", CallSign), ("Name", "Long Way Home"), ("CarrierID", CarrierId), ("FuelLevel", 900)));

        return state;
    }

    private static List<Announcement> Feed(
        CarrierCallout callout, CommanderGameState state, bool priming, params JournalEvent[] events)
    {
        var said = new List<Announcement>();

        foreach (var journalEvent in events)
        {
            state.Apply(journalEvent);

            said.AddRange(callout.Examine(new CalloutContext(
                DateTimeOffset.UnixEpoch, priming, state, GameStatus.Unknown, NavRoute.None, [journalEvent])));
        }

        return said;
    }

    private static JournalEvent Request() => Event("CarrierJumpRequest", "2026-02-10T09:00:00Z",
        ("CarrierID", CarrierId), ("SystemName", "LTT 7786"), ("DepartureTime", "2026-02-10T09:15:00Z"));

    [Fact]
    public void ALocationAtTheDestinationClearsTheBookingAndSaysSoOnce()
    {
        var callout = new CarrierCallout();
        var state = WithCarrier();

        var said = Feed(callout, state, false,
            Request(),
            Event("CarrierLocation", "2026-02-10T09:15:20Z", ("CarrierID", CarrierId), ("StarSystem", "LTT 7786")),
            Event("CarrierLocation", "2026-02-10T09:15:25Z", ("CarrierID", CarrierId), ("StarSystem", "LTT 7786")));

        Assert.Null(state.Carrier.DestinationSystem);
        Assert.Null(state.Carrier.DestinationBody);
        Assert.Null(state.Carrier.DepartureTime);

        Assert.Equal(CarrierCallout.JumpKey, said[0].Key);
        var arrival = Assert.Single(said, line => line.Key == CarrierCallout.ArrivalKey);
        Assert.Contains("LTT 7786", arrival.Text, StringComparison.Ordinal);
        Assert.Contains("Commander Fixture", arrival.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ALoginLocationAnHourAfterDepartureClearsTheBookingInSilence()
    {
        var callout = new CarrierCallout();
        var state = WithCarrier();

        var said = Feed(callout, state, false,
            Request(),
            Event("CarrierLocation", "2026-02-10T10:15:00Z", ("CarrierID", CarrierId), ("StarSystem", "LTT 7786")));

        Assert.Null(state.Carrier.DestinationSystem);
        Assert.DoesNotContain(said, line => line.Key == CarrierCallout.ArrivalKey);
    }

    [Fact]
    public void ALocationElsewhereLeavesTheBookingAndSaysNothing()
    {
        var callout = new CarrierCallout();
        var state = WithCarrier();

        var said = Feed(callout, state, false,
            Request(),
            Event("CarrierLocation", "2026-02-10T09:15:20Z", ("CarrierID", CarrierId), ("StarSystem", "Sol")));

        Assert.Equal("LTT 7786", state.Carrier.DestinationSystem);
        Assert.DoesNotContain(said, line => line.Key == CarrierCallout.ArrivalKey);
    }

    [Fact]
    public void ASquadronCarrierArrivingSaysNothing()
    {
        var callout = new CarrierCallout();
        var state = WithCarrier();

        var said = Feed(callout, state, false,
            Request(),
            Event("CarrierLocation", "2026-02-10T09:15:20Z",
                ("CarrierType", "SquadronCarrier"), ("CarrierID", 99L), ("StarSystem", "LTT 7786")));

        Assert.Equal("LTT 7786", state.Carrier.DestinationSystem);
        Assert.DoesNotContain(said, line => line.Key == CarrierCallout.ArrivalKey);
    }

    [Fact]
    public void ACarrierJumpAboardSaysTheArrival()
    {
        var callout = new CarrierCallout();
        var state = WithCarrier();

        var said = Feed(callout, state, false,
            Request(),
            Event("CarrierJump", "2026-02-10T09:15:30Z", ("StarSystem", "LTT 7786")));

        Assert.Single(said, line => line.Key == CarrierCallout.ArrivalKey);
    }

    [Fact]
    public void NothingIsSaidWhilePriming()
    {
        var callout = new CarrierCallout();
        var state = WithCarrier();

        var said = Feed(callout, state, true,
            Request(),
            Event("CarrierLocation", "2026-02-10T09:15:20Z", ("CarrierID", CarrierId), ("StarSystem", "LTT 7786")));

        Assert.Empty(said);
        Assert.Null(state.Carrier.DestinationSystem);
    }
}
