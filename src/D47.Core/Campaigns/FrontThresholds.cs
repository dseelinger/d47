namespace D47.Core.Campaigns;

/// <summary>
/// The thresholds <see cref="CampaignFronts"/> detects fronts by. Influence is a fraction of 1. Page numbers are
/// from The Complete BGS Guide 2025.
/// </summary>
public static class FrontThresholds
{
    /// <summary>Influence at which a faction expands (p.65).</summary>
    public const double Expansion = 0.75;

    /// <summary>Influence at which a faction that does not control a system can take it by a coup (p.60).</summary>
    public const double Coup = 0.60;

    /// <summary>Days won that win a conflict (pp.56, 61).</summary>
    public const int ConflictDaysToWin = 4;

    /// <summary>The most days a conflict runs (pp.56, 61).</summary>
    public const int ConflictDays = 7;

    /// <summary>d47's own: a rival at this influence or more is approaching <see cref="Coup"/>.</summary>
    public const double CoupApproach = 0.50;

    /// <summary>d47's own: influence at this or more is approaching <see cref="Expansion"/>.</summary>
    public const double ExpansionApproach = 0.70;

    /// <summary>d47's own: a system's newest report older than this is stale.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(30);

    /// <summary>
    /// d47's own: the lead over the nearest rival below which a controlled system is a thin margin, by intent. Null
    /// where the intent does not hold control, so no lead is defended.
    /// </summary>
    public static double? Margin(SystemIntent intent) => intent switch
    {
        SystemIntent.Hold => 0.10,
        SystemIntent.Grow => 0.10,
        SystemIntent.ExpandFromHere => 0.15,
        SystemIntent.StayUnder75 => 0.10,
        _ => null,
    };
}
