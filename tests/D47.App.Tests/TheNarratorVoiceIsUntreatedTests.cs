using D47.Core.Audio;
using Xunit;

namespace D47.App.Tests;

/// <summary>A ship AI treatment, Guardian or COVAS, never reaches the Narrator or the crew.</summary>
public class TheNarratorVoiceIsUntreatedTests
{
    [Theory]
    [InlineData(VoiceRole.Narrator)]
    [InlineData(VoiceRole.Crew)]
    public async Task ANarratorOrCrewVoiceIsUntreated(VoiceRole role) =>
        Assert.Equal(
            "Scanning.",
            await GuardianVoiceReachesOnlyTheShipAiTests.NameOfWhatWasPlayed(role));

    [Fact]
    public async Task AnOverTheAirRoleStillGetsTheRadio() =>
        Assert.EndsWith(
            "(radio)",
            await GuardianVoiceReachesOnlyTheShipAiTests.NameOfWhatWasPlayed(VoiceRole.Comms),
            StringComparison.Ordinal);
}
