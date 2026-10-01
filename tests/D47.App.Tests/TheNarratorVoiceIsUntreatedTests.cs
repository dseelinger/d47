using D47.Core.Audio;
using Xunit;

namespace D47.App.Tests;

/// <summary>A ship AI treatment, Guardian or COVAS, never reaches the Narrator or the crew.</summary>
public class TheNarratorVoiceIsUntreatedTests
{
    [Fact]
    public async Task TheNarratorIsUntreated() =>
        Assert.Equal(
            "Scanning.",
            await GuardianVoiceReachesOnlyTheShipAiTests.NameOfWhatWasPlayed(VoiceRole.Narrator));

    [Fact]
    public async Task TheCrewIsUntreated() =>
        Assert.Equal(
            "Scanning.",
            await GuardianVoiceReachesOnlyTheShipAiTests.NameOfWhatWasPlayed(VoiceRole.Crew));

    [Fact]
    public async Task AnOverTheAirRoleStillGetsTheRadio() =>
        Assert.EndsWith(
            "(radio)",
            await GuardianVoiceReachesOnlyTheShipAiTests.NameOfWhatWasPlayed(VoiceRole.Comms),
            StringComparison.Ordinal);
}
