using D47.Core.Audio;
using D47.Core.Callouts;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class TheCarrierBriefNamesTheOwnerTests
{
    private static Announcement Canned() => new(IncomingMessages.CarrierCannedKey, "Docking request granted.")
    {
        Voice = VoiceRole.TowerControl,
        CommsChannel = "npc",
        Transcript = "Docking request granted.",
    };

    [Fact]
    public void TheBriefAsksForRankAndSurname()
    {
        var brief = FlavourBriefs.For(Canned(), personalityEnabled: true, commanderName: "JOHN DEPARAGON")!;

        Assert.Contains("Commander DEPARAGON", brief.Instruction, StringComparison.Ordinal);
        Assert.DoesNotContain("JOHN DEPARAGON", brief.Instruction, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void WithoutANameTheBriefAsksForCommanderAlone(string? name)
    {
        var brief = FlavourBriefs.For(Canned(), personalityEnabled: true, commanderName: name)!;

        Assert.DoesNotContain("surname", brief.Instruction, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("last word of their name", brief.Instruction, StringComparison.Ordinal);
    }
}
