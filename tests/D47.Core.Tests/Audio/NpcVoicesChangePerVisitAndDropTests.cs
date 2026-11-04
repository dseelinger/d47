using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// How long an NPC's voice lasts: a person for one arrival, a station for one drop, a group ship for one message,
/// and an NPC who interdicted the Commander for the session (#2129).
/// </summary>
public class NpcVoicesChangePerVisitAndDropTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private static VoiceCast Cast() => new()
    {
        Pool = ["voice-a", "voice-b", "voice-c", "voice-d", "voice-e", "voice-f"],
        DefaultVoice = "ship-ai",
    };

    private static JournalEvent Event(string kind, DateTimeOffset at, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            ["event"] = kind,
        };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Message(string from, string localised, DateTimeOffset at) =>
        Event("ReceiveText", at, ("From", from), ("Message", localised), ("Channel", "npc"));

    [Fact]
    public void TwoArrivalsInTheSameSystemCanGiveTheSameNameDifferentVoices()
    {
        var voices = Enumerable.Range(0, 6)
            .Select(visit =>
            {
                var cast = Cast();
                cast.EnteredSystem("Sol", Nine.AddHours(visit));
                return cast.ForSender("Ilse Bruhn", isPlayer: false).VoiceId;
            })
            .ToArray();

        Assert.True(voices.Distinct(StringComparer.Ordinal).Count() > 1);
    }

    [Fact]
    public void AnArrivalIsStampedFromItsEventAndAReplayGivesTheSameVoice()
    {
        var events = new[]
        {
            Event("FSDJump", Nine, ("StarSystem", "Sol")),
            Event("FSDJump", Nine.AddMinutes(10), ("StarSystem", "Wolf 359")),
            Event("FSDJump", Nine.AddMinutes(20), ("StarSystem", "Sol")),
        };

        string? VoiceAfter(int arrivals)
        {
            var casting = new VoiceCasting();
            var cast = casting.Of("edge");
            cast.Pool = Cast().Pool;
            cast.DefaultVoice = "ship-ai";
            casting.Observe(events.Take(arrivals));
            return cast.ForSender("Ilse Bruhn", isPlayer: false).VoiceId;
        }

        var stamped = Cast();
        stamped.EnteredSystem("Sol", Nine.AddMinutes(20));

        Assert.Equal(stamped.ForSender("Ilse Bruhn", isPlayer: false).VoiceId, VoiceAfter(3));
        Assert.Equal(VoiceAfter(3), VoiceAfter(3));
    }

    [Fact]
    public void AStationCanSoundDifferentAfterLeavingNormalSpaceAndDroppingAgain()
    {
        var casting = new VoiceCasting();
        var cast = casting.Of("edge");
        cast.Pool = Cast().Pool;
        cast.DefaultVoice = "ship-ai";

        casting.Observe([Event("FSDJump", Nine, ("StarSystem", "Lave"))]);
        var voices = new List<string?>();

        for (var drop = 0; drop < 6; drop++)
        {
            casting.Observe([Event("SupercruiseExit", Nine.AddMinutes(10 * drop + 1), ("StarSystem", "Lave"))]);

            var first = cast.ForSender("Lave Station", isPlayer: false, kind: SenderKind.Station).VoiceId;
            Assert.Equal(first, cast.ForSender("Lave Station", isPlayer: false, kind: SenderKind.Station).VoiceId);
            voices.Add(first);

            casting.Observe([Event("SupercruiseEntry", Nine.AddMinutes(10 * drop + 5), ("StarSystem", "Lave"))]);
            Assert.DoesNotContain(cast.MetHere, met => met.Name == "Lave Station");
        }

        Assert.True(voices.Distinct(StringComparer.Ordinal).Count() > 1);
    }

    [Fact]
    public void ConsecutiveMessagesFromAnAuthorityShipCanComeInDifferentVoices()
    {
        var cast = Cast();
        cast.EnteredSystem("Lave", Nine);

        var first = cast.ForSender("System Authority Vessel", isPlayer: false, kind: SenderKind.GroupShip, sentAt: Nine.AddMinutes(1)).VoiceId;
        var second = cast.ForSender("System Authority Vessel", isPlayer: false, kind: SenderKind.GroupShip, sentAt: Nine.AddMinutes(2)).VoiceId;

        Assert.NotEqual(first, second);

        // The same message asked about again, for its accent brief or a reply, is in the same voice.
        Assert.Equal(first, cast.ForSender("System Authority Vessel", isPlayer: false, kind: SenderKind.GroupShip, sentAt: Nine.AddMinutes(1)).VoiceId);
    }

    [Fact]
    public void TheSameAuthorityMessageGetsTheSameVoiceOnASecondRun()
    {
        string? Run()
        {
            var cast = Cast();
            cast.EnteredSystem("Lave", Nine);
            return cast.ForSender("System Authority Vessel", isPlayer: false, kind: SenderKind.GroupShip, sentAt: Nine.AddMinutes(1)).VoiceId;
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void AnInterdictorKeepsItsVoiceIntoTheNextSystemAndNobodyElseDoes()
    {
        var casting = new VoiceCasting();
        var cast = casting.Of("edge");
        cast.Pool = Cast().Pool;
        cast.DefaultVoice = "ship-ai";

        casting.Observe([Event("FSDJump", Nine, ("StarSystem", "Lave"))]);

        var pirate = cast.ForSender("Richy Reay", isPlayer: false).VoiceId;
        cast.ForSender("Ilse Bruhn", isPlayer: false);

        casting.Observe(
        [
            Event("Interdicted", Nine.AddMinutes(1), ("Submitted", true), ("Interdictor", "Richy Reay"), ("IsPlayer", false)),
            Event("FSDJump", Nine.AddMinutes(5), ("StarSystem", "Leesti")),
        ]);

        Assert.Contains(cast.MetHere, met => met.Name == "Richy Reay" && met.VoiceId == pirate);
        Assert.DoesNotContain(cast.MetHere, met => met.Name == "Ilse Bruhn");
        Assert.Equal(pirate, cast.ForSender("Richy Reay", isPlayer: false).VoiceId);
    }

    [Fact]
    public void AnotherCommandersInterdictionKeepsNobody()
    {
        var casting = new VoiceCasting();
        var cast = casting.Of("edge");
        cast.Pool = Cast().Pool;

        cast.ForSender("Vex", isPlayer: false);
        casting.Observe(
        [
            Event("EscapeInterdiction", Nine, ("Interdictor", "Vex"), ("IsPlayer", true)),
            Event("FSDJump", Nine.AddMinutes(5), ("StarSystem", "Leesti")),
        ]);

        Assert.DoesNotContain(cast.MetHere, met => met.Name == "Vex");
    }

    [Fact]
    public void ACastMadeLaterStartsInTheScopesAlreadyOpen()
    {
        var casting = new VoiceCasting();
        casting.Observe([Event("Location", Nine, ("StarSystem", "Lave"), ("Docked", true))]);

        var cast = casting.Of("edge");

        Assert.Equal("Lave", cast.System);
        Assert.Equal(Nine, cast.Arrival);
        Assert.Equal(Nine, cast.Drop);
    }

    [Theory]
    [InlineData("$npc_name_decorate:#name=Ilse Bruhn;", "npc", SenderKind.Person)]
    [InlineData("$ShipName_PassengerLiner_Cruise;", "npc", SenderKind.Person)]
    [InlineData("$EXT_PANEL_ColonisationShip;", "npc", SenderKind.Person)]
    [InlineData("$ShipName_Police_Independent;", "npc", SenderKind.GroupShip)]
    [InlineData("$ShipName_Police_Federation;", "npc", SenderKind.GroupShip)]
    [InlineData("$ShipName_Military_Empire;", "npc", SenderKind.GroupShip)]
    [InlineData("$ShipName_PowersSecurity;", "npc", SenderKind.GroupShip)]
    [InlineData("Lave Station", "npc", SenderKind.Station)]
    [InlineData("GDS PREDATOR B0X-79X", "npc", SenderKind.Station)]
    [InlineData("Vex", "local", SenderKind.Person)]
    public void TheRawSenderSaysWhatKindItIs(string from, string channel, SenderKind kind) =>
        Assert.Equal(kind, IncomingMessages.KindOf(from, channel));

    [Fact]
    public void AMessageCarriesItsKindAndTimeToTheVoice()
    {
        var reader = new IncomingMessages { Enabled = () => true, IncludeNpcs = () => true };

        var read = reader.Read(Event(
            "ReceiveText",
            Nine,
            ("From", "$ShipName_Police_Independent;"),
            ("From_Localised", "System Authority Vessel"),
            ("Message", "Stand down."),
            ("Channel", "npc")));

        Assert.NotNull(read);
        Assert.Equal(SenderKind.GroupShip, read.SpeakerKind);
        Assert.Equal(Nine, read.SentAt);
    }

    [Fact]
    public void ThePatrolNearTheCommandersCarrierIsAGroupShipToo()
    {
        var reader = new IncomingMessages
        {
            Enabled = () => true,
            IncludeNpcs = () => true,
            AuthorityNearOwnCarrier = () => true,
        };

        var read = reader.Read(Event(
            "ReceiveText",
            Nine,
            ("From", "$ShipName_Police_Independent;"),
            ("From_Localised", "System Authority Vessel"),
            ("Message", "$Police_Scan_NothingFound;"),
            ("Message_Localised", "We didn't find anything on you... this time."),
            ("Channel", "npc")));

        Assert.NotNull(read);
        Assert.Equal(IncomingMessages.AuthorityCannedKey, read.Key);
        Assert.Equal(SenderKind.GroupShip, read.SpeakerKind);
        Assert.Equal(Nine, read.SentAt);
    }

    [Fact]
    public void AStationMessageIsVoicedForTheDrop()
    {
        var cast = Cast();
        cast.EnteredSystem("Lave", Nine);
        cast.Dropped(Nine.AddMinutes(1));

        var read = new IncomingMessages { Enabled = () => true, IncludeNpcs = () => true }
            .Read(Message("Lave Station", "Welcome.", Nine.AddMinutes(2)));

        Assert.NotNull(read);
        var voice = SpeakerAccent.VoiceOf(cast, read).VoiceId;

        Assert.Contains(cast.MetHere, met => met.Name == "Lave Station" && met.VoiceId == voice);

        cast.LeftNormalSpace();
        Assert.DoesNotContain(cast.MetHere, met => met.Name == "Lave Station");
    }
}
