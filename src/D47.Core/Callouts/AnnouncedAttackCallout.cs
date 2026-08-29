using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// An attack that has been announced but has not yet landed (Phase 15).
/// <para>
/// NPCs say what they are about to do before they do it, and Elite writes that as a localisation
/// id — <c>$Pirate_StartInterdiction07;</c> — with the prose in a separate field. So this is an
/// <b>allowlist of ids</b> and never a keyword match on text, and it buys a median of six to eight
/// seconds: enough to boost, deploy hardpoints or high-wake.
/// </para>
/// <para>
/// <b>The allowlist is what makes it work rather than what makes it tidy.</b> Measured over 912
/// journals, <c>$Trader_OnEnemyShipDetection*</c> fires 2,399 times and precedes an attack 1.3% of
/// the time, and <c>$HostileScan*</c> 48 times at 0%. Anything matching on "this sounds hostile"
/// cries wolf a hundred times per real event, which is a warning a Commander switches off within
/// the hour and then does not have. See docs/spikes/journal-corpus-warnings.md.
/// </para>
/// <para>
/// <b>This is the trust boundary.</b> In-game comms are untrusted (architecture.md §7) and the
/// attacker is any player in range. Two things follow, and both are load-bearing. The comparison is
/// against <c>Message</c>, which is an id from a fixed set, rather than against
/// <c>Message_Localised</c>, which is prose; and the spoken line is a constant chosen by the group
/// rather than anything assembled from the event, so no text from the message reaches the
/// synthesiser, the panel or the model.
/// </para>
/// </summary>
public sealed class AnnouncedAttackCallout : ICallout
{
    public string Id => "announced-attack";

    /// <summary>
    /// Shorter than <see cref="DangerCallout"/>'s thirty seconds, and per group. A burst of pirate
    /// chatter is one warning; a second aggressor twenty seconds later is a second one, and this
    /// is the callout whose whole value is arriving in time to do something.
    /// </summary>
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    /// <summary>
    /// One measured group: the id prefix, what to say, and what to sound like.
    /// </summary>
    /// <param name="Prefix">
    /// Compared after the leading <c>$</c> and as a whole prefix, so <c>Pirate_</c> does not match
    /// <c>PirateLord_</c>. That is not pedantry — <c>$PirateLord_OnDeclarePiracyAttack*</c> was
    /// measured separately at 43% against this group's 67%, on a tenth of the evidence, and is
    /// deliberately not here.
    /// </param>
    private readonly record struct Group(string Prefix, string Key, string Text, AlertCue Cue);

    /// <summary>
    /// The whole allowlist. Three groups, each with its own line and its own cue, because the game
    /// has already told us which situation it is and one generic warning would throw that away.
    /// </summary>
    private static readonly Group[] Allowed =
    [
        // 88% followed by an attack, median six seconds. The strongest signal measured.
        new(
            "Pirate_StartInterdiction",
            "attack.interdiction",
            "Pirate lining up an interdiction. Boost or high-wake now.",
            AlertCue.Interdiction),

        // 67%, and by far the commonest of the three at 398 events. Weaker, and said differently
        // so the Commander can weigh it differently.
        new(
            "Pirate_OnDeclarePiracyAttack",
            "attack.piracy",
            "Pirate demanding cargo. They will open fire.",
            AlertCue.Piracy),

        // One event in the corpus, and it was followed by an attack. Shipped on one sample because
        // it is the same shape as the pirate line that scores 88% and because a bounty hunter
        // wants the Commander rather than the hold — the answer is different, so the line is.
        new(
            "BountyHunter_StartInterdiction",
            "attack.bounty-hunter",
            "Bounty hunter interdicting. This one is not after the cargo.",
            AlertCue.BountyHunter),

        // <b>7 of 7 — the strongest signal in the corpus</b>, above the 88% the pirate
        // interdiction line was called the strongest measured on
        // (<a href="https://github.com/dseelinger/d47/issues/137">#137</a>). Measured by the same
        // code in the same run as the three above, which reproduced their shipped figures exactly
        // (88%, 66%, 100%) against a rejected control at 1% — so this row is comparable rather
        // than merely plausible.
        new(
            "HitmanMissionFailure_OnEnemyDetect",
            "attack.hitman",
            "Someone has been paid to find us, and they have. This one is not after the cargo.",
            AlertCue.BountyHunter),

        // <b>2 of 3, and shipped on exactly the terms the bounty hunter's single event was.</b>
        // Thin evidence, right shape, and the same answer as the row above — so it is taken
        // deliberately and its n is written down rather than left to be inferred from a
        // percentage. If it turns out to cry wolf, the number to reconsider is this one.
        new(
            "HitmanMissionFailure_NearDeath",
            "attack.hitman",
            "Someone has been paid to find us, and they have. This one is not after the cargo.",
            AlertCue.BountyHunter),
    ];

