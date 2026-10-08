using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

public class AMissingChatterboxVoiceHasAShippedStandInTests
{
    private static ChatterboxVoice Voice(string id, string gender, string pitch, string pace, VoiceRole? role = null, bool shipped = true) =>
        new(new VoiceInfo(id, id, "en", gender), role, "a test clip", id + ".wav") { Shipped = shipped, Pitch = pitch, Pace = pace };

    private static readonly ChatterboxVoice[] Shipped =
    [
        Voice("marlow", "female", "mid", "even", VoiceRole.ShipAi),
        Voice("isolde", "female", "high", "brisk", VoiceRole.Comms),
        Voice("odette", "female", "low", "slow"),
        Voice("corwin", "male", "mid", "even", VoiceRole.ShipAi),
    ];

    [Fact]
    public void TheRolesShippedVoiceOfTheSameGenderStandsIn()
    {
        var wanted = Voice("wren", "female", "low", "slow", shipped: false);

        Assert.Equal("marlow", ChatterboxCatalog.StandIn(wanted, VoiceRole.ShipAi, Shipped)!.Voice.Id);
    }

    [Fact]
    public void WithNoRoleTheNearestPitchAndPaceOfTheSameGenderStandsIn()
    {
        var wanted = Voice("wren", "female", "low", "slow", shipped: false);

        Assert.Equal("odette", ChatterboxCatalog.StandIn(wanted, null, Shipped)!.Voice.Id);
    }

    [Fact]
    public void GenderComesBeforeRole()
    {
        var wanted = Voice("bram", "male", "high", "brisk", shipped: false);

        Assert.Equal("corwin", ChatterboxCatalog.StandIn(wanted, VoiceRole.Comms, Shipped)!.Voice.Id);
    }

    [Fact]
    public void AVoiceThatIsNotShippedNeverStandsIn()
    {
        var wanted = Voice("bram", "male", "mid", "even", shipped: false);

        Assert.Null(ChatterboxCatalog.StandIn(wanted, null, [Voice("other", "male", "mid", "even", shipped: false)]));
    }
}
