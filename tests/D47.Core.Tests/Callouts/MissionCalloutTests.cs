using D47.Core.Callouts;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Tests.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class MissionCalloutTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string kind, params (string Key, object? Value)[] fields) =>
        JournalScript.EventOf(kind, "2026-09-30T12:00:00Z", fields);

    private static JournalEvent Accept(long id, string station, long reward, DateTimeOffset expiry) =>
        Event("MissionAccepted",
            ("MissionID", id),
            ("Name", "Mission_Courier"),
            ("LocalisedName", $"Courier {id}"),
            ("DestinationSystem", "Sol"),
            ("DestinationStation", station),
            ("Reward", reward),
            ("Expiry", expiry.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")));

    private static CommanderGameState Commander(params JournalEvent[] events) =>
        JournalScript.Commander("F1", "Fixture", events);

    private static List<Announcement> Run(MissionCallout callout, CommanderGameState state, DateTimeOffset now, bool priming = false, params JournalEvent[] events) =>
        [.. callout.Examine(new CalloutContext(now, priming, state, GameStatus.Unknown, NavRoute.None, events))];

    private static JournalEvent Cargo(long id, string kind, string symbol, string localised, int count) =>
        Event("MissionAccepted",
            ("MissionID", id),
            ("Name", kind),
            ("LocalisedName", $"Cargo {id}"),
            ("Commodity", $"${symbol}_Name;"),
            ("Commodity_Localised", localised),
            ("Count", count),
            ("Expiry", Start.AddDays(2).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")));

    private static CommanderGameState DockedWithHold(int hold) =>
        Commander(
            Event("Loadout", ("Ship", "python"), ("ShipID", 1), ("CargoCapacity", hold)),
            Event("Docked", ("StarSystem", "Sol"), ("StationName", "Jameson Memorial")));

    private static MarketBook Market(string commodity, int stock)
    {
        var book = new MarketBook(Path.Combine(@"C:\d47-test", "market.json"), new MemoryFileSystem(), NullLogger.Instance);

        book.Remember(new MarketSnapshot
        {
            Station = "Jameson Memorial",
            System = "Sol",
            Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
            {
                [commodity] = new MarketQuote(commodity) { Supply = stock },
            },
        });

        return book;
    }

    [Fact]
    public void ADeliveryLargerThanTheHoldSaysHowManyTripsItTakes()
    {
        var accept = Cargo(1, "Mission_Delivery_Boom", "powergenerators", "Power Generators", 99);

        var said = Run(new MissionCallout(), DockedWithHold(64), Start, false, accept);

        Assert.Equal("That's 99 tons against a 64-ton hold. Two trips, or a bigger ship.", Assert.Single(said).Text);
        Assert.Empty(Run(new MissionCallout(), DockedWithHold(128), Start, false, accept));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void ACollectMissionForWhatTheMarketStocksSaysTheyAreSoldHere()
    {
        var callout = new MissionCallout { Markets = Market("Power Generators", 500) };
        var accept = Cargo(1, "Mission_Collect_Boom", "powergenerators", "Power Generators", 20);

        Assert.Equal("They sell Power Generators here.", Assert.Single(Run(callout, DockedWithHold(64), Start, false, accept)).Text);

        var unstocked = new MissionCallout { Markets = Market("Power Generators", 0) };
        Assert.Empty(Run(unstocked, DockedWithHold(64), Start, false, accept));

        var elsewhere = new MissionCallout { Markets = Market("Gold", 500) };
        Assert.Empty(Run(elsewhere, DockedWithHold(64), Start, false, accept));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void ADeliveryMissionForWhatTheMarketStocksSaysNothing()
    {
        var callout = new MissionCallout { Markets = Market("Power Generators", 500) };
        var accept = Cargo(1, "Mission_Delivery_Boom", "powergenerators", "Power Generators", 20);

        Assert.Empty(Run(callout, DockedWithHold(64), Start, false, accept));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheMarketIsJoinedByDisplayNameNotBySymbol()
    {
        var callout = new MissionCallout { Markets = Market("Low Temperature Diamonds", 12) };
        var accept = Cargo(1, "Mission_Collect_Boom", "lowtemperaturediamond", "Low Temperature Diamonds", 4);

        Assert.Equal("They sell Low Temperature Diamonds here.", Assert.Single(Run(callout, DockedWithHold(64), Start, false, accept)).Text);
    }

    [Fact]
    public void DockingWhereTwoMissionsHandInSaysTheCountAndTheReward()
    {
        var state = Commander(
            Accept(1, "Jameson Memorial", 1_400_000, Start.AddDays(2)),
            Accept(2, "jameson memorial", 1_500_000, Start.AddDays(2)),
            Accept(3, "Elsewhere", 900_000, Start.AddDays(2)));

        var said = Run(new MissionCallout(), state, Start, false, Event("Docked", ("StationName", "Jameson Memorial")));

        Assert.Equal("Two missions conclude here. 2.9 million waiting.", Assert.Single(said).Text);
    }

    [Fact]
    public void DockingSomewhereNothingHandsInSaysNothing()
    {
        var state = Commander(Accept(1, "Jameson Memorial", 1_400_000, Start.AddDays(2)));

        Assert.Empty(Run(new MissionCallout(), state, Start, false, Event("Docked", ("StationName", "Elsewhere"))));
    }

    [Fact]
    public void LeavingWithAHandInUnclaimedSaysSoUntilItIsCompleted()
    {
        var state = Commander(Accept(1, "Jameson Memorial", 1_400_000, Start.AddDays(2)));
        var callout = new MissionCallout();

        var said = Run(callout, state, Start, false, Event("Undocked", ("StationName", "Jameson Memorial")));

        Assert.Equal("You're leaving with a hand-in unclaimed at Jameson Memorial.", Assert.Single(said).Text);

        state.Apply(Event("MissionCompleted", ("MissionID", 1L)));

        Assert.Empty(Run(callout, state, Start, false, Event("Undocked", ("StationName", "Jameson Memorial"))));
    }

    [Fact]
    public void EachExpiryWarningIsSpokenOnceAcrossAReplay()
    {
        var state = Commander(Accept(1, "Jameson Memorial", 1_000_000, Start.AddHours(2)));
        var callout = new MissionCallout();
        var said = new List<string>();

        Run(callout, state, Start, true);

        for (var minute = 0; minute <= 125; minute++)
        {
            said.AddRange(Run(callout, state, Start.AddMinutes(minute)).Select(a => a.Text));
        }

        Assert.Equal(["Courier 1 expires in an hour.", "Courier 1 expires in ten minutes."], said);
    }

    [Fact]
    public void ARestartInsideTheHourDoesNotRepeatTheHourWarning()
    {
        var state = Commander(Accept(1, "Jameson Memorial", 1_000_000, Start.AddMinutes(30)));
        var callout = new MissionCallout();

        Run(callout, state, Start, true);

        Assert.Empty(Run(callout, state, Start.AddSeconds(1)));
        Assert.Equal("Courier 1 expires in ten minutes.", Assert.Single(Run(callout, state, Start.AddMinutes(20))).Text);
    }

    [Fact]
    public void AMissionPastTheTenMinuteMarkByMoreThanAMinuteGetsNoWarningAtStart()
    {
        var state = Commander(Accept(1, "Jameson Memorial", 1_000_000, Start.AddMinutes(8)));
        var callout = new MissionCallout();

        Run(callout, state, Start, true);

        Assert.Empty(Run(callout, state, Start.AddSeconds(1)));
    }

    [Fact]
    public void ARowTurnsMissionCalloutsOffAndDefaultsOn()
    {
        Assert.True(new CalloutSettings().Missions);
        Assert.Equal("missions", new MissionCallout().Id);
        Assert.Equal("callouts.missions", CalloutCapability.MissionsKey);
    }

    private static JournalEvent Gold(long id, string kind = "Mission_Mining", int count = 20) =>
        Event("MissionAccepted",
            ("MissionID", id),
            ("Name", kind),
            ("LocalisedName", $"Gold {id}"),
            ("DestinationSystem", "Sol"),
            ("DestinationStation", "Jameson Memorial"),
            ("Commodity", "$Gold_Name;"),
            ("Commodity_Localised", "Gold"),
            ("Count", count),
            ("Expiry", Start.AddDays(2).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")));

    private static CommanderGameState Flying(string system, int gold, bool docked, params JournalEvent[] missions)
    {
        var state = Commander(missions);
        state.Apply(Event(docked ? "Docked" : "Location", ("StarSystem", system), ("StationName", "Jameson Memorial"), ("Docked", docked)));
        state.Hold = Aboard(gold);
        return state;
    }

    private static CargoHold Aboard(int gold, string vessel = "Ship") => new()
    {
        Vessel = vessel,
        Count = gold,
        Items = gold > 0 ? [new CargoItem("gold", gold)] : [],
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private static bool IsReady(Announcement line) => line.Key.StartsWith(MissionCallout.ReadyKey, StringComparison.Ordinal);

    private static string? Ready(MissionCallout callout, CommanderGameState state) =>
        Run(callout, state, Start).SingleOrDefault(IsReady)?.Text;

    [Fact]
    public void AMiningMissionFilledInAnotherSystemSaysWhereToHandInAndOffersToPlotIt()
    {
        var offer = new D47.Core.Conversation.HandInOffer();
        var callout = new MissionCallout { Offer = offer };
        var state = Flying("Wolf 359", 0, false, Gold(1));
        Assert.Null(Ready(callout, state));

        state.Hold = Aboard(20);

        Assert.Equal("All 20 tons of Gold aboard. Hand in at Jameson Memorial in Sol. Say plot it to set the course.", Ready(callout, state));
        Assert.Equal(1, offer.MissionId);
        Assert.Equal("Sol", offer.System);
        Assert.Null(Ready(callout, state));
    }

    [Fact]
    public void AMissionFilledInTheHandInSystemOffersNothing()
    {
        var offer = new D47.Core.Conversation.HandInOffer();

        Assert.Equal(
            "All 20 tons of Gold aboard. Hand in at Jameson Memorial.",
            Ready(new MissionCallout { Offer = offer }, Flying("Sol", 20, false, Gold(1))));
        Assert.Null(offer.System);
    }

    [Fact]
    public void AMissionFilledWhileDockedAtTheHandInSaysToHandItInHere()
    {
        Assert.Equal(
            "All 20 tons of Gold aboard. Hand it in here.",
            Ready(new MissionCallout(), Flying("Sol", 20, true, Gold(1, "Mission_Collect"))));
    }

    [Fact]
    public void AMissionWithASystemAndNoStationSaysTheSystem()
    {
        var accept = Event("MissionAccepted",
            ("MissionID", 1L), ("Name", "Mission_Mining"), ("LocalisedName", "Gold 1"), ("DestinationSystem", "Sol"),
            ("Commodity", "$Gold_Name;"), ("Commodity_Localised", "Gold"), ("Count", 20));

        Assert.Equal(
            "All 20 tons of Gold aboard. Hand-in is in Sol. Say plot it to set the course.",
            Ready(new MissionCallout(), Flying("Wolf 359", 20, false, accept)));
    }

    [Fact]
    public void AMissionAcceptedWithTheHoldAlreadyFullIsAnnouncedOnTheFirstTick()
    {
        var callout = new MissionCallout();
        var state = Flying("Wolf 359", 20, false);
        Assert.Null(Ready(callout, state));

        state.Apply(Gold(1, "Mission_Altruism"));

        Assert.NotNull(Ready(callout, state));
    }

    [Fact]
    public void DeliveryAndSmuggleMissionsAreNeverAnnounced()
    {
        var state = Flying("Wolf 359", 40, false, Gold(1, "Mission_Delivery"), Gold(2, "Mission_Smuggle"));

        Assert.Null(Ready(new MissionCallout(), state));
    }

    [Fact]
    public void TwoMissionsForOneCommodityAreAnnouncedWhenTheHoldCoversEach()
    {
        var callout = new MissionCallout();
        var state = Flying("Wolf 359", 20, false, Gold(1), Gold(2));

        Assert.Single(Run(callout, state, Start), IsReady);

        state.Hold = Aboard(40);

        Assert.Equal("missions.ready.2", Assert.Single(Run(callout, state, Start), IsReady).Key);
    }

    [Fact]
    public void APartLoadAnUnknownHoldAndTheSrvManifestAnnounceNothing()
    {
        var callout = new MissionCallout();
        var state = Flying("Wolf 359", 19, false, Gold(1));
        Assert.Null(Ready(callout, state));

        state.Hold = CargoHold.Empty;
        Assert.Null(Ready(callout, state));

        state.Hold = Aboard(20, "SRV");
        Assert.Null(Ready(callout, state));
    }

    [Fact]
    public void AMissionIsAnnouncedOnceEvenIfTheHoldDropsAndRefills()
    {
        var callout = new MissionCallout();
        var state = Flying("Wolf 359", 20, false, Gold(1));
        Assert.NotNull(Ready(callout, state));

        state.Hold = Aboard(5);
        Assert.Null(Ready(callout, state));

        state.Hold = Aboard(20);
        Assert.Null(Ready(callout, state));
    }

    [Fact]
    public void AMissionAlreadyReadyWhenD47StartsIsNotAnnounced()
    {
        var callout = new MissionCallout();
        var state = Flying("Wolf 359", 20, false, Gold(1));

        Assert.Empty(Run(callout, state, Start, priming: true));
        Assert.Null(Ready(callout, state));
    }
}
