using D47.Core.Seats;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Seats;

public class ASeatOverrideBelongsToItsProviderTests
{
    private static readonly IReadOnlyDictionary<string, string> Kokoro =
        new Dictionary<string, string> { ["helm"] = "bm_george" };

    private static readonly IReadOnlyDictionary<string, string> ElevenLabs =
        new Dictionary<string, string> { ["helm"] = "el_helm" };

    private static readonly CrewSeat Overridden =
        new("0000000a", CrewRole.Helm, null, "Ada", new CrewSeatVoice("kokoro", "am_adam"));

    private static bool Everything(string id) => true;

    [Fact]
    public void TheOverrideSpeaksWhileItsProviderDoes()
    {
        Assert.Equal("am_adam", SeatVoices.Resolve(Overridden, "kokoro", Kokoro, Everything));
    }

    [Fact]
    public void AnotherSeatOnTheSameRoleKeepsTheRolesVoice()
    {
        var other = new CrewSeat("0000000b", CrewRole.Helm, null, "Bo");

        Assert.Equal("bm_george", SeatVoices.Resolve(other, "kokoro", Kokoro, Everything));
    }

    [Fact]
    public void AnotherProvidersOverrideIsIgnoredAndTheRolesChoiceApplies()
    {
        Assert.Equal("el_helm", SeatVoices.Resolve(Overridden, "elevenlabs", ElevenLabs, Everything));
    }

    [Fact]
    public void AnotherProvidersOverrideWithNoRoleChoiceFallsThrough()
    {
        Assert.Null(SeatVoices.Resolve(Overridden, "elevenlabs", new Dictionary<string, string>(), Everything));
    }

    [Fact]
    public void AnOverrideTheProviderDoesNotListFallsToTheRole()
    {
        Assert.Equal("bm_george", SeatVoices.Resolve(Overridden, "kokoro", Kokoro, id => id != "am_adam"));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheOverrideSurvivesTheSeatFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"d47-seats-{Guid.NewGuid():N}.json");

        try
        {
            new CrewSeatStore(path, NullLogger<CrewSeatStore>.Instance)
                .Set(new ShipSeats(string.Empty, 7, "type9", [Overridden]));

            var reread = new CrewSeatStore(path, NullLogger<CrewSeatStore>.Instance);
            reread.Poll();

            Assert.Equal(Overridden.Voice, reread.For(null, 7)!.Seats.Single().Voice);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
