using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>An attack that has been announced but has not yet landed (Phase 15).</summary>
public sealed class AnnouncedAttackCallout : ICallout
{
    public string Id => "announced-attack";

    /// <summary>Shorter than <see cref="DangerCallout"/>'s thirty seconds, and per group.</summary>
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    /// <summary>One measured group: the id prefix, what to say, and what to sound like.</summary>
    /// <param name="Prefix">
    /// Compared after the leading <c>$</c> and as a whole prefix, so <c>Pirate_</c> does not match
    /// <c>PirateLord_</c>.
    /// </param>
    private readonly record struct Group(string Prefix, string Key, string Text, AlertCue Cue);

    /// <summary>The whole allowlist.</summary>
    private static readonly Group[] Allowed =
    [
        // 88% followed by an attack, median six seconds.
        new(
            "Pirate_StartInterdiction",
            "attack.interdiction",
            "Pirate lining up an interdiction. Boost or high-wake now.",
            AlertCue.Interdiction),

        // 67%, and by far the commonest of the three at 398 events.
        new(
            "Pirate_OnDeclarePiracyAttack",
            "attack.piracy",
            "Pirate demanding cargo. They will open fire.",
            AlertCue.Piracy),

        // One event in the corpus, and it was followed by an attack.
        new(
            "BountyHunter_StartInterdiction",
            "attack.bounty-hunter",
            "Bounty hunter interdicting. This one is not after the cargo.",
            AlertCue.BountyHunter),

        // 7 of 7 — the strongest signal in the corpus, above the 88% the pirate interdiction line was
        // called the strongest measured on (#137).
        new(
            "HitmanMissionFailure_OnEnemyDetect",
            "attack.hitman",
            "Someone has been paid to find us, and they have. This one is not after the cargo.",
            AlertCue.BountyHunter),

        // 2 of 3, and shipped on exactly the terms the bounty hunter's single event was. Thin
        // evidence, right shape, and the same answer as the row above — so it is taken deliberately and its n
        // is written down rather than left to be inferred from a percentage.
        new(
            "HitmanMissionFailure_NearDeath",
            "attack.hitman",
            "Someone has been paid to find us, and they have. This one is not after the cargo.",
            AlertCue.BountyHunter),
    ];

    /// <summary>
    /// The families where a hunter is talking about the Commander and an alarm would be wrong (#137).
    /// </summary>
    private static readonly string[] Hunted =
    [
        "Hitman_HunterHostileSC_Relevant",
        "HitmanMissionFailure_Attack",
    ];

    /// <summary>The one key every hunted reaction shares, so a burst of chatter is one remark (#137).</summary>
    public const string HuntedKey = "hunted";

    /// <summary>Long, because being hunted is a condition rather than an event.</summary>
    private static readonly TimeSpan HuntedCooldown = TimeSpan.FromMinutes(10);

    /// <summary>What d47 says about being hunted.</summary>
    private static readonly string[] HuntedLines =
    [
        "Someone out there is hunting us, and they are talking about it.",
        "That transmission was about us. We are being looked for.",
        "We have somebody's attention, and they are not being subtle about it.",
    ];

    private int _reactions;

    /// <summary>The channel NPC lines arrive on.</summary>
    private const string NpcChannel = "npc";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        // Never from the backlog.
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

            // A warning first, then a reaction.
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
    /// A hunter has been heard talking about the Commander, and d47 says so in its own voice (#137).
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

        // The index the stock line was picked with, carried the way the ambient remarks carry theirs — it is
        // the only deterministic choice a flavour call has, and no Core component reads a clock or a seed.
        var variant = _reactions++;

        return new Announcement(HuntedKey, HuntedLines[variant % HuntedLines.Length], CalloutUrgency.Routine)
        {
            Cooldown = HuntedCooldown,
            Variant = variant,
        };
    }

    /// <summary>One message, or null if it announces nothing worth warning about.</summary>
    public static Announcement? Read(JournalEvent journalEvent)
    {
        if (!string.Equals(journalEvent.String("Channel"), NpcChannel, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // The id, never the localised prose.
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
