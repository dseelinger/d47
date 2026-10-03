using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>With a stock core aboard, humor is off and lines with a persona brief are said as written.</summary>
public class CovasSpeaksFlatTests
{
    private static readonly PersonaSettings MaxHumor = new()
    {
        CoreHumor = 10,
        CoreHumorPercent = 100,
        NpcHumor = 10,
        NpcHumorPercent = 100,
        CarrierHumor = 10,
        CarrierHumorPercent = 100,
    };

    private static Announcement Promotion() =>
        new($"{PromotionCallout.KeyPrefix}combat", "Promoted. Combat, Expert.");

    private static Task<Announcement?> Vary(Announcement announcement, bool stock, int percent, Action onAsk) =>
        new Rewording(new RewordChance(new Random(1)), null).VaryAsync(
            announcement,
            hasModel: true,
            personalityEnabled: true,
            percent,
            () => ShipFacts.Unknown,
            "Doug",
            (brief, instruction, token) =>
            {
                onAsk();
                return Task.FromResult(new FlavourReply("Well done, Commander!", FlavourMiss.None, null));
            },
            () => stock);

    [Fact]
    public void CovasGetsNoHumorWhateverTheCoreDialIsSetTo()
    {
        Assert.Equal(HumorDial.Off, Humor.DialFor(MaxHumor, HumorGroup.Cores, stockCoreAboard: true));
        Assert.Equal(new HumorDial(10, 100), Humor.DialFor(MaxHumor, HumorGroup.Cores, stockCoreAboard: false));
    }

    [Fact]
    public void NpcAndCarrierHumorAreUnchangedWithCovasAboard()
    {
        Assert.Equal(new HumorDial(10, 100), Humor.DialFor(MaxHumor, HumorGroup.Npcs, stockCoreAboard: true));
        Assert.Equal(new HumorDial(10, 100), Humor.DialFor(MaxHumor, HumorGroup.Carrier, stockCoreAboard: true));
    }

    [Fact]
    public async Task APromotionIsSaidAsWrittenWithNoModelCallWithCovasAboard()
    {
        var asked = 0;

        var said = await Vary(Promotion(), stock: true, percent: 100, () => asked++);

        Assert.Equal("Promoted. Combat, Expert.", said?.Text);
        Assert.Equal(0, asked);
    }

    [Fact]
    public async Task APromotionIsStillRewordedWithAGuardianCoreAboard()
    {
        var asked = 0;

        var said = await Vary(Promotion(), stock: false, percent: 100, () => asked++);

        Assert.Equal("Well done, Commander!", said?.Text);
        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task ALineWithoutAPersonaBriefIsStillRewordedWithCovasAboard()
    {
        var asked = 0;
        var carrier = new Announcement(CarrierCallout.JumpKey, "Jump complete.") { Voice = VoiceRole.CarrierCaptain };

        await Vary(carrier, stock: true, percent: 100, () => asked++);

        Assert.Equal(1, asked);
    }

    [Fact]
    public void CovasIsToldItIsEquipmentThatExpressesNoEmotion()
    {
        var block = PersonaCatalog.Covas.RenderBlock().Replace("\r\n", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

        Assert.Contains("equipment, not an AI companion", block, StringComparison.Ordinal);
        Assert.Contains("You express no emotion", block, StringComparison.Ordinal);
    }
}
