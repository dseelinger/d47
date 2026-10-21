using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>One outstanding commodity at a site, with the depot's figure and the planner's.</summary>
/// <param name="Depot">What the depot said was left at the Commander's last docking.</param>
/// <param name="Planner">What the planner says is left, or null when it gives no figure for this site and commodity.</param>
/// <param name="OnLinkedCarriers">Tonnes on the carriers linked to the planner's project; reported, never subtracted.</param>
public sealed record PlannedNeed(string Symbol, string Name, int Depot, int? Planner, int OnLinkedCarriers)
{
    /// <summary>The lower of the two figures, since a site's need only goes down.</summary>
    public int Remaining => Planner is { } planner ? Math.Min(Depot, planner) : Depot;
}

public static class PlannedNeeds
{
    /// <summary>Each outstanding commodity with the depot's figure, the planner's, and the one used.</summary>
    /// <remarks>
    /// The plan counts only when its <see cref="PlannedSite.MarketId"/> is the site's, and its figures only while its
    /// status is <see cref="PlanStatus.Build"/>. Completion is the depot's alone.
    /// </remarks>
    public static IReadOnlyList<PlannedNeed> Merge(ConstructionSite site, PlannedSite? plan)
    {
        ArgumentNullException.ThrowIfNull(site);

        var matched = plan is { MarketId: { } marketId } && marketId == site.MarketId ? plan : null;
        var figures = matched is { Status: PlanStatus.Build } ? matched.Needed : null;

        return
        [
            .. site.Outstanding
                .Select(resource =>
                {
                    int? planner = resource.Symbol is { } symbol && figures?.TryGetValue(symbol, out var left) == true
                        ? left
                        : null;
                    var onCarriers = resource.Symbol is { } named && matched is not null
                        ? matched.OnLinkedCarriers.GetValueOrDefault(named)
                        : 0;

                    return new PlannedNeed(resource.Symbol ?? string.Empty, resource.Name, resource.Remaining, planner, onCarriers);
                })
                .OrderByDescending(need => need.Remaining),
        ];
    }
}
