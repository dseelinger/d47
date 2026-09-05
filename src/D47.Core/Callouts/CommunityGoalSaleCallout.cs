using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>
/// The running total, said after every sale of the Community Goal commodity
/// (<a href="https://github.com/dseelinger/d47/issues/296">#296</a>).
/// <para>
/// <b>The session total and not the sale</b> (ruling, 2026-09-05): the Commander is looking at
/// the sale's own figure on the screen at the moment it happens, and what they cannot see is where
/// that leaves them. "2.1 million up this session" is the line, net of what the cargo cost.
/// </para>
/// <para>
/// <b>Reads the ledger, never writes it.</b> The host folds every tick's events into
/// <see cref="CommodityLedger"/> before the callouts run, so switching this row off stops the
/// sentence and nothing else — the ledger the page and the "how have I done" question read stays
/// whole. A disabled callout is not examined at all, which is why the fold cannot live here.
/// </para>
/// <para>
/// Nothing during priming, like every callout: a sale from this afternoon's backlog is a fact for
/// the ledger and not a thing to announce.
/// </para>
/// </summary>
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

        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "MarketSell" when search.IsCommodity(journalEvent.Named("Type")):
                    var total = ledger.Session(commander, search.Commodity);

                    yield return new Announcement(Key, $"That's {total.Said} this session.")
                    {
                        // Every sale is its own moment; the sale itself is the spacing.
                        Cooldown = TimeSpan.Zero,
                    };

                    break;
            }
        }
    }
}