    /// <summary>
    /// The families where a hunter is talking about the Commander and an <em>alarm</em> would be
    /// wrong (<a href="https://github.com/dseelinger/d47/issues/137">#137</a>).
    /// <para>
    /// <b>Measured, and they do not qualify.</b> <c>HitmanMissionFailure_Attack</c> is followed by
    /// an attack 7 times in 20 (<b>35%</b>) and <c>Hitman_HunterHostileSC_Relevant</c> — the
    /// <i>"the eagle is in the nest"</i> line the Commander actually noticed — 7 times in 47
    /// (<b>15%</b>). Both are well under the 66% of the weakest line that ships, and cueing them
    /// would be precisely the crying-wolf the allowlist exists to prevent: <i>"anything matching on
    /// 'this sounds hostile' cries wolf a hundred times per real event"</i>.
    /// </para>
    /// <para>
    /// <b>But 15% of 47 is still a hitman talking about you</b>, and that is the half of the report
    /// that is not about alarms. Being hunted is a <em>situation</em>. A warning would be wrong and
    /// saying nothing was the gap, so d47 reacts to it instead — in its own voice, off the cue
    /// channel, at <see cref="CalloutUrgency.Routine"/>, on a long cooldown.
    /// </para>
    /// </summary>
    private static readonly string[] Hunted =
    [
        "Hitman_HunterHostileSC_Relevant",
        "HitmanMissionFailure_Attack",
    ];

    /// <summary>
    /// The one key every hunted reaction shares, so a burst of chatter is one remark
    /// (<a href="https://github.com/dseelinger/d47/issues/137">#137</a>).
    /// <para>
    /// Public because <see cref="FlavourBriefs"/> matches on it: this is the one line here that is
    /// said in character rather than exactly as written.
    /// </para>
    /// </summary>
    public const string HuntedKey = "hunted";

    /// <summary>
    /// Long, because being hunted is a condition rather than an event. The 47 corpus events arrive
    /// in bursts — the reported session had three across half an hour — and a companion that
    /// remarks on each one is noise wearing a personality.
    /// </summary>
    private static readonly TimeSpan HuntedCooldown = TimeSpan.FromMinutes(10);

    /// <summary>
    /// What d47 says about being hunted. <b>Its own words, chosen by the id family and never
    /// assembled from the message</b> — the same constant-line rule the warnings above follow, for
    /// the same reason.
    /// <para>
    /// <b>None of them says why.</b> <c>HitmanMissionFailure_*</c> reads as though it should join to
    /// a mission the Commander failed, and it does not: of 30 such lines in the corpus, <b>one</b>
    /// was preceded by a <c>MissionFailed</c> or <c>MissionAbandoned</c> within the hour. So the
    /// reaction is about the situation now, and inventing a story the journal does not carry is the
    /// one thing it must not do.
    /// </para>
    /// </summary>
    private static readonly string[] HuntedLines =
    [
        "Someone out there is hunting us, and they are talking about it.",
        "That transmission was about us. We are being looked for.",
        "We have somebody's attention, and they are not being subtle about it.",
    ];

    private int _reactions;

