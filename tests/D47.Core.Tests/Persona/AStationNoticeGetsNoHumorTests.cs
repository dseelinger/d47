using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

public sealed class AStationNoticeGetsNoHumorTests
{
    private static readonly FlavourBrief Brief = new() { Speaker = "x", Instruction = "y", NeedsPersona = false, NeedsGameState = false, NeedsAboutMe = false, NeedsScenario = false };

    private static HumorGroup? GroupOf(string family) =>
        FlavourBriefs.HumorGroupOf(
            new Announcement(IncomingMessages.CannedKeyPrefix + family, "line") { Voice = VoiceRole.Comms }, Brief);

    [Theory]
    [InlineData(CannedSpeakers.StationFamily)]
    [InlineData(CannedSpeakers.DockingFailedFamily)]
    public void AStationNoticeOrDockingRefusalHasNoHumorGroup(string family) =>
        Assert.Null(GroupOf(family));

    [Theory]
    [InlineData("DockingChatter")]
    [InlineData("Pirate")]
    public void OtherCannedFamiliesKeepTheNpcHumor(string family) =>
        Assert.Equal(HumorGroup.Npcs, GroupOf(family));
}
