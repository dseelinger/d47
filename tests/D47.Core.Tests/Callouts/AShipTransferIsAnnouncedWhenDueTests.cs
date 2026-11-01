using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class AShipTransferIsAnnouncedWhenDueTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string kind, DateTimeOffset at, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = at.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["event"] = kind,
        };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Dock() =>
        Event("Docked", Noon, ("StationName", "Jameson Memorial"), ("StationType", "Coriolis"), ("StarSystem", "Shinrarta Dezhra"), ("MarketID", 128666762L));

    private static JournalEvent Transfer(int seconds, string type = "type6", int id = 7) =>
        Event("ShipyardTransfer", Noon, ("ShipType", type), ("ShipID", id), ("System", "Sol"), ("MarketID", 128666762L), ("TransferTime", seconds));

    private static CommanderGameState State(params JournalEvent[] events)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        foreach (var journalEvent in events)
        {
            state.Apply(journalEvent);
        }

        return state;
    }

    private static List<Announcement> Tick(ShipTransferCallout callout, CommanderGameState state, DateTimeOffset now, bool priming, params JournalEvent[] events) =>
        [.. callout.Examine(new CalloutContext(now, priming, state, GameStatus.Unknown, NavRoute.None, events))];

    [Fact]
    public void ATransferIsSilentBeforeItsDueTimeAndSpeaksOnceAtIt()
    {
        var transfer = Transfer(600);
        var state = State(Dock(), transfer);
        var callout = new ShipTransferCallout();

        Assert.Empty(Tick(callout, state, Noon.AddSeconds(1), false, transfer));
        Assert.Empty(Tick(callout, state, Noon.AddSeconds(599), false));

        var said = Assert.Single(Tick(callout, state, Noon.AddSeconds(600), false));

        Assert.Equal(ShipTransferCallout.Key, said.Key);
        Assert.Equal("Your Type-6 Transporter has arrived at Jameson Memorial.", said.Text);
        Assert.Empty(Tick(callout, state, Noon.AddSeconds(700), false));
    }

    [Fact]
    public void ATransferThatFellDueWhilePrimingIsSilent()
    {
        var transfer = Transfer(60);
        var state = State(Dock(), transfer);
        var callout = new ShipTransferCallout();

        Assert.Empty(Tick(callout, state, Noon.AddHours(1), true, transfer));
        Assert.Empty(Tick(callout, state, Noon.AddHours(2), false));
    }

    [Fact]
    public void ATransferStillInTransitAfterPrimingIsAnnouncedOnTime()
    {
        var transfer = Transfer(3600);
        var state = State(Dock(), transfer);
        var callout = new ShipTransferCallout();

        Assert.Empty(Tick(callout, state, Noon.AddMinutes(10), true, transfer));

        Assert.Single(Tick(callout, state, Noon.AddHours(1), false));
    }

    [Fact]
    public void ATransferTimeOfZeroSaysNothing()
    {
        var transfer = Transfer(0);
        var state = State(Dock(), transfer);

        Assert.Empty(Tick(new ShipTransferCallout(), state, Noon.AddHours(1), false, transfer));
    }

    [Fact]
    public void TwoTransfersDueOnTheSameTickGiveTwoLines()
    {
        var first = Transfer(60, id: 7);
        var second = Transfer(120, "python", 8);
        var state = State(Dock(), first, second);
        var callout = new ShipTransferCallout();

        Tick(callout, state, Noon.AddSeconds(1), false, first, second);

        var said = Tick(callout, state, Noon.AddMinutes(5), false);

        Assert.Equal(2, said.Count);
        Assert.Contains("Type-6", said[0].Text);
        Assert.Contains("Python", said[1].Text);
    }

    [Fact]
    public void TheRowDefaultsOn()
    {
        Assert.True(new CalloutSettings().ShipTransfers);
    }
}