    /// <summary>
    /// The channel NPC lines arrive on. Required rather than assumed: all 441 allowlisted events
    /// in the corpus were on it, so insisting costs nothing — and without it another Commander can
    /// type <c>$Pirate_StartInterdiction01;</c> into local chat and raise the warning by hand,
    /// which is precisely the attack the id comparison exists to prevent.
    /// </summary>
    private const string NpcChannel = "npc";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        // Never from the backlog. There is no state to fold here — every one of these is an event
        // — so priming is simply nothing to say, and starting d47 after an hour of flying must not
        // warn about an interdiction that resolved forty minutes ago.
        if (context.IsPriming)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind is not "ReceiveText")
            {
                continue;
            }

            // A warning first, then a reaction. The two tables share no prefix, so an event
            // reaches at most one of them and no id can produce both an alarm and a remark.
            if (Read(journalEvent) is { } warning)
            {
                yield return warning;
            }
            else if (Reacted(journalEvent) is { } remark)
            {
                yield return remark;
            }
        }
    }

    /// <summary>
    /// A hunter has been heard talking about the Commander, and d47 says so in its own voice
    /// (<a href="https://github.com/dseelinger/d47/issues/137">#137</a>).
    /// <para>
    /// <b>Keyed on the id family and never on the prose, which is the trust boundary rather than a
    /// preference.</b> The comparison is against <c>Message</c> — a token from a closed set — and
    /// what comes back is a constant from <see cref="HuntedLines"/>. So no text from the message
    /// reaches the synthesiser, the panel or the model, exactly as for the warnings above. Reading
    /// a message aloud is quoting and <see cref="IncomingMessages"/> already does it; handing that
    /// text to a model is a different act and is the one the rule forbids.
    /// </para>
    /// <para>
    /// The npc channel is required for the same reason it is required above: without it, another
    /// Commander can type <c>$Hitman_HunterHostileSC_Relevant04;</c> into local chat and make d47
    /// react to a threat nobody made.
    /// </para>
    /// <para>
    /// <b>Routine, and it carries no cue.</b> Menace is not an alarm — 15% of these are followed by
    /// anything at all, so interrupting for one would be the crying wolf the measurement rejected.
    /// One shared key and a ten-minute cooldown make a burst into one remark.
    /// </para>
    /// </summary>
    private Announcement? Reacted(JournalEvent journalEvent)
    {
        if (!string.Equals(journalEvent.String("Channel"), NpcChannel, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (journalEvent.String("Message") is not { Length: > 1 } message || message[0] is not '$')
        {
            return null;
        }

        var id = message.AsSpan(1);
        var hunted = false;

        foreach (var prefix in Hunted)
        {
            if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                hunted = true;
                break;
            }
        }

        if (!hunted)
        {
            return null;
        }

        // The index the stock line was picked with, carried the way the ambient remarks carry
        // theirs — it is the only deterministic choice a flavour call has, and no Core component
        // reads a clock or a seed.
        var variant = _reactions++;

        return new Announcement(HuntedKey, HuntedLines[variant % HuntedLines.Length], CalloutUrgency.Routine)
        {
            Cooldown = HuntedCooldown,
            Variant = variant,
        };
    }

    /// <summary>
    /// One message, or null if it announces nothing worth warning about. Separated from the loop so
    /// the whole of the decision — including every named false positive — is testable against a
    /// single event.
    /// </summary>
    public static Announcement? Read(JournalEvent journalEvent)
    {
        if (!string.Equals(journalEvent.String("Channel"), NpcChannel, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // The id, never the localised prose. Message_Localised is a sentence somebody wrote and
        // Message is a token from a closed set, and only the second can be compared safely.
        if (journalEvent.String("Message") is not { Length: > 1 } message || message[0] is not '$')
        {
            return null;
        }

        var id = message.AsSpan(1);

        foreach (var group in Allowed)
        {
            if (!id.StartsWith(group.Prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return new Announcement(group.Key, group.Text, CalloutUrgency.Urgent)
            {
                Cue = group.Cue,
                Cooldown = Cooldown,
            };
        }

        return null;
    }
}
