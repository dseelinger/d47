using D47.Core.Audio;
using D47.Core.Callouts;
using Xunit;

namespace D47.Core.Tests.Callouts;

public sealed class AReworderedNpcLineKeepsItsLanguageAndListenerTests
{
    [Fact]
    public void TheAccentRulesRequireEnglish() =>
        Assert.Contains("Write in English. One word from the accent's own language is allowed; a sentence is not.", SpeakerAccent.Rules);

    [Fact]
    public void ACannedMilitaryBriefNamesTheListenerAbbreviationsAndAdditions()
    {
        var brief = FlavourBriefs.For(
            new Announcement(IncomingMessages.CannedKeyPrefix + "Military", "ADCS ready. No hostiles sighted.") { Voice = VoiceRole.Comms },
            personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.Contains("Address only the listener the line addresses; if it names no listener, name none.", brief.Instruction);
        Assert.Contains("Leave abbreviations as they are.", brief.Instruction);
        Assert.Contains("Add no threat, order or warning the line does not carry.", brief.Instruction);
    }
}
