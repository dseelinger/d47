using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A hunter that has found the Commander in supercruise warns about thirty seconds ahead of the interdiction.</summary>
public class AHunterThatHasFoundUsWarnsTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private static JournalEvent Comms(string message, string channel = "npc")
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["timestamp"] = "3311-01-01T00:00:00Z",
            ["event"] = "ReceiveText",
            ["From"] = "$npc_name_decorate:#name=Javier Mart;",
            ["Message"] = message,
            ["Channel"] = channel,
        });

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CalloutContext Context(IEnumerable<JournalEvent> events, int atSecond = 0) =>
        new(Start.AddSeconds(atSecond), IsPriming: false, null, GameStatus.Unknown, NavRoute.None, [.. events]);

    private static CalloutEngine Engine() =>
        new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(new AnnouncedAttackCallout());

    [Theory]
    [InlineData("$Pirate_HunterHostileSC_Relevant01;", "attack.hunter-pirate", AlertCue.Interdiction)]
    [InlineData("$Pirate_HunterHostileSC_Relevant05;", "attack.hunter-pirate", AlertCue.Interdiction)]
    [InlineData("$CargoHunter_HunterHostileSC_RelevantPlayer_003;", "attack.hunter-pirate", AlertCue.Interdiction)]
    [InlineData("$Hitman_HunterHostileSC_Relevant01;", "attack.hunter-hitman", AlertCue.BountyHunter)]
    [InlineData("$Hitman_HunterHostileSC_Relevant04;", "attack.hunter-hitman", AlertCue.BountyHunter)]
    [InlineData("$Hitman_HunterHostileSC_Relevant05;", "attack.hunter-hitman", AlertCue.BountyHunter)]
    [InlineData("$PirateHitman_HunterHostileSC_Relevant01;", "attack.hunter-hitman", AlertCue.BountyHunter)]
    [InlineData("$BountyHunter_HunterHostileSC_Relevant01;", "attack.hunter-hitman", AlertCue.BountyHunter)]
    public void AHunterThatHasFoundUsInSupercruiseWarnsUrgently(string message, string key, AlertCue cue)
    {
        var warning = AnnouncedAttackCallout.Read(Comms(message));

        Assert.NotNull(warning);
        Assert.Equal(key, warning.Key);
        Assert.Equal(cue, warning.Cue);
        Assert.Equal(CalloutUrgency.Urgent, warning.Urgency);
        Assert.Equal(VoiceRole.ShipAi, warning.Voice);
        Assert.Contains("About thirty seconds, maybe less.", warning.Text, StringComparison.Ordinal);
        Assert.Null(FlavourBriefs.For(warning, personalityEnabled: true));
    }

    [Fact]
    public void ThePirateAndTheHitmanHunterSayDifferentThings()
    {
        var pirate = AnnouncedAttackCallout.Read(Comms("$Pirate_HunterHostileSC_Relevant01;"))!;
        var hitman = AnnouncedAttackCallout.Read(Comms("$Hitman_HunterHostileSC_Relevant01;"))!;

        Assert.Contains("submit or fight", pirate.Text, StringComparison.Ordinal);
        Assert.Contains("get ready to fight", hitman.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("submit", hitman.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("$CargoHunter_HunterHostile_RelevantPlayer_004;")]
    [InlineData("$PirateLord_HunterHostileSC_Relevant01;")]
    public void ALookAlikeThatDoesNotMatchAFamilyDoesNotWarn(string message) =>
        Assert.Null(AnnouncedAttackCallout.Read(Comms(message)));

    [Fact]
    public void TheWarningIsFollowedByAnAsideFromTheSameEvent()
    {
        var said = new AnnouncedAttackCallout()
            .Examine(Context([Comms("$Pirate_HunterHostileSC_Relevant02;")])).ToList();

        Assert.Equal(2, said.Count);
        Assert.Equal("attack.hunter-pirate", said[0].Key);
        Assert.Equal(AnnouncedAttackCallout.HunterAsideKey, said[1].Key);
        Assert.Equal(CalloutUrgency.Routine, said[1].Urgency);
        Assert.Null(said[1].Cue);
        Assert.Equal(VoiceRole.ShipAi, said[1].Voice);
        Assert.Equal(TimeSpan.FromMinutes(10), said[1].Cooldown);
    }

    [Fact]
    public void EveryRepeatWarnsAndTheAsideIsOncePerHunt()
    {
        var engine = Engine();

        engine.Tick(Context([Comms("$Pirate_HunterHostileSC_Relevant01;")]));
        engine.Tick(Context([Comms("$Pirate_HunterHostileSC_Relevant02;")], atSecond: 30));
        engine.Tick(Context([Comms("$Pirate_HunterHostileSC_Relevant03;")], atSecond: 90));

        var said = engine.Drain();

        Assert.Equal(3, said.Count(a => a.Key == "attack.hunter-pirate"));
        Assert.Single(said, a => a.Key == AnnouncedAttackCallout.HunterAsideKey);
    }

    [Fact]
    public void TheAsideIsSaidAgainOnceTheCooldownHasPassed()
    {
        var engine = Engine();

        engine.Tick(Context([Comms("$Hitman_HunterHostileSC_Relevant01;")]));
        engine.Tick(Context([Comms("$Hitman_HunterHostileSC_Relevant01;")], atSecond: 700));

        Assert.Equal(2, engine.Drain().Count(a => a.Key == AnnouncedAttackCallout.HunterAsideKey));
    }

    [Fact]
    public void TheAsideLinesRotateAndAreSaidAsWrittenWithPersonalityOff()
    {
        var callout = new AnnouncedAttackCallout();

        var asides = Enumerable.Range(0, 3)
            .Select(n => callout.Examine(Context([Comms("$Hitman_HunterHostileSC_Relevant01;")], atSecond: n * 40))
                .Single(a => a.Key == AnnouncedAttackCallout.HunterAsideKey))
            .ToList();

        Assert.Equal(3, asides.Select(a => a.Text).Distinct(StringComparer.Ordinal).Count());
        Assert.All(asides, a => Assert.Null(FlavourBriefs.For(a, personalityEnabled: false)));
    }

    [Fact]
    public void TheAsideBriefForbidsANumberAnInstructionAQuestionAndAReason()
    {
        var aside = new AnnouncedAttackCallout()
            .Examine(Context([Comms("$Hitman_HunterHostileSC_Relevant01;")]))
            .Single(a => a.Key == AnnouncedAttackCallout.HunterAsideKey);

        var brief = FlavourBriefs.For(aside, personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.Contains(aside.Text, brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("number", brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("instruction", brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("question", brief.Instruction, StringComparison.Ordinal);
        Assert.Null(FlavourBriefs.HumorGroupOf(aside, brief));
    }

    [Theory]
    [InlineData("local")]
    [InlineData("wing")]
    [InlineData("player")]
    public void AnotherCommanderCannotStageAHunterWarningByTypingTheId(string channel) =>
        Assert.Empty(new AnnouncedAttackCallout().Examine(Context(
            [Comms("$Pirate_HunterHostileSC_Relevant01;", channel)])));

    [Fact]
    public void ThePrimingBacklogProducesNoHunterWarning() =>
        Assert.Empty(new AnnouncedAttackCallout().Examine(new CalloutContext(
            Start,
            IsPriming: true,
            null,
            GameStatus.Unknown,
            NavRoute.None,
            [Comms("$Pirate_HunterHostileSC_Relevant01;")])));

    [Theory]
    [InlineData("$Pirate_HunterHostileSC_Relevant01;")]
    [InlineData("$Hitman_HunterHostileSC_Relevant04;")]
    [InlineData("$BountyHunter_HunterHostileSC_Relevant01;")]
    public void AHunterLineMeansTheCommanderIsUnderThreat(string message) =>
        Assert.True(Threat.IsUnder(Context([Comms(message)])));
}
