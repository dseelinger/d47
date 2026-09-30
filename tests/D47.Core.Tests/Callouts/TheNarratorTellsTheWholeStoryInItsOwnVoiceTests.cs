using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// A narration goes to the model with the whole backstory and the scenario, is captioned "Narrator", and is
/// never spoken in the ship's voice.
/// </summary>
public class TheNarratorTellsTheWholeStoryInItsOwnVoiceTests
{
    private const string Backstory = "Raised on a mining platform in Diaguandri, she left owing money to the wrong people.";

    private const string Scenario = "Working off the debt one smuggling run at a time.";

    private static Announcement Narration(int variant) =>
        new(NarratorCallout.Key, string.Empty) { Voice = VoiceRole.Narrator, Variant = variant };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EveryNarrationCarriesTheWholeBackstory(int variant)
    {
        var brief = Assert.IsType<FlavourBrief>(FlavourBriefs.For(Narration(variant), personalityEnabled: true));

        var story = CommanderStory.Compose("Commander Vale, she/her.", Backstory, withStory: brief.NeedsStory);

        Assert.Contains(Backstory, story, StringComparison.Ordinal);
        Assert.True(brief.NeedsGameState);
        Assert.False(brief.NeedsPersona);
        Assert.NotNull(brief.Speaker);
    }

    [Fact]
    public void TheNarratorsBriefCarriesTheScenarioAtTheAboardAudience()
    {
        var brief = FlavourBriefs.For(Narration(0), personalityEnabled: true)!;

        Assert.Equal(Scenario, FlavourBriefs.ScenarioFor(brief, ScenarioAudience.Aboard, VoiceRole.Narrator, Scenario));
    }

    [Fact]
    public void WithPersonalityOffThereIsNoNarration() =>
        Assert.Null(FlavourBriefs.For(Narration(0), personalityEnabled: false));

    [Fact]
    public void EveryNarrationIsPutToTheModel() =>
        Assert.True(new RewordChance(new Random(1)).ShouldReword(Narration(0), rewordPercent: 0));

    [Fact]
    public void ANarrationIsCaptionedNarrator() =>
        Assert.Equal("Narrator", VoiceRoles.Called(VoiceRole.Narrator));

    [Theory]
    [InlineData(null)]
    [InlineData("ship")]
    public void ANarratorWithNoVoiceOrTheShipsTakesTheFirstThatIsNotTheShips(string? chosen)
    {
        var cast = new VoiceCast { DefaultVoice = "ship", Pool = ["ship", "second", "third"] };
        cast.Assign(VoiceRole.Narrator, chosen);

        Assert.Equal("second", cast.For(VoiceRole.Narrator).VoiceId);
        Assert.Equal("ship", cast.For(VoiceRole.ShipAi).VoiceId);
    }

    [Fact]
    public void ANarratorVoiceThatIsNotTheShipsIsKept()
    {
        var cast = new VoiceCast { DefaultVoice = "ship", Pool = ["ship", "second", "third"] };
        cast.Assign(VoiceRole.Narrator, "third");

        Assert.Equal("third", cast.For(VoiceRole.Narrator).VoiceId);
    }

    [Fact]
    public void TheNarratorSpeaksFromTheShipsProviderInTheCockpit()
    {
        Assert.Equal(VoiceGroup.Aboard, VoiceGroups.Of(VoiceRole.Narrator));
        Assert.False(RadioVoice.IsOverTheAir(VoiceRole.Narrator));
    }

    [Fact]
    public void TheNarratorsVoiceFollowsTheShipsProvider()
    {
        var settings = new D47Settings();
        settings = settings with { Speech = settings.Speech with { Provider = "edge", VoicesProvider = "edge", NarratorVoice = "en-GB-RyanNeural" } };

        var away = VoiceMemory.Switched(settings, VoiceGroup.Aboard, "edge", "elevenlabs");
        Assert.Null(away.Speech.NarratorVoice);

        var back = VoiceMemory.Switched(away with { Speech = away.Speech with { Provider = "edge" } }, VoiceGroup.Aboard, "elevenlabs", "edge");
        Assert.Equal("en-GB-RyanNeural", back.Speech.NarratorVoice);
    }
}
