using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A brief opts in to the scenario and the audience decides who hears it (#589).</summary>
public class TheScenarioReachesOnlyTheLinesThatComposeTests
{
    private const string Scenario = "We are hauling relief supplies to a besieged outpost.";

    [Fact]
    public void ABriefForSomebodyElsesWordsNeverCarriesIt()
    {
        var announcements = new[]
        {
            new Announcement($"{IncomingMessages.CannedKeyPrefix}Pirate_Threat", "Hand it over.") { CommsChannel = "npc", Voice = VoiceRole.Comms },
            new Announcement(IncomingMessages.CarrierCannedKey, "Welcome aboard.") { CommsChannel = "npc", Voice = VoiceRole.CarrierCaptain },
            new Announcement(IncomingMessages.AuthorityCannedKey, "Scan complete.") { CommsChannel = "npc", Voice = VoiceRole.Comms },
            new Announcement("npc.line", "Nice ship.") { CommsChannel = "npc", Voice = VoiceRole.Comms },
            new Announcement("player.line", "o7") { Transcript = "o7", Voice = VoiceRole.Comms },
            new Announcement("other.key", "Say this.") { Voice = VoiceRole.CarrierCaptain },
        };

        foreach (var announcement in announcements)
        {
            var brief = FlavourBriefs.For(announcement, personalityEnabled: true);

            if (brief is not null)
            {
                Assert.False(brief.NeedsScenario, announcement.Key);
            }
        }
    }

    [Fact]
    public void ACarrierHomeLineCarriesAnAboardScenarioAsNothingAndACarrierScenarioInFull()
    {
        var home = new Announcement(CarrierCallout.HomeKey, "Welcome home.") { Voice = VoiceRole.CarrierCaptain };
        var brief = FlavourBriefs.For(home, personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.Null(FlavourBriefs.ScenarioFor(brief, ScenarioAudience.Aboard, home.Voice, Scenario));
        Assert.Equal(Scenario, FlavourBriefs.ScenarioFor(brief, ScenarioAudience.Carrier, home.Voice, Scenario));
    }

    [Fact]
    public void AShipsAiAmbientLineCarriesAnAboardScenario()
    {
        var ambient = new Announcement($"{AmbientCallout.KeyPrefix}docked", "Quiet out there.");
        var brief = FlavourBriefs.For(ambient, personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.Equal(Scenario, FlavourBriefs.ScenarioFor(brief, ScenarioAudience.Aboard, ambient.Voice, Scenario));
    }

    [Fact]
    public async Task ALineThatClaimsCargoOverAnEmptyHoldIsStillDroppedWithAScenarioSet()
    {
        var ambient = new Announcement($"{AmbientCallout.KeyPrefix}docked", "Quiet out there.");
        var brief = FlavourBriefs.For(ambient, personalityEnabled: true)!;
        Assert.NotNull(FlavourBriefs.ScenarioFor(brief, ScenarioAudience.Aboard, ambient.Voice, Scenario));

        var facts = new ShipFacts { HoldKnown = true, HoldTonnes = 0 };
        var said = await ContradictedClaims.SayableAsync(
            "The hold is full of relief supplies.",
            facts,
            _ => Task.FromResult<string?>("Forty tonnes of relief supplies, all aboard."),
            NullLogger.Instance,
            "test");

        Assert.Null(said);
    }
}
