namespace D47.Core.Journal;

/// <summary>
/// What has happened since the Commander entered the game (Phase 7, "Session
/// summary"), accumulated from the journal already being tailed rather than from anything new.
/// <para>
/// "Session" means since the last LoadGame, not since the journal file began. Elite rolls the
/// journal into a continuation file during a long session without re-emitting LoadGame, so
/// resetting on the file would silently restart the totals mid-flight — and it is the one
/// event that unambiguously marks the Commander entering the game.
/// </para>
/// <para>
/// Every total here is a sum of figures Elite reported. Nothing is derived from a price table
/// or a market lookup: the journal states what each sale actually earned, which is the number
/// the Commander would recognise.
/// </para>
/// </summary>
public sealed record SessionSummary
{
    public static readonly SessionSummary Empty = new();

    /// <summary>When the session started, from the LoadGame event. Null before one is seen.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>The last event timestamp folded in — the far end of the window these totals cover.</summary>
    public DateTimeOffset? LastEventAt { get; init; }

    public long BountyEarnings { get; init; }

    public long CombatBondEarnings { get; init; }

    public long TradeEarnings { get; init; }

    public long ExplorationEarnings { get; init; }

    public long MissionEarnings { get; init; }

    /// <summary>Vouchers cashed in. Counted apart from the bounty that created them.</summary>
    public long VoucherEarnings { get; init; }

    public int Jumps { get; init; }

    /// <summary>Light years covered in hyperspace, summed from each jump's reported distance.</summary>
    public double DistanceTravelled { get; init; }

    public int MaterialsGained { get; init; }

    /// <summary>Bodies scanned with the detailed surface scanner, and first discoveries among them.</summary>
    public int BodiesScanned { get; init; }

    public int Deaths { get; init; }

    public int Interdictions { get; init; }

    /// <summary>
    /// The credit balance Elite last reported, from LoadGame. Kept because a Commander asking
    /// what they earned usually wants to know what they have as well.
    /// </summary>
    public long? Balance { get; init; }

    public long TotalEarnings =>
        BountyEarnings + CombatBondEarnings + TradeEarnings +
        ExplorationEarnings + MissionEarnings + VoucherEarnings;

    public bool IsKnown => StartedAt is not null;

    public TimeSpan? Elapsed => StartedAt is { } start && LastEventAt is { } last && last > start
        ? last - start
        : null;

    public SessionSummary Apply(JournalEvent journalEvent)
    {
        // A fresh session wipes the slate. Everything before this belongs to a session that has
        // ended, and carrying it forward would report a morning's earnings as an evening's.
        if (journalEvent.Kind == "LoadGame")
        {
            return new SessionSummary
            {
                StartedAt = journalEvent.Timestamp,
                LastEventAt = journalEvent.Timestamp,
                Balance = journalEvent.Long("Credits"),
            };
        }

        var updated = this with { LastEventAt = journalEvent.Timestamp };

        return journalEvent.Kind switch
        {
            // TotalReward is the modern field; older journals only itemise per faction. Summing
            // the rewards when the total is absent keeps both readable.
            "Bounty" => updated with
            {
                BountyEarnings = BountyEarnings +
                    (journalEvent.Long("TotalReward")
                     ?? journalEvent.Items("Rewards").Sum(reward => reward.Long("Reward") ?? 0)),
            },

            "FactionKillBond" => updated with
            {
                CombatBondEarnings = CombatBondEarnings + (journalEvent.Long("Reward") ?? 0),
            },

            "MarketSell" => updated with
            {
                TradeEarnings = TradeEarnings + (journalEvent.Long("TotalSale") ?? 0),
            },

            "SellExplorationData" or "MultiSellExplorationData" => updated with
            {
                ExplorationEarnings = ExplorationEarnings + (journalEvent.Long("TotalEarnings") ?? 0),
            },

            // Odyssey's biological sampling, which earns separately from cartographic data but
            // is exploration as far as anyone asking would mean it.
            "SellOrganicData" => updated with
            {
                ExplorationEarnings = ExplorationEarnings +
                    journalEvent.Items("BioData").Sum(data => (data.Long("Value") ?? 0) + (data.Long("Bonus") ?? 0)),
            },

            "MissionCompleted" => updated with
            {
                MissionEarnings = MissionEarnings + (journalEvent.Long("Reward") ?? 0),
            },

            "RedeemVoucher" => updated with
            {
                VoucherEarnings = VoucherEarnings + (journalEvent.Long("Amount") ?? 0),
            },

            "FSDJump" => updated with
            {
                Jumps = Jumps + 1,
                DistanceTravelled = DistanceTravelled + (journalEvent.Double("JumpDist") ?? 0),
            },

            "MaterialCollected" => updated with
            {
                MaterialsGained = MaterialsGained + (journalEvent.Int("Count") ?? 1),
            },

            // The detailed scan, not the honk. A discovery scan reports a system's body count
            // in one event and would inflate this by an order of magnitude.
            "Scan" => updated with { BodiesScanned = BodiesScanned + 1 },

            "Died" => updated with { Deaths = Deaths + 1 },

            // Only the ones that succeeded against the Commander. An escaped interdiction is
            // reported by the same event with Succeeded false.
            "Interdicted" => journalEvent.Bool("Submitted") || !journalEvent.Bool("Succeeded")
                ? updated
                : updated with { Interdictions = Interdictions + 1 },

            _ => updated,
        };
    }
}
