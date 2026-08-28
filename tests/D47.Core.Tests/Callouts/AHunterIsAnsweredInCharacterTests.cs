using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// A hitman hunting the Commander (<a href="https://github.com/dseelinger/d47/issues/137">#137</a>),
/// reported as three threats read out flat in a stranger's voice while one was on their tail.
/// <para>
/// <b>The measurement split the request in two.</b> Run over the Commander's 935 journals with the
/// three shipped lines as controls — which it reproduced exactly, at 88%, 66% and 100% against a
/// rejected control at 1% — the four Hitman families come out as:
/// </para>
/// <list type="table">
/// <item><c>HitmanMissionFailure_OnEnemyDetect</c> — 7 of 7, <b>100%</b>: warns.</item>
/// <item><c>HitmanMissionFailure_NearDeath</c> — 2 of 3, <b>67%</b>: warns, on the same thin-evidence
/// terms the bounty hunter's single event shipped on.</item>
/// <item><c>HitmanMissionFailure_Attack</c> — 7 of 20, <b>35%</b>: does not warn.</item>
/// <item><c>Hitman_HunterHostileSC_Relevant</c> — 7 of 47, <b>15%</b>: does not warn. This is the
/// <i>"the eagle is in the nest"</i> line the Commander actually noticed.</item>
/// </list>
/// <para>
/// <b>But 15% of 47 is still a hitman talking about you.</b> A warning would be wrong and saying
/// nothing was the gap, so the two that do not qualify get a reaction instead — d47's own words, off
/// the cue channel, on a long cooldown.
/// </para>
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

    /// <summary>
    /// <b>The strongest signal in the corpus, and one taken on thin evidence deliberately.</b> Both
    /// share a cue with the bounty hunter, because the rule for a distinct cue is that the response
    /// differs and here it does not: both are here for the Commander and neither can be bought off
    /// with the hold.
    /// </summary>
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
    /// The two share a key, which is the opposite of the rule the three original groups follow and
    /// is right for the same reason those differ: a key is a cooldown, and these two are one
    /// situation reported twice rather than two situations.
    /// </summary>
    [Fact]
    public void TheTwoHitmanWarningsAreOneSituationAndShareACooldown()
    {
        var detect = AnnouncedAttackCallout.Read(Comms("$HitmanMissionFailure_OnEnemyDetect01;"));
        var nearDeath = AnnouncedAttackCallout.Read(Comms("$HitmanMissionFailure_NearDeath02;"));

        Assert.Equal(detect!.Key, nearDeath!.Key);
    }

    // ---- The two that must not ------------------------------------------------------------

    /// <summary>
    /// <b>35% and 15%, both under the 66% of the weakest line that ships.</b> Cueing these would be
    /// exactly the crying-wolf the allowlist exists to prevent — <i>"anything matching on 'this
    /// sounds hostile' cries wolf a hundred times per real event"</i>. The ids are the ones from the
    /// reported session.
    /// </summary>
    [Theory]
    [InlineData("$Hitman_HunterHostileSC_Relevant04;")]
    [InlineData("$Hitman_HunterHostileSC_Relevant05;")]
    [InlineData("$HitmanMissionFailure_Attack03;")]
    public void TheOnesTheCommanderNoticedAreNotAttackWarnings(string message) =>
        Assert.Null(AnnouncedAttackCallout.Read(Comms(message)));

    /// <summary>
    /// <b>They produce a reaction instead, and it is not an alarm.</b> Routine, so it never
    /// interrupts; no cue, because menace is not an emergency; and the ship's AI's own voice.
    /// </summary>
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

    /// <summary>
    /// <b>Keyed on the id family, never on the prose, and nothing from the message comes back.</b>
    /// This is the boundary <c>AnnouncedAttackCallout</c> states of itself: in-game comms are
    /// untrusted and the attacker is any player in range, so the comparison is against
    /// <c>Message</c> — a token from a closed set — and what is said is a constant chosen by the
    /// family.
    /// </summary>
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

    /// <summary>
    /// <b>And the brief the model is handed carries d47's line rather than the hitman's.</b> The
    /// reaction is said in character, which means a prompt — so this is the assertion that the
    /// prompt cannot be a route for text a stranger wrote.
    /// </summary>
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

    /// <summary>
    /// <b>It never states why.</b> The family reads as though it joins to a failed mission and it
    /// does not: of 30 such lines in the corpus, one was preceded by a <c>MissionFailed</c> or
    /// <c>MissionAbandoned</c> within the hour. A model given a hunter and no reason will supply
    /// one, so the instruction forbids it rather than hoping.
    /// </summary>
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
    /// A hostile player cannot manufacture the reaction by typing the id into local chat, which is
    /// the same channel requirement the warnings carry and for the same reason.
    /// </summary>
    [Theory]
    [InlineData("local")]
    [InlineData("wing")]
    [InlineData("player")]
    public void AnotherCommanderCannotStageAHuntByTypingTheId(string channel) =>
        Assert.Empty(new AnnouncedAttackCallout().Examine(Context(
            [Comms("$Hitman_HunterHostileSC_Relevant04;", channel)])));

    // ---- One reaction, not seven -----------------------------------------------------------

    /// <summary>
    /// <b>A burst produces one remark.</b> The 47 corpus events arrive in bursts — the reported
    /// session had three across half an hour — and a companion that comments on each one is noise
    /// wearing a personality. One shared key plus a ten-minute cooldown is what makes that true, so
    /// it is asserted through the engine that applies the cooldown rather than off the callout.
    /// </summary>
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
    /// sentence every time. The index rides on the announcement the way the ambient remarks' does —
    /// no Core component reads a clock or a seed, and a recorded session replays to the same call.
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
    /// The backlog is not reacted to, for the reason it is not warned about: a hitman who was
    /// looking for the Commander forty minutes ago is not news they can act on.
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

    /// <summary>
    /// Nothing outside the two hunted families reacts. A pirate is a warning and a trader is
    /// neither, and neither becomes a remark by being hostile-sounding.
    /// </summary>
    [Theory]
    [InlineData("$Trader_OnEnemyShipDetection02;")]
    [InlineData("$HostileScan01;")]
    [InlineData("$STATION_NoFireZone_entered;")]
    public void OrdinaryChatterProducesNeitherAWarningNorAReaction(string message) =>
        Assert.Empty(new AnnouncedAttackCallout().Examine(Context([Comms(message)])));
}
