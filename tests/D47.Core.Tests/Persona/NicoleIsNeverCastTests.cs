using D47.Core.Audio;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>Kokoro's whispered voice is never given to a role automatically.</summary>
public class NicoleIsNeverCastTests
{
    private const string Nicole = "af_nicole";

    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        new(Nicole, "Nicole", "en-US", "female"),
        new("af_heart", "Heart", "en-US", "female"),
        new("af_bella", "Bella", "en-US", "female"),
    ];

    private static readonly VoicePairing.Slot[] Covas = [VoicePairing.SlotFor(PersonaCatalog.Covas)];

    [Fact]
    public async Task AModelThatAnswersNicoleIsNotObeyed()
    {
        var chosen = await VoicePairing.ChooseForAsync(
            Voices(),
            Covas,
            taken: [],
            FakeLlmProvider.Answering($"covas = {Nicole}"),
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(Nicole, chosen["covas"]);
    }

    [Fact]
    public async Task TheFallbackNeverPicksNicoleEitherTime()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var chosen = await VoicePairing.ChooseForAsync(
                Voices(),
                Covas,
                taken: [],
                provider: null,
                model: null,
                spend: null,
                prices: null,
                logger: null,
                random: new Random(seed),
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotEqual(Nicole, chosen["covas"]);
        }
    }

    [Fact]
    public async Task AListOfNothingButNicoleCastsNothing()
    {
        var chosen = await VoicePairing.ChooseForAsync(
            [Voices()[0]],
            Covas,
            taken: [],
            provider: null,
            model: null,
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(chosen);
    }

    [Fact]
    public void TheNarratorWithNoVoicePinnedSkipsNicole()
    {
        var cast = new VoiceCast { Pool = [Nicole, "voice-b"], DefaultVoice = "ship-ai" };

        Assert.Equal("voice-b", cast.For(VoiceRole.Narrator).VoiceId);
    }

    [Fact]
    public void ANarratorYouPinnedToNicoleKeepsIt()
    {
        var cast = new VoiceCast { Pool = [Nicole, "voice-b"], DefaultVoice = "ship-ai" };
        cast.Assign(VoiceRole.Narrator, Nicole);

        Assert.Equal(Nicole, cast.For(VoiceRole.Narrator).VoiceId);
    }

    [Fact]
    public void ASenderCanStillBeGivenNicole()
    {
        var cast = new VoiceCast { Pool = [Nicole], DefaultVoice = "ship-ai" };

        Assert.Equal(Nicole, cast.ForSender("Commander Vex", isPlayer: true).VoiceId);
    }
}
