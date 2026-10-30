using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class AHandInIsAnnouncedOnlyWhenItCanCompleteTests
{
    private const string Station = "Crater Outpost";

    private static readonly DateTimeOffset Start = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string kind, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?> { ["timestamp"] = "2026-09-30T12:00:00Z", ["event"] = kind };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Accept(long id, string name, string? commodity = null, int? count = null, int expiryHours = 48) =>
        Event("MissionAccepted",
            ("MissionID", id),
            ("Name", name),
            ("LocalisedName", $"Mission {id}"),
            ("DestinationSystem", "Sol"),
            ("DestinationStation", Station),
            ("Commodity", commodity is null ? null : $"${commodity}_Name;"),
            ("Count", count),
            ("Reward", 1_000_000L),
            ("Expiry", Start.AddHours(expiryHours).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")));

    private static CommanderGameState Commander(int? aboard, params JournalEvent[] events)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        foreach (var journalEvent in events)
        {
            state.Apply(journalEvent);
        }

        if (aboard is { } tonnes)
        {
            state.Hold = new CargoHold
            {
                Vessel = "Ship",
                Count = tonnes,
                Items = tonnes > 0 ? [new CargoItem("grain", tonnes)] : [],
                ReadAt = DateTimeOffset.UnixEpoch,
            };
        }

        return state;
    }

    private static List<Announcement> Run(CommanderGameState state, string kind) =>
        [.. new MissionCallout().Examine(new CalloutContext(
            Start, false, state, GameStatus.Unknown, NavRoute.None, [Event(kind, ("StationName", Station))]))];

    private static string? Said(CommanderGameState state, string kind) =>
        Run(state, kind).SingleOrDefault(line => line.Key is MissionCallout.HandInKey or MissionCallout.UnclaimedKey)?.Text;

    [Fact]
    public void FourCollectMissionsWithAnEmptyHoldSayNothingOnUndocking()
    {
        var state = Commander(0, Accept(1, "Mission_Collect", "grain", 8), Accept(2, "Mission_Collect", "grain", 8),
            Accept(3, "Mission_Collect", "grain", 8), Accept(4, "Mission_Collect", "grain", 8));

        Assert.Null(Said(state, "Undocked"));
        Assert.Null(Said(state, "Docked"));
    }

    [Fact]
    public void ACollectMissionWithItsCargoAboardIsUnclaimedOnUndocking()
    {
        var state = Commander(8, Accept(1, "Mission_Collect", "grain", 8), Accept(2, "Mission_Collect", "grain", 99));

        Assert.Equal($"You're leaving with a hand-in unclaimed at {Station}.", Said(state, "Undocked"));
    }

    [Fact]
    public void DockingNamesOnlyTheReadyMissionsAndTheirReward()
    {
        var state = Commander(8, Accept(1, "Mission_Collect", "grain", 8), Accept(2, "Mission_Collect", "grain", 99));

        var said = Said(state, "Docked");

        Assert.StartsWith("One mission concludes here.", said);
        Assert.Contains("million", said);
    }

    [Fact]
    public void ACollectMissionOwingMoreThanTheHoldIsNotReady()
    {
        var state = Commander(5, Accept(1, "Mission_Collect", "grain", 8));

        Assert.Null(Said(state, "Docked"));
    }

    [Fact]
    public void APartlyDeliveredCollectMissionIsReadyWhenTheRestIsAboard()
    {
        var state = Commander(3, Accept(1, "Mission_Collect", "grain", 8));
        state.Apply(Event("CargoDepot", ("MissionID", 1L), ("ItemsCollected", 8), ("ItemsDelivered", 5), ("TotalItemsToDeliver", 8)));

        Assert.NotNull(Said(state, "Docked"));
    }

    [Fact]
    public void TwoMissionsForTheSameCargoShareTheHoldSoonestExpiryFirst()
    {
        var state = Commander(5, Accept(1, "Mission_Collect", "grain", 5, 48), Accept(2, "Mission_Collect", "grain", 5, 24));

        var ready = MissionReadiness.ReadyAt(state.Missions, Station, state.Hold);

        Assert.Equal([2L], ready.Select(mission => mission.Id));
    }

    [Fact]
    public void ACourierIsReadyOnArrival()
    {
        var state = Commander(0, Accept(1, "Mission_Courier"));

        Assert.NotNull(Said(state, "Docked"));
    }

    [Fact]
    public void ACargoMissionIsNotReadyWhileTheHoldIsUnknown()
    {
        var state = Commander(null, Accept(1, "Mission_Collect", "grain", 8));

        Assert.Null(Said(state, "Docked"));
    }

    [Fact]
    public void ACargoMissionWithoutItsAcceptHasNoFigureToMeet()
    {
        var state = Commander(8, Event("Missions", ("Active", new[] { new { MissionID = 1L, Name = "Mission_Collect", PassengerMission = false, Expires = 3600 } })));

        Assert.Empty(MissionReadiness.Ready(state.Missions.Missions, state.Hold));
    }

    [Fact]
    public void AMassacreAtItsAcceptTimeDestinationIsNotAHandIn()
    {
        var state = Commander(0, Accept(1, "Mission_Massacre"));

        Assert.Null(Said(state, "Docked"));
        Assert.Null(Said(state, "Undocked"));
    }

    [Fact]
    public void ARedirectedMassacreHandsInAtTheNewStation()
    {
        var state = Commander(0, Accept(1, "Mission_Massacre"));
        state.Apply(Event("MissionRedirected", ("MissionID", 1L), ("NewDestinationSystem", "Sol"), ("NewDestinationStation", Station)));

        Assert.NotNull(Said(state, "Docked"));
    }

    [Fact]
    public void AnOnFootMissionAtItsSettlementIsNotAHandInBeforeTheRedirect()
    {
        var state = Commander(0, Accept(1, "Mission_OnFoot_Reboot"));

        Assert.Null(Said(state, "Docked"));
    }

    [Fact]
    public void ANameNotInTheCatalogueIsNotReadyUntilRedirected()
    {
        var state = Commander(0, Accept(1, "Mission_Invented_Name"));

        Assert.Null(Said(state, "Docked"));
    }
}
