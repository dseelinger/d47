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
/// wholesale to avoid. See <see cref="Apply"/>, which since
/// <a href="https://github.com/dseelinger/d47/issues/128">#128</a> forgets on three events rather
/// than one — a rolling window used to expire a stale row by itself, and a file does not.
/// </para>
/// <para>
/// <b>This is kept in a file since #128</b> (<see cref="LoadoutStore"/>), so a ship not sat in
/// for months is still answerable. It stays a cache rather than a source of truth: everything in
/// it can be re-derived by <see cref="LoadoutBackfill"/> from the journals themselves.
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

    /// <summary>
    /// The event kinds that can change what is remembered, so a caller deciding whether to write
    /// the file asks this rather than keeping a second copy of the list
    /// (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>).
    /// <para>
    /// <b>Three of them come from <see cref="ShipLoadout.Apply"/> rather than from here</b>, which
    /// is why the list is not simply the branches below: a remembered ship changes when the ship
    /// being flown changes, and that happens on <c>Loadout</c>, on a rename, and on an
    /// <c>EngineerCraft</c> that Elite writes no <c>Loadout</c> for.
    /// </para>
    /// </summary>
    public static bool MayChange(JournalEvent journalEvent) =>
        journalEvent is not null && journalEvent.Kind
            is "Loadout" or "SetUserShipName" or "EngineerCraft"
            or "ShipyardSell" or "ShipyardBuy" or "ShipyardNew";

    /// <summary>
    /// <b>Forgetting, which is the half a durable file makes load-bearing</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>).
    /// <para>
    /// A swap or a transfer moves a ship the Commander still owns and its modules are unchanged by
    /// either, so neither is here. What is here is every event that makes a remembered ship
    /// <em>wrong</em> rather than merely old — and there are three of them rather than the one
    /// this fold shipped with, because <c>ShipID</c> is reused and a rolling window used to expire
    /// a stale row on its own. With the file it does not.
    /// </para>
    /// <para>
    /// <b>Measured on the 943-journal corpus rather than assumed.</b> <c>ShipyardSell</c> is 72
    /// events and names <c>SellShipID</c> every time. <c>ShipyardBuy</c> is 34, and <b>one</b> of
    /// them carries a <c>SellShipID</c> — a part exchange, which is a sale wearing another event's
    /// name and was previously missed. <c>ShipyardNew</c> is 34, names <c>NewShipID</c> every
    /// time, and <b>12 of the 34 reuse an id that had already been alive</b>: a purchase is
    /// unambiguous proof that the id now belongs to something else, so it is the second and
    /// independent chance to forget a ship whose sale d47 was not running for.
    /// </para>
    /// </summary>
    public ShipLoadouts Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        var gone = journalEvent.Kind switch
        {
            "ShipyardSell" => journalEvent.Int("SellShipID"),

            // The part exchange. One in 34 measured, and it is a sale.
            "ShipyardBuy" => journalEvent.Int("SellShipID"),

            // Not a sale at all, and it forgets for a stronger reason than one: whatever this id
            // used to name, it names this new hull now.
            "ShipyardNew" => journalEvent.Int("NewShipID"),

            _ => null,
        };

        if (gone is not { } sold || !Ships.ContainsKey(sold))
        {
            return this;
        }

        var remaining = new Dictionary<int, RememberedShip>(Ships);
        _ = remaining.Remove(sold);

        return this with { Ships = remaining };
    }
}
