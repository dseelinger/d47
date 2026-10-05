using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Seats;
using Xunit;

namespace D47.Core.Tests.Seats;

public class EachRoleKeepsItsVoiceAcrossShipsTests
{
    private static readonly IReadOnlyDictionary<string, string> Helm =
        new Dictionary<string, string> { [SeatVoices.KeyOf(CrewRole.Helm)] = "bm_george" };

    private static bool Everything(string id) => true;

    private static CrewSeat Seat(string id, CrewRole role, CrewSeatVoice? voice = null) =>
        new(id, role, null, $"Name{id}", voice);

    private static VoiceCast CastFor(params CrewSeat[] seats) => new()
    {
        DefaultVoice = "af_heart",
        Pool = ["am_adam", "bm_george", "af_heart", "bf_emma"],
        SeatVoice = id => seats.FirstOrDefault(seat => seat.Id == id) is { } seat
            ? SeatVoices.Resolve(seat, "kokoro", Helm, Everything)
            : null,
    };

    [Fact]
    public void EveryHelmSeatOnEveryShipSpeaksInTheRolesVoice()
    {
        var cast = CastFor(Seat("00000001", CrewRole.Helm), Seat("00000002", CrewRole.Helm));

        Assert.Equal("bm_george", cast.ForSender("Ada", false, VoiceRole.Crew, seatId: "00000001").VoiceId);
        Assert.Equal("bm_george", cast.ForSender("Bo", false, VoiceRole.Crew, seatId: "00000002").VoiceId);
    }

    [Fact]
    public void ARoleWithNoVoiceFallsThroughToThePool()
    {
        var seat = Seat("00000003", CrewRole.Navigation);

        Assert.Null(SeatVoices.Resolve(seat, "kokoro", Helm, Everything));
    }

    [Fact]
    public void AVoiceTheProviderDoesNotListFallsThrough()
    {
        var seat = Seat("00000004", CrewRole.Helm);

        Assert.Null(SeatVoices.Resolve(seat, "kokoro", Helm, id => id != "bm_george"));
    }

    [Fact]
    public void ASeatWithARoleVoiceBeatsAPinnedCrewVoiceAndAHiredPilotKeepsIt()
    {
        var cast = CastFor(Seat("00000005", CrewRole.Helm));
        cast.Assign(VoiceRole.Crew, "bf_emma");

        Assert.Equal("bm_george", cast.ForSender("Ada", false, VoiceRole.Crew, seatId: "00000005").VoiceId);
        Assert.Equal("bf_emma", cast.ForSender("Hired pilot", false, VoiceRole.Crew).VoiceId);
    }

    [Fact]
    public void ACustomSeatUsesNoRoleVoice()
    {
        var roles = new Dictionary<string, string> { ["custom"] = "bf_emma" };

        Assert.Null(SeatVoices.Resolve(Seat("00000006", CrewRole.Custom), "kokoro", roles, Everything));
    }

    [Fact]
    public void RoleVoicesAreKeptPerProviderAndComeBack()
    {
        var settings = new D47Settings
        {
            Speech = new SpeechSettings { Provider = "kokoro", VoicesProvider = "kokoro", SeatVoices = Helm },
        };

        var elevenLabs = VoiceMemory.Switched(settings, "kokoro", "elevenlabs");

        Assert.Empty(elevenLabs.Speech.SeatVoices);

        var back = VoiceMemory.Switched(elevenLabs, "elevenlabs", "kokoro");

        Assert.Equal("bm_george", back.Speech.SeatVoices[SeatVoices.KeyOf(CrewRole.Helm)]);
    }

    [Fact]
    public void ASettingsFileWrittenBeforeRoleVoicesLoadsUnchanged()
    {
        var loaded = JsonSerializer.Deserialize<D47Settings>(
            """{"speech":{"provider":"kokoro","voice":"af_heart"}}""", SettingsStore.Json);

        Assert.NotNull(loaded);
        Assert.Equal("af_heart", loaded.Speech.Voice);
        Assert.Empty(loaded.Speech.SeatVoices);
    }

    [Fact]
    public void RoleVoicesSurviveTheSettingsFile()
    {
        var written = JsonSerializer.Serialize(
            new D47Settings { Speech = new SpeechSettings { SeatVoices = Helm } }, SettingsStore.Json);
        var read = JsonSerializer.Deserialize<D47Settings>(written, SettingsStore.Json)!;

        Assert.Equal("bm_george", read.Speech.SeatVoices[SeatVoices.KeyOf(CrewRole.Helm)]);
    }
}
