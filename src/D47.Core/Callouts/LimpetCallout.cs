using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// A reminder to buy limpets, when docking somewhere that sells them with a big hold and few
/// aboard (asked for 2026-08-21).
/// <para>
/// The Commander's words: <i>"When I land at a station that offers limpets, and I have more than X
/// cargo capacity, and I am under Y% limpets, remind me to grab limpets."</i> All three thresholds,
/// and the feature itself, are settings rows.
/// </para>
/// <para>
/// <b>Limpets are not a commodity, which is the thing to know before reading any of this.</b> They
/// are bought through <b>Advanced Maintenance</b>, not the commodity market — reported by the
/// Commander and confirmed at the pad: Reiter City's market, open on screen, listed 356 items and
/// not one limpet. It is also why Elite writes <c>BuyDrones</c> and why there is not one
/// <c>MarketBuy</c> for drones in 920 journals. So <c>Market.json</c> cannot answer whether a
/// station sells them, however fresh it is.
/// </para>
/// <para>
/// <b>The <c>rearm</c> service is the signal, and it was measured rather than guessed.</b> For each
/// of the 136 <c>BuyDrones</c> in the corpus with a known docking before it, the station's
/// <c>StationServices</c> were read: <c>rearm</c> is present at <b>133 of 136</b>, and all three
/// misses are the same fleet carrier. <c>repair</c> is not the flag — it is absent at two
/// <c>CraterOutpost</c> purchases where <c>rearm</c> is present. <c>commodities</c> is present at
/// all 136 and is useless as a filter, being present at 3,741 of 3,779 dockings anyway.
/// </para>
/// <para>
/// <b>95% is not a false positive rate.</b> <c>rearm</c> is present at 3,591 of 3,779 dockings, and
/// an earlier reading rejected it for that — wrongly. Most stations really do sell limpets, and the
/// 188 without it are exactly what one would expect: on-foot settlements, small outposts, and
/// carriers whose owner has not fitted the service. What makes this rare is the other two
/// conditions, which is where it always belonged.
/// </para>
/// <para>
/// <b>The carrier miss is accepted rather than special-cased</b> (the Commander's ruling,
/// 2026-08-21, choosing the simplest of three offered). A Commander restocking at their own carrier
/// hears nothing. That is a known three-in-136 and is not a bug report waiting to happen, because
/// it is written down here.
/// </para>
/// <para>
/// <b>No price is quoted.</b> Reported as always the same, and it very nearly is, but across 148
/// purchases it is 101 (×105), 95 (×36), 82 (×5) and one each of 201, 97 and 91. d47 has no reading
/// of <em>this</em> station's price at the pad — <c>BuyDrones</c> only fires after buying — so the
/// line says limpets are low and never what topping up will cost.
/// </para>
/// </summary>
public sealed class LimpetCallout : ICallout
{
    public string Id => "limpets";

    public const string Key = "limpets.low";

    /// <summary>Elite's own name for a limpet. Folded to a symbol by <see cref="CargoHold.Of"/>.</summary>
    public const string Limpet = "drones";

    /// <summary>The service that gates Advanced Maintenance, and therefore limpets.</summary>
    public const string Service = "rearm";

    /// <summary>
    /// The smallest hold worth reminding about, in tonnes, and the limpet threshold as a percentage
    /// <b>of that hold's capacity</b> — the Commander's ruling, 2026-08-21. Both are settings rows;
    /// these are the defaults if nothing supplies them.
    /// </summary>
    public Func<int> Floor { get; set; } = () => 64;

    public Func<int> Percent { get; set; } = () => 5;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.State is not { } state || context.IsPriming)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "Docked" || !Sells(journalEvent))
            {
                continue;
            }

            if (state.Ship.CargoCapacity is not { } capacity || capacity <= Floor())
            {
                continue;
            }

            // Not merely empty: the hold has to have been read at all. A Commander whose Cargo.json
            // has never been written is not a Commander with no limpets, and telling them to go
            // shopping on that basis would be inventing the reading.
            if (!state.Hold.IsKnown || !state.Hold.IsShip)
            {
                continue;
            }

            var held = state.Hold.Of(Limpet);

            // Percent of capacity, stated as a multiplication so no rounding is invented: held×100
            // against capacity×percent is the same comparison with none of the division.
            if (held * 100 >= capacity * Percent())
            {
                continue;
            }

            yield return new Announcement(Key, Said(held, capacity))
            {
                // Long, because it is about a place rather than a moment, and a Commander who
                // docks twice in ten minutes has not forgotten.
                Cooldown = TimeSpan.FromMinutes(20),
            };
        }
    }

    private static bool Sells(JournalEvent journalEvent) =>
        journalEvent.Items("StationServices")
            .Any(service => string.Equals(service.GetString(), Service, StringComparison.OrdinalIgnoreCase));

    private static string Said(int held, int capacity) =>
        held == 0
            ? $"No limpets aboard, and this station sells them. You have {Tonnes(capacity)} to fill."
            : $"{Tonnes(held)} of limpets against {Tonnes(capacity)} of hold. This station sells them.";

    private static string Tonnes(int count) => count == 1 ? "1 tonne" : $"{count} tonnes";
}
