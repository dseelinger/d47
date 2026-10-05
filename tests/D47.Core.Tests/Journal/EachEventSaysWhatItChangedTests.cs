using System.Reflection;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Each journal event carries a receipt naming the parts of d47's state it changed.</summary>
public class EachEventSaysWhatItChangedTests
{
    private const string LoadGame =
        """{"timestamp":"2026-10-05T10:00:00Z","event":"LoadGame","FID":"F1234567","Commander":"Doug Seelinger","Ship":"Krait_MkII","ShipID":7}""";

    private const string CarrierLocation =
        """{"timestamp":"2026-10-05T10:00:01Z","event":"CarrierLocation","CarrierType":"FleetCarrier","CarrierID":3715429376,"StarSystem":"Kuk","SystemAddress":1,"BodyID":0}""";

    private const string Docked =
        """{"timestamp":"2026-10-05T10:05:00Z","event":"Docked","StationName":"BNH-T2F","StationType":"FleetCarrier","StarSystem":"Kuk","SystemAddress":1,"MarketID":3715429376}""";

    private const string Undocked =
        """{"timestamp":"2026-10-05T10:10:00Z","event":"Undocked","StationName":"BNH-T2F","StationType":"FleetCarrier","MarketID":3715429376}""";

    private const string DockedAgain =
        """{"timestamp":"2026-10-05T10:15:00Z","event":"Docked","StationName":"BNH-T2F","StationType":"FleetCarrier","StarSystem":"Kuk","SystemAddress":1,"MarketID":3715429376}""";

    private const string Music =
        """{"timestamp":"2026-10-05T10:16:00Z","event":"Music","MusicTrack":"Exploration"}""";

    [Fact]
    public void DockingAtYourOwnCarrierLearnsItsCallsign()
    {
        var store = new GameStateStore();
        store.Apply(Event(LoadGame));
        store.Apply(Event(CarrierLocation));

        var said = store.Apply(Event(Docked)).Said;

        Assert.StartsWith("This event updated:", said, StringComparison.Ordinal);
        Assert.Contains("your location (Kuk · BNH-T2F)", said, StringComparison.Ordinal);
        Assert.Contains("your carrier's callsign (learned here)", said, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNextDockingDoesNotLearnTheCallsignAgain()
    {
        var store = new GameStateStore();
        store.Apply(Event(LoadGame));
        store.Apply(Event(CarrierLocation));
        store.Apply(Event(Docked));
        store.Apply(Event(Undocked));

        var said = store.Apply(Event(DockedAgain)).Said;

        Assert.DoesNotContain("callsign", said, StringComparison.Ordinal);
    }

    [Fact]
    public void MusicChangesNothing()
    {
        var store = new GameStateStore();
        store.Apply(Event(LoadGame));
        store.Apply(Event(CarrierLocation));

        Assert.Equal("Nothing in d47's picture changed.", store.Apply(Event(Music)).Said);
    }

    [Fact]
    public void AnEventBeforeAnyCommanderChangesNothing() =>
        Assert.Same(FoldReceipt.Nothing, new GameStateStore().Apply(Event(Music)));

    [Fact]
    public void EveryFoldedPartHasAPhrase()
    {
        var parts = typeof(CommanderGameState)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is not null)
            .Select(property => property.Name)
            .Where(name => name is not (nameof(CommanderGameState.Suit) or nameof(CommanderGameState.Hold)))
            .ToList();

        Assert.NotEmpty(parts);
        Assert.All(parts, part => Assert.NotNull(CommanderGameState.PhraseFor(part)));
    }

    [Fact]
    public void TheLogKeepsEachEventsReceiptAsWords()
    {
        var store = new GameStateStore();
        var events = new[] { LoadGame, CarrierLocation, Docked, Music }.Select(Event).ToList();
        var receipts = events.Select(journalEvent => store.Apply(journalEvent)).ToList();

        var log = new JournalLog();
        log.Add(events, receipts);

        Assert.Equal(receipts.Select(receipt => receipt.Said), log.Entries.Select(entry => entry.Receipt));
    }

    [Fact]
    public void ALogWithNoStateBehindItHasNoReceipts()
    {
        var log = new JournalLog();
        log.Add([Event(Music)]);

        Assert.Null(Assert.Single(log.Entries).Receipt);
    }

    [Fact]
    public void AJournalEntryHoldsNoStatePart()
    {
        var partTypes = typeof(CommanderGameState)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.PropertyType)
            .ToHashSet();

        var entry = typeof(JournalEntry).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Equal(typeof(string), entry.Single(property => property.Name == nameof(JournalEntry.Receipt)).PropertyType);
        Assert.DoesNotContain(entry, property => partTypes.Contains(property.PropertyType));
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
