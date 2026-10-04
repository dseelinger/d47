using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>With no Narrator voice pinned, the fallback is a narrator with an English-speaking accent.</summary>
public class AnUnpinnedNarratorIsAnEnglishSpeakingNarratorTests
{
    private const string Ship = "ship";

    private static VoiceInfo Voice(string id, string accent, string? description = null, string name = "Voice") =>
        new(id, name, accent) { Description = description };

    private static VoiceCast Cast(params VoiceInfo[] voices) => new()
    {
        Pool = [.. voices.Select(voice => voice.Id)],
        Voices = voices.ToDictionary(voice => voice.Id, StringComparer.OrdinalIgnoreCase),
        DefaultVoice = Ship,
    };

    [Fact]
    public void ABritishNarrativeVoiceBeatsATurkishNarratorAndACasualAmerican()
    {
        var cast = Cast(
            Voice(Ship, "american"),
            Voice("turkish", "turkish", "Narrator"),
            Voice("casual", "american", "Casual"),
            Voice("british", "british", "use case: narrative story"));

        Assert.Equal("british", cast.For(VoiceRole.Narrator).VoiceId);
    }

    [Fact]
    public void ANarratorInTheNameCountsAsNarration()
    {
        var cast = Cast(
            Voice("casual", "american", "Casual"),
            Voice("louise", "british", name: "Louise - Calm & Neutral Narration"));

        Assert.Equal("louise", cast.For(VoiceRole.Narrator).VoiceId);
    }

    [Fact]
    public void WithOnlyIndianOrUnlabelledNarratorsTheFirstAllowedAccentIsTaken()
    {
        var cast = Cast(
            Voice("indian", "indian", "Narrator"),
            Voice("plain", "", "Narrator"),
            Voice("neutral", "neutral"),
            Voice("american", "american", "Casual"),
            Voice("british", "british"));

        Assert.Equal("american", cast.For(VoiceRole.Narrator).VoiceId);
    }

    [Fact]
    public void WithNoAllowedAccentTheFirstOtherVoiceIsTaken()
    {
        var cast = Cast(Voice(Ship, "american"), Voice("indian", "indian"), Voice("plain", ""));

        Assert.Equal("indian", cast.For(VoiceRole.Narrator).VoiceId);
    }

    [Fact]
    public void APinnedNarratorIsUsedWhateverItsAccent()
    {
        var cast = Cast(Voice("british", "british", "Narrator"), Voice("indian", "indian"));
        cast.Assign(VoiceRole.Narrator, "indian");

        Assert.Equal("indian", cast.For(VoiceRole.Narrator).VoiceId);
    }

    [Fact]
    public void ThePickDoesNotChangeBetweenCalls()
    {
        var cast = Cast(Voice("a", "american"), Voice("b", "british", "Narrator"));

        Assert.Equal(cast.For(VoiceRole.Narrator).VoiceId, cast.For(VoiceRole.Narrator).VoiceId);
    }
}
