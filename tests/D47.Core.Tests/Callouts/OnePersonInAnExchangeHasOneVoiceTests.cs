using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// One invented person gets one voice, however the model spells their name between lines of the
/// same exchange (<a href="https://github.com/dseelinger/d47/issues/256">#256</a>).
/// <para>
/// Found in the Commander's own log: <c>Courier Vance</c> on the first line and <c>Vance</c> on
/// the third, in two different men's voices, and the other speaker split the same way. The cast
/// keys on the string, so it cannot know; the exchange is the only place the two spellings are
/// knowably one man, so the parser settles them before any line reaches the cast.
/// </para>
/// </summary>
public class OnePersonInAnExchangeHasOneVoiceTests
{
    private const string TheLog =
        "Courier Vance: Type-11, right? Saw it come in. Yours acting up again?\n"
        + "Dock hand Ressa: Coolant loop. Third time this week.\n"
        + "Vance: Rough. I'll swing back after this drop-off, see if you're clear.\n"
        + "Ressa: Do that. I'll have the kettle on.";

    [Fact]
    public void TheExchangeIsDrawnWithOneNamePerPerson()
    {
        var lines = NpcChatter.Parse(TheLog, NpcChatterKind.Passersby);

        Assert.Equal(4, lines.Count);
        Assert.Equal(new[] { "Courier Vance", "Dock hand Ressa", "Courier Vance", "Dock hand Ressa" }, lines.Select(line => line.Name).ToList());
    }

    [Fact]
    public void TheExchangeIsSpokenInOneVoicePerPersonAndLeavesTwoEntriesInTheCast()
    {
        var cast = new VoiceCast { Pool = ["voice-a", "voice-b", "voice-c", "voice-d"] };

        var voices = NpcChatter.Parse(TheLog, NpcChatterKind.Passersby)
            .Select(line => cast.ForSender(line.Name, isPlayer: false, line.Role ?? VoiceRole.Comms).VoiceId)
            .ToList();

        Assert.Equal(voices[0], voices[2]);
        Assert.Equal(voices[1], voices[3]);
        Assert.NotEqual(voices[0], voices[1]);

        // No leftover second entry for the same person survives in the per-system cast.
        Assert.Equal(2, cast.Assignments.PerSystem);
    }

    [Fact]
    public void AFirstNameFoldsTheSameWayAsASurname()
    {
        var lines = NpcChatter.Parse(
            "Vera Kolt: Pad nine again.\n"
            + "Dock Control: Take it up with scheduling.\n"
            + "Vera: One day I will.",
            NpcChatterKind.Controller);

        Assert.Equal(new[] { "Vera Kolt", "Dock Control", "Vera Kolt" }, lines.Select(line => line.Name).ToList());
    }

    [Fact]
    public void TwoPeopleWhoShareASurnameStayTwoPeople()
    {
        var lines = NpcChatter.Parse(
            "Mara Vance: You took my pad.\n"
            + "Tom Vance: Dispatch gave it to me.\n"
            + "Vance: Which of us are you shouting at?\n"
            + "Mara Vance: Both.",
            NpcChatterKind.Passersby);

        // The bare surname could be either of them, and a wrong fold is the reported fault pointed
        // the other way — so it stays as written.
        Assert.Equal(new[] { "Mara Vance", "Tom Vance", "Vance", "Mara Vance" }, lines.Select(line => line.Name).ToList());
    }

    [Fact]
    public void TheCarriersOwnPostsTakeNoPartInTheFolding()
    {
        var mine = CarrierState.None with
        {
            CallSign = "K7Q-B4Z",
            Name = "Nomad's Rest",
            CarrierId = 3_700_123_456,
            StarSystem = "Shinrarta Dezhra",
            DestinationSystem = "Sol",
        };

        var here = NpcChatterCarrier.Of(
            mine,
            new JournalLocation("Shinrarta Dezhra", null, true, "K7Q-B4Z")
            {
                Mode = FlightMode.Docked, StationType = "FleetCarrier", MarketId = 3_700_123_456,
            });

        Assert.True(here.Present);

        var lines = NpcChatter.Parse(
            "Captain: Pad four, and mind the paint this time.\n"
            + "Captain Reyes: Mind your own paint.",
            NpcChatterKind.Controller,
            here);

        // "Captain Reyes" is a pilot with a rank, not a second spelling of the captain: the post
        // keeps its role and its name, and the pilot keeps hers.
        Assert.Equal(2, lines.Count);
        Assert.Equal(("Captain", VoiceRole.CarrierCaptain), (lines[0].Name, lines[0].Role));
        Assert.Equal(("Captain Reyes", null), (lines[1].Name, lines[1].Role));
    }

    [Fact]
    public void TheBriefAsksForOneSpellingPerSpeaker()
    {
        foreach (var kind in Enum.GetValues<NpcChatterKind>())
        {
            Assert.Contains(
                "written the same way on every line",
                NpcChatter.Instruction(kind),
                StringComparison.Ordinal);
        }
    }
}
