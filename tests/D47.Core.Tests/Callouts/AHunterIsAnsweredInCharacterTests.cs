using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// A hitman hunting the Commander, reported as three threats read out flat in a stranger's voice
/// while one was on their tail.
/// </summary>
public class AHunterIsAnsweredInCharacterTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private static JournalEvent Comms(string message, string channel = "npc", string? localised = null)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["timestamp"] = "3311-01-01T00:00:00Z",
            ["event"] = "ReceiveText",
            ["From"] = "$npc_name_decorate:#name=Javier Mart;",
            ["Message"] = message,
            ["Message_Localised"] = localised,
            ["Channel"] = channel,
        });

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CalloutContext Context(IEnumerable<JournalEvent> events, int atSecond = 0) =>
        new(Start.AddSeconds(atSecond), IsPriming: false, null, GameStatus.Unknown, NavRoute.None, [.. events]);

    private static IReadOnlyList<Announcement> Heard(AnnouncedAttackCallout callout, string message, int atSecond = 0) =>
        [.. callout.Examine(Context([Comms(message)], atSecond))];

    // ---- The two that warn ---------------------------------------------------------------

    /// <summary>The strongest signal in the corpus, and one taken on thin evidence deliberately.</summary>
    [Theory]
    [InlineData("$HitmanMissionFailure_OnEnemyDetect01;")]
    [InlineData("$HitmanMissionFailure_NearDeath02;")]
    public void AHunterThatMeasuresAsAnAttackWarnsWithACue(string message)
    {
        var warning = AnnouncedAttackCallout.Read(Comms(message));

        Assert.NotNull(warning);
        Assert.Equal(CalloutUrgency.Urgent, warning.Urgency);
        Assert.Equal(AlertCue.BountyHunter, warning.Cue);
        Assert.Contains("not after the cargo", warning.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The two share a key, which is the opposite of the rule the three original groups follow and is
    /// right for the same reason those differ: a key is a cooldown, and these two are one situation
    /// reported twice rather than two situations.
    /// </summary>
    [Fact]
    public void TheTwoHitmanWarningsAreOneSituationAndShareACooldown()
    {
        var detect = AnnouncedAttackCallout.Read(Comms("$HitmanMissionFailure_OnEnemyDetect01;"));
        var nearDeath = AnnouncedAttackCallout.Read(Comms("$HitmanMissionFailure_NearDeath02;"));

        Assert.Equal(detect!.Key, nearDeath!.Key);
    }

    // ---- The two that must not ------------------------------------------------------------

    /// <summary>35% and 15%, both under the 66% of the weakest line that ships.</summary>
    [Theory]
    [InlineData("$Hitman_HunterHostileSC_Relevant04;")]
    [InlineData("$Hitman_HunterHostileSC_Relevant05;")]
    [InlineData("$HitmanMissionFailure_Attack03;")]
    public void TheOnesTheCommanderNoticedAreNotAttackWarnings(string message) =>
        Assert.Null(AnnouncedAttackCallout.Read(Comms(message)));

    /// <summary>They produce a reaction instead, and it is not an alarm.</summary>
    [Theory]
    [InlineData("$Hitman_HunterHostileSC_Relevant04;")]
    [InlineData("$HitmanMissionFailure_Attack03;")]
    public void BeingHuntedProducesAnInCharacterLineRatherThanAWarning(string message)
    {
        var said = Assert.Single(Heard(new AnnouncedAttackCallout(), message));

        Assert.Equal(AnnouncedAttackCallout.HuntedKey, said.Key);
        Assert.Equal(CalloutUrgency.Routine, said.Urgency);
        Assert.Null(said.Cue);
        Assert.Equal(VoiceRole.ShipAi, said.Voice);
        Assert.True(said.Cooldown > TimeSpan.Zero);
    }

    // ---- The trust boundary ---------------------------------------------------------------

    /// <summary>Keyed on the id family, never on the prose, and nothing from the message comes back.</summary>
    [Fact]
    public void NoTextFromTheMessageReachesTheLine()
    {
        var said = Assert.Single(new AnnouncedAttackCallout().Examine(Context(
        [
            Comms(
                "$Hitman_HunterHostileSC_Relevant04;",
                localised: "Finally! Found you. Come to me lil' fishy."),
        ])));

        Assert.DoesNotContain("lil' fishy", said.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Found you", said.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Javier", said.Text, StringComparison.Ordinal);

        // And nothing carries it onward either: no speaker, no transcript line, no comms channel.
        Assert.Null(said.Speaker);
        Assert.Null(said.Transcript);
        Assert.Null(said.CommsChannel);
    }

    /// <summary>And the brief the model is handed carries d47's line rather than the hitman's.</summary>
    [Fact]
    public void TheModelIsHandedD47sOwnWordsAndNotTheHunters()
    {
        var said = Assert.Single(new AnnouncedAttackCallout().Examine(Context(
        [
            Comms(
                "$Hitman_HunterHostileSC_Relevant04;",
                localised: "The eagle is in the nest, repeat, the eagle is in the nest."),
        ])));

        var brief = FlavourBriefs.For(said, personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.Contains(said.Text, brief.Instruction, StringComparison.Ordinal);
        Assert.DoesNotContain("eagle", brief.Instruction, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>It never states why.</summary>
    [Fact]
    public void TheReactionIsForbiddenFromInventingAReason()
    {
        var said = Assert.Single(Heard(new AnnouncedAttackCallout(), "$Hitman_HunterHostileSC_Relevant04;"));
        var brief = FlavourBriefs.For(said, personalityEnabled: true)!;

        Assert.Contains("why", brief.Instruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("who sent them", brief.Instruction, StringComparison.Ordinal);

        // And none of the authored lines says it either, since one of them is what a Commander with
        // personality switched off actually hears.
        Assert.DoesNotContain("mission", said.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("because", said.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bounty", said.Text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Personality off means the authored line is said exactly as written, which is the rule every
    /// announcement follows.
    /// </summary>
    [Fact]
    public void WithPersonalityOffTheAuthoredReactionIsSaidAsWritten()
    {
        var said = Assert.Single(Heard(new AnnouncedAttackCallout(), "$Hitman_HunterHostileSC_Relevant04;"));

        Assert.Null(FlavourBriefs.For(said, personalityEnabled: false));
    }

    /// <summary>
    /// A hostile player cannot manufacture the reaction by typing the id into local chat, which is the
    /// same channel requirement the warnings carry and for the same reason.
    /// </summary>
    [Theory]
    [InlineData("local")]
    [InlineData("wing")]
    [InlineData("player")]
    public void AnotherCommanderCannotStageAHuntByTypingTheId(string channel) =>
        Assert.Empty(new AnnouncedAttackCallout().Examine(Context(
            [Comms("$Hitman_HunterHostileSC_Relevant04;", channel)])));

    // ---- One reaction, not seven -----------------------------------------------------------

    /// <summary>A burst produces one remark.</summary>
    [Fact]
    public void ABurstOfHunterChatterIsOneReactionAndNotSeven()
    {
        var callout = new AnnouncedAttackCallout();
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(callout);

        for (var second = 0; second < 7; second++)
        {
            engine.Tick(Context([Comms("$Hitman_HunterHostileSC_Relevant04;")], atSecond: second * 20));
        }

        Assert.Single(engine.Drain());
    }

    /// <summary>
    /// And it is said again once the situation has had time to be worth remarking on afresh, rather
    /// than once per session.
    /// </summary>
    [Fact]
    public void ItIsSaidAgainMuchLater()
    {
        var callout = new AnnouncedAttackCallout();
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(callout);

        engine.Tick(Context([Comms("$Hitman_HunterHostileSC_Relevant04;")]));
        engine.Tick(Context([Comms("$HitmanMissionFailure_Attack03;")], atSecond: 1_800));

        Assert.Equal(2, engine.Drain().Count);
    }

    /// <summary>
    /// The stock lines rotate, so a Commander with personality switched off does not hear the same
    /// sentence every time.
    /// </summary>
    [Fact]
    public void TheAuthoredLinesRotateRatherThanRepeating()
    {
        var callout = new AnnouncedAttackCallout();

        var said = Enumerable.Range(0, 3)
            .Select(n => Assert.Single(Heard(callout, "$Hitman_HunterHostileSC_Relevant04;", atSecond: n)))
            .ToList();

        Assert.Equal(3, said.Select(line => line.Text).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal([0, 1, 2], said.Select(line => line.Variant));
    }

    /// <summary>
    /// The backlog is not reacted to, for the reason it is not warned about: a hitman who was looking
    /// for the Commander forty minutes ago is not news they can act on.
    /// </summary>
    [Fact]
    public void ThePrimingBacklogProducesNoReaction()
    {
        Assert.Empty(new AnnouncedAttackCallout().Examine(new CalloutContext(
            Start,
            IsPriming: true,
            null,
            GameStatus.Unknown,
            NavRoute.None,
            [Comms("$Hitman_HunterHostileSC_Relevant04;")])));
    }

    /// <summary>Nothing outside the two hunted families reacts.</summary>
    [Theory]
    [InlineData("$Trader_OnEnemyShipDetection02;")]
    [InlineData("$HostileScan01;")]
    [InlineData("$STATION_NoFireZone_entered;")]
    public void OrdinaryChatterProducesNeitherAWarningNorAReaction(string message) =>
        Assert.Empty(new AnnouncedAttackCallout().Examine(Context([Comms(message)])));
}
