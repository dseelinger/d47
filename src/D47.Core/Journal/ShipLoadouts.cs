namespace D47.Core.Journal;

/// <summary>One ship's modules as they were last seen, and when that was.</summary>
/// <param name="Loadout">The whole picture, exactly as it stood.</param>
/// <param name="SeenAt">
/// The timestamp of the journal event that carried it. <b>Off the event and never off a clock</b>,
/// which is what lets the replay harness drive this at 100x and lets a backfill over month-old
/// files date what it finds correctly.
/// </param>
public sealed record RememberedShip(ShipLoadout Loadout, DateTimeOffset SeenAt);

/// <summary>
/// Every ship the Commander has been seen sitting in, keyed on <c>ShipID</c> (asked for
/// 2026-08-20: <i>"Don't get amnesia — you should remember modules of ships"</i>).
/// <para>
/// <b>Elite reports the loadout of the ship you are in and no other</b>, and d47 read that as a
/// reason to say nothing about any other ship. But the constraint is about what Elite <em>sends</em>,
/// not about what has been seen: a ship the Commander sat in an hour ago was described in full,
/// and throwing that away the moment they swapped meant a fleet whose every slot read "not seen"
/// when it plainly had been.
/// </para>
/// <para>
/// <b>What is remembered stays dated.</b> A remembered loadout is a fact about a moment, not about
/// now — the Commander may have re-outfitted the ship at a station d47 never watched. So every
/// entry carries the timestamp it was taken at, and surfaces are expected to say so rather than
/// present it as live. That is the same distinction <see cref="FleetRegistry.TakenAt"/> keeps.
/// </para>
/// <para>
/// <b>Sold means forgotten.</b> Keeping a sold ship's modules would put a hull the Commander no
/// longer owns in front of them, which is the failure <see cref="FleetRegistry"/> replaces itself
/// wholesale to avoid.
/// </para>
/// </summary>
public sealed record ShipLoadouts
{
    public static readonly ShipLoadouts Empty = new();

    public IReadOnlyDictionary<int, RememberedShip> Ships { get; init; } =
        new Dictionary<int, RememberedShip>();

    public bool IsKnown => Ships.Count > 0;

    /// <summary>What this ship looked like when it was last seen, or null for one never seen.</summary>
    public RememberedShip? For(int? shipId) =>
        shipId is { } id && Ships.TryGetValue(id, out var remembered) ? remembered : null;

    /// <summary>
    /// Files the ship the Commander is in now, where it has moved on.
    /// <para>
    /// <b>Reference equality is the whole test</b>, and it is exact rather than a shortcut:
    /// <see cref="ShipLoadout.Apply"/> returns <c>this</c> for every event it does not handle, so
    /// the same instance arriving again means nothing about the ship changed. That keeps this
    /// free on the ~99% of events that are not about the ship, without comparing module lists.
    /// </para>
    /// </summary>
    public ShipLoadouts Remember(ShipLoadout ship, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(ship);

        if (ship.ShipId is not { } id || !ship.IsKnown)
        {
            return this;
        }

        if (Ships.TryGetValue(id, out var held) && ReferenceEquals(held.Loadout, ship))
        {
            return this;
        }

        return this with
        {
            Ships = new Dictionary<int, RememberedShip>(Ships) { [id] = new RememberedShip(ship, at) },
        };
    }

    public ShipLoadouts Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        // The only event that makes a remembered ship wrong rather than merely old. A swap or a
        // transfer moves a ship the Commander still owns, and its modules are unchanged by either.
        if (journalEvent.Kind != "ShipyardSell" || journalEvent.Int("SellShipID") is not { } sold)
        {
            return this;
        }

        if (!Ships.ContainsKey(sold))
        {
            return this;
        }

        var remaining = new Dictionary<int, RememberedShip>(Ships);
        _ = remaining.Remove(sold);

        return this with { Ships = remaining };
    }
}
