using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>Comms traffic and invented chatter are tagged where the announcement is made; the arbiter group is unchanged.</summary>
public class AnAnnouncementSaysWhereItCameFromTests
{
    private static readonly DateTimeOffset T0 = new(3311, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static Announcement? Received(string from, string message, string channel) =>
        new IncomingMessages { Enabled = () => true, IncludeNpcs = () => true }.Read(Event(
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["timestamp"] = "2026-02-10T09:00:00Z",
                ["event"] = "ReceiveText",
                ["From"] = from,
                ["Message"] = message,
                ["Message_Localised"] = message,
                ["Channel"] = channel,
            })));

    [Fact]
    public void ANoFireZoneNoticeIsComms()
    {
        var notice = Received("Jameson Memorial", "No fire zone entered.", "npc");

        Assert.Equal(AnnouncementOrigin.Comms, notice!.Origin);
        Assert.Equal(SpokenGroup.Announcement, notice.Group);
    }

    [Fact]
    public void AWingMessageHasNoOrigin()
    {
        var wing = Received("$cmdr_decorate:#name=Vex;", "On my way.", "wing");

        Assert.Equal(AnnouncementOrigin.None, wing!.Origin);
        Assert.Equal(SpokenGroup.Announcement, wing.Group);
    }

    [Fact]
    public void ANpcChatterMarkerIsInventedChatter()
    {
        var callout = new NpcChatterCallout(new NearbyFight())
        {
            Interval = TimeSpan.FromMinutes(20),
            Longest = TimeSpan.FromMinutes(20),
            Settle = TimeSpan.Zero,
        };

        _ = callout.Examine(Context(T0)).ToList();
        var marker = Assert.Single(callout.Examine(Context(T0 + TimeSpan.FromMinutes(21))));

        Assert.Equal(AnnouncementOrigin.InventedChatter, marker.Origin);
        Assert.Equal(SpokenGroup.Announcement, marker.Group);
    }

    [Fact]
    public void ASceneMarkerIsInventedChatter()
    {
        var tracker = new SceneTracker();
        var callout = new SceneCallout(tracker) { Scenario = () => "Running guns for the resistance." };

        _ = callout.Examine(Context(T0, priming: true)).ToList();

        var interdiction = Event(
            """{ "timestamp":"2026-08-29T00:00:32Z", "event":"Interdicted", "Submitted":true, "Interdictor":"Ramona Starburst", "IsPlayer":false, "Faction":"Pirates of Kuwemaki" }""");
        tracker.Fold(interdiction);

        var marker = Assert.Single(callout.Examine(Context(T0 + TimeSpan.FromSeconds(1), events: [interdiction])));

        Assert.Equal(SceneCallout.Key, marker.Key);
        Assert.Equal(AnnouncementOrigin.InventedChatter, marker.Origin);
        Assert.Equal(SpokenGroup.Announcement, marker.Group);
    }

    [Fact]
    public void AnAmbientRemarkIsInventedChatter()
    {
        var callout = new AmbientCallout
        {
            Enabled = () => true,
            Interval = TimeSpan.FromMinutes(15),
            Longest = TimeSpan.FromMinutes(15),
            Settle = TimeSpan.FromSeconds(90),
        };
        var docked = GameStatus.Unknown with { Flags = StatusFlags.Docked | StatusFlags.InMainShip };

        _ = callout.Examine(Context(T0, status: docked)).ToList();
        var remark = Assert.Single(callout.Examine(Context(T0 + TimeSpan.FromMinutes(16), status: docked)));

        Assert.Equal(AnnouncementOrigin.InventedChatter, remark.Origin);
        Assert.Equal(SpokenGroup.Announcement, remark.Group);
    }

    [Fact]
    public void ACalloutAndANarratorLineHaveNoOrigin()
    {
        Assert.Equal(AnnouncementOrigin.None, new Announcement("fuel.low", "Fuel is low.").Origin);
        Assert.Equal(
            AnnouncementOrigin.None,
            new Announcement(NarratorCallout.KeyPrefix, "The ship hums.") { Voice = VoiceRole.Narrator }.Origin);
    }

    private static CalloutContext Context(
        DateTimeOffset now,
        bool priming = false,
        GameStatus? status = null,
        IReadOnlyList<JournalEvent>? events = null) =>
        new(
            now,
            priming,
            null,
            status ?? GameStatus.Unknown with { Flags = StatusFlags.Docked | StatusFlags.InMainShip },
            NavRoute.None,
            events ?? []);
}
