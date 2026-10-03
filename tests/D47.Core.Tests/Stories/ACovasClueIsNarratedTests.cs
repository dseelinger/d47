using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A stock core never speaks a story line: the Narrator speaks the ship's lines while it is aboard. Any other core speaks them.</summary>
public sealed class ACovasClueIsNarratedTests
{
    [Fact]
    public void WithCovasAboardTheNarratorSpeaksTheShipsClue() =>
        Assert.Equal(VoiceRole.Narrator, ClueVoice(PersonaCatalog.Covas));

    [Fact]
    public void WithKexAboardKexSpeaksTheShipsClue() =>
        Assert.Equal(VoiceRole.ShipAi, ClueVoice(PersonaCatalog.Kex));

    private static VoiceRole ClueVoice(D47.Core.Persona.Persona core)
    {
        var callout = new StoryClueCallout(new NearbyFight())
        {
            Due = _ => new StoryClueDue(Id, 0),
            VoiceOf = _ => StoryVoices.Of(Secret.Clues[0].Speaker, Secret, gender: null, core),
        };

        var docked = GameStatus.Unknown with { Flags = StatusFlags.Docked | StatusFlags.InMainShip, ReadAt = Now };

        return Assert.Single(callout.Examine(new CalloutContext(Now, false, null, docked, NavRoute.None, [], null))).Voice;
    }
}
