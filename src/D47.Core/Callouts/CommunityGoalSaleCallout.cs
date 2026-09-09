using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>The running total, said after every sale of the Community Goal commodity (#296).</summary>
public sealed class CommunityGoalSaleCallout(CommodityLedger ledger, CommunityGoalSearch search) : ICallout
{
    public string Id => "community-goal-sales";

    public const string Key = "community-goal.sale";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming)
        {
            yield break;
        }

        var commander = context.State?.Identity.FrontierId;

        // The whole batch is already folded into the ledger before Examine runs, so one matching sale
        // anywhere in it already sees the batch's final total — announcing per sale would repeat that same
        // total as a second, duplicate sentence (#311).
        if (!context.Events.Any(e => e.Kind == "MarketSell" && search.IsCommodity(e.Named("Type"))))
        {
            yield break;
        }

        var commodity = search.Commodity;
        var session = ledger.Session(commander, commodity);

        yield return new Announcement(Key, Sentence(session))
        {
            // Every sale is its own moment; the sale itself is the spacing.
            Cooldown = TimeSpan.Zero,
        };
    }

    /// <summary>
    /// The session figure, and nothing else (#342): "That's 2 million up this session." Today's and
    /// this week's totals are answers to a question — see <see
    /// cref="D47.Core.Capabilities.Builtin.CommunityGoalCapability"/> — not part of the automatic
    /// sentence.
    /// </summary>
    private static string Sentence(LedgerTotal session) => $"That's {session.Said} this session.";
}
