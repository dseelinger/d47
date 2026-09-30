using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Audio;

public class TheStockCoreSpeaksWithoutEffectsTests
{
    private static readonly SpeechSettings Ticked = new()
    {
        GuardianVoice = new GuardianVoiceSettings
        {
            Effects = [.. GuardianVoice.Defaults.Select(effect => effect with { Ticked = effect.Id == "cylon" })],
        },
    };

    [Fact]
    public void CovasGetsNoTreatmentWhateverIsTicked() =>
        Assert.Null(GuardianVoice.ColourFor(Ticked, PersonaCatalog.Covas));

    [Fact]
    public void AGuardianCoreStillGetsTheTickedEffects() =>
        Assert.NotNull(GuardianVoice.ColourFor(Ticked, PersonaCatalog.Warden));
}
