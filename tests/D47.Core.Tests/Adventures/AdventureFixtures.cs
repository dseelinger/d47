using D47.Core.Adventures;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>The one invented story every adventure test folds, and the events that drive it.</summary>
internal static class AdventureFixtures
{
    public const long Lantern = 2870130552490;
    public const long QuietField = 2870211994018;
    public const long Hollow = 2870300000001;
    public const long Veyl = 2871004117263;
    public const long Home = 2869440911642;
    public const long Anchorage = 3700481092;

    public static readonly DateTimeOffset Accepted = new(2026, 8, 22, 19, 43, 50, TimeSpan.Zero);

    /// <summary>The Lantern Route: arrive, scan, dock, land, arrive. Every place invented.</summary>
    public static Adventure LanternRoute(DateTimeOffset? acceptedAt = null) => new()
    {
        Key = "the-lantern-route",
        Name = "The Lantern Route",
        Source = AdventureSource.Generated,
        Written = Accepted.AddMinutes(-3),
        Spine = new AdventureSpine
        {
            Premise = "An outpost abandoned in 3302 still runs a beacon.",
            Want = "To find out who keeps it running, and why.",
            Stake = "Whether a place left behind can still be owed to.",
            Turn = "The beacon speaks to one person by name.",
            Ending = "She died forty kilometres short of it.",
        },
        Opening = "Beacons cost money. Somebody is paying.",
        Beats =
        [
            Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
            Beat("The Survey", "catalyst", new AdventureTrigger { Kind = TriggerKind.Scan, SystemAddress = QuietField, BodyId = 6, System = "The Quiet Field", Body = "The Quiet Field A 2" }, "Filed in 3306."),
            Beat("The Anchorage", "midpoint", new AdventureTrigger { Kind = TriggerKind.Dock, MarketId = Anchorage, System = "Dyson's Hollow", Station = "Maren Anchorage" }, "To one name."),
            Beat("Veyl 3 c", "all is lost", new AdventureTrigger { Kind = TriggerKind.Land, SystemAddress = Veyl, BodyId = 9, System = "Cairn of Veyl", Body = "Veyl 3 c" }, "Forty kilometres short."),
            Beat("Tavell's Reach", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Home, System = "Tavell's Reach" }, "Eleven months left."),
        ],
        AcceptedAt = acceptedAt,
    };

    public static AdventureBeat Beat(string title, string? function, AdventureTrigger trigger, string line) => new()
    {
        Title = title,
        Function = function,
        Trigger = trigger,
        Line = line,
    };

    public static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed), json);
        return parsed!;
    }

    public static string Stamp(DateTimeOffset at) => at.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    public static JournalEvent Jump(long systemAddress, DateTimeOffset at, string name = "Somewhere") =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"FSDJump", "StarSystem":"{{name}}", "SystemAddress":{{systemAddress}}, "StarPos":[0,0,0] }""");

    public static JournalEvent Location(long systemAddress, DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"Location", "StarSystem":"Somewhere", "SystemAddress":{{systemAddress}}, "Docked":false }""");

    public static JournalEvent Scan(long systemAddress, int bodyId, DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"Scan", "ScanType":"Detailed", "BodyName":"A body", "BodyID":{{bodyId}}, "StarSystem":"Somewhere", "SystemAddress":{{systemAddress}} }""");

    public static JournalEvent Docked(long marketId, DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"Docked", "StationName":"Some Station", "StationType":"Outpost", "StarSystem":"Somewhere", "SystemAddress":1, "MarketID":{{marketId}} }""");

    public static JournalEvent Touchdown(long systemAddress, int bodyId, DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"Touchdown", "PlayerControlled":true, "StarSystem":"Somewhere", "SystemAddress":{{systemAddress}}, "Body":"A moon", "BodyID":{{bodyId}}, "OnPlanet":true }""");

    public static JournalEvent Promotion(string career, int rank, DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"Promotion", "{{career}}":{{rank}} }""");

    public static JournalEvent Commander(string fid, DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"Commander", "FID":"{{fid}}", "Name":"Tester" }""");

    /// <summary>The whole route, in order, each a minute apart from the acceptance.</summary>
    public static IReadOnlyList<JournalEvent> WholeRoute(DateTimeOffset from) =>
    [
        Jump(Lantern, from.AddMinutes(1)),
        Scan(QuietField, 6, from.AddMinutes(2)),
        Docked(Anchorage, from.AddMinutes(3)),
        Touchdown(Veyl, 9, from.AddMinutes(4)),
        Jump(Home, from.AddMinutes(5)),
    ];
}
