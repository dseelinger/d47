using D47.Core.Audio;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A stock core's own voice never reads the hidden layer; the Narrator, the NPCs and any other core do.</summary>
public sealed class CovasDoesNotReadTheHiddenStoryTests
{
    [Fact]
    public void CovasSpeakingAsTheShipGetsNoHiddenStory()
    {
        using var fixtures = PauseSupport.Picked(out _);

        Assert.Null(fixtures.Director.HiddenBrief("F1", VoiceRole.ShipAi, PersonaCatalog.Covas));
    }

    [Fact]
    public void KexSpeakingAsTheShipGetsTheHiddenStory()
    {
        using var fixtures = PauseSupport.Picked(out _);

        Assert.Equal(fixtures.Director.HiddenBrief("F1"), fixtures.Director.HiddenBrief("F1", VoiceRole.ShipAi, PersonaCatalog.Kex));
    }

    [Theory]
    [InlineData(VoiceRole.Narrator)]
    [InlineData(VoiceRole.Comms)]
    public void TheNarratorAndTheNpcsGetTheHiddenStoryWithEitherCore(VoiceRole speaker)
    {
        using var fixtures = PauseSupport.Picked(out _);

        Assert.NotNull(fixtures.Director.HiddenBrief("F1", speaker, PersonaCatalog.Covas));
        Assert.NotNull(fixtures.Director.HiddenBrief("F1", speaker, PersonaCatalog.Kex));
    }
}
