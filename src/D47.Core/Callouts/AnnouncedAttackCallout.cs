using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// An attack that has been announced but has not yet landed (list.md Phase 15).
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
    ];

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

            if (Read(journalEvent) is { } warning)
            {
                yield return warning;
            }
        }
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
