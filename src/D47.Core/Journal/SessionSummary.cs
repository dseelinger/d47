namespace D47.Core.Journal;

/// <summary>
/// What has happened since the Commander entered the game (Phase 7, "Session summary"), accumulated
/// from the journal already being tailed rather than from anything new.
/// </summary>
public sealed record SessionSummary
{
    public static readonly SessionSummary Empty = new();

    /// <summary>When the session started, from the LoadGame event.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>The last event timestamp folded in — the far end of the window these totals cover.</summary>
    public DateTimeOffset? LastEventAt { get; init; }

    public long BountyEarnings { get; init; }

    public long CombatBondEarnings { get; init; }

    public long TradeEarnings { get; init; }

    public long ExplorationEarnings { get; init; }

    public long MissionEarnings { get; init; }

    /// <summary>Vouchers cashed in.</summary>
    public long VoucherEarnings { get; init; }

    public int Jumps { get; init; }

    /// <summary>Light years covered in hyperspace, summed from each jump's reported distance.</summary>
    public double DistanceTravelled { get; init; }

    public int MaterialsGained { get; init; }

    /// <summary>Bodies scanned with the detailed surface scanner, and first discoveries among them.</summary>
    public int BodiesScanned { get; init; }

    public int Deaths { get; init; }

    public int Interdictions { get; init; }

    /// <summary>The credit balance Elite last reported, from LoadGame.</summary>
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
        // A fresh session wipes the slate.
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
            // TotalReward is the modern field; older journals only itemise per faction.
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

            // Odyssey's biological sampling, which earns separately from cartographic data but is exploration
            // as far as anyone asking would mean it.
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

            // The detailed scan, not the honk.
            "Scan" => updated with { BodiesScanned = BodiesScanned + 1 },

            "Died" => updated with { Deaths = Deaths + 1 },

            // Only the ones that succeeded against the Commander.
            "Interdicted" => journalEvent.Bool("Submitted") || !journalEvent.Bool("Succeeded")
                ? updated
                : updated with { Interdictions = Interdictions + 1 },

            _ => updated,
        };
    }
}
