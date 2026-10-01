using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A stock core never speaks a clue: the Narrator does, whatever the Narrator setting. Any other core speaks it when the Narrator is off.</summary>
public sealed class ACovasClueIsNarratedTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithCovasAboardTheNarratorSpeaksTheClue(bool narratorOn) =>
        Assert.Equal(VoiceRole.Narrator, ClueVoice(PersonaCatalog.Covas, narratorOn));

    [Fact]
    public void WithKexAboardKexSpeaksTheClueWhenTheNarratorIsOff() =>
        Assert.Equal(VoiceRole.ShipAi, ClueVoice(PersonaCatalog.Kex, narratorOn: false));

    [Fact]
    public void WithKexAboardTheNarratorSpeaksTheClueWhenItIsOn() =>
        Assert.Equal(VoiceRole.Narrator, ClueVoice(PersonaCatalog.Kex, narratorOn: true));

    private static VoiceRole ClueVoice(D47.Core.Persona.Persona core, bool narratorOn)
    {
        var callout = new StoryClueCallout(new NearbyFight())
        {
            Due = _ => new StoryClueDue(Id, 0),
            NarratorOn = () => narratorOn,
            Core = () => core,
        };

        var docked = GameStatus.Unknown with { Flags = StatusFlags.Docked | StatusFlags.InMainShip, ReadAt = Now };

        return Assert.Single(callout.Examine(new CalloutContext(Now, false, null, docked, NavRoute.None, [], null))).Voice;
    }
}
