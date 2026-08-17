namespace D47.Core.Conversation;

/// <summary>A stretch of time the ledger can be asked about, as absolute instants.</summary>
public readonly record struct SpendPeriod(string Name, DateTimeOffset From, DateTimeOffset To)
{
    /// <summary>
    /// Closed at both ends, deliberately.
    /// <para>
    /// Every period here ends at <em>now</em>, and the charge a Commander most wants to see is
    /// the one that was just made — stamped, to the tick, at the same instant. An exclusive
    /// upper bound puts it outside all four windows, so the turn they are looking at is the one
    /// turn the totals do not count. Half-open is the right default when windows abut and a
    /// point must fall in exactly one; these overlap by construction, so nothing is
    /// double-counted by including the edge.
    /// </para>
    /// </summary>
    public bool Holds(DateTimeOffset instant) => instant >= From && instant <= To;
}

/// <summary>
/// The four windows the spend dialog reports, worked out from an instant and a zone.
/// <para>
/// <b>Two of these are timezone-free and two are not, and that is the whole reason this is its
/// own type.</b> "The last seven days" is 168 hours back from now and means the same thing
/// everywhere. "This week" and "this month" are local-calendar ideas: they begin at a midnight,
/// and which instant that midnight is depends on where the Commander is and on whether the
/// clocks have gone forward since. Rows are stored as absolute instants precisely so this
/// arithmetic can be done at query time rather than baked in wrongly at write time.
/// </para>
/// <para>
/// Nothing here reads a clock. <paramref name="now"/> arrives from
/// <see cref="D47.Core.IWallClock"/> and the zone from the caller, which keeps every one of
/// these boundaries testable at a date that is not today.
/// </para>
/// </summary>
public static class SpendPeriods
{
    /// <summary>The rolling windows, plus the two calendar ones, in the order the dialog lists them.</summary>
    public static IReadOnlyList<SpendPeriod> All(DateTimeOffset now, TimeZoneInfo zone) =>
    [
        Rolling("Last 7 days", now, 7),
        Rolling("Last 30 days", now, 30),
        CurrentWeek(now, zone),
        CurrentMonth(now, zone),
    ];

    /// <summary>
    /// The last <paramref name="days"/> days ending now. No zone: an elapsed duration is the
    /// same duration wherever it is measured, and giving this one a midnight would make it a
    /// different question.
    /// </summary>
    public static SpendPeriod Rolling(string name, DateTimeOffset now, int days) =>
        new(name, now - TimeSpan.FromDays(days), now);

    /// <summary>
    /// Sunday to Saturday, in the Commander's own zone. Sunday because that is what the request
    /// asked for; it is a convention rather than a fact, and the ISO week starts on Monday.
    /// </summary>
    public static SpendPeriod CurrentWeek(DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var startOfDay = local.Date;

        return new SpendPeriod(
            "This week",
            StartOfLocalDay(startOfDay.AddDays(-(int)startOfDay.DayOfWeek), zone),
            now);
    }

    /// <summary>The first of the month in the Commander's own zone, to now.</summary>
    public static SpendPeriod CurrentMonth(DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(now, zone).Date;

        return new SpendPeriod(
            "This month",
            StartOfLocalDay(new DateTime(local.Year, local.Month, 1), zone),
            now);
    }

    /// <summary>
    /// The instant a local calendar day begins.
    /// <para>
    /// <b>Midnight is not guaranteed to exist.</b> Where the clocks go forward at midnight —
    /// they do in parts of South America — the local day starts at 01:00 and 00:00 never
    /// happens, and asking to convert it throws. Skipping ahead to the first minute that does
    /// exist is the honest reading of "when this day started".
    /// </para>
    /// <para>
    /// It can also happen twice, where the clocks go back. <see cref="TimeZoneInfo"/> resolves an
    /// ambiguous local time to the <em>standard</em> offset, which is the later of the two —
    /// leaving the repeated hour out of the window. Taking the earlier one instead means a week
    /// that starts when the Commander's clock first said it did.
    /// </para>
    /// </summary>
    private static DateTimeOffset StartOfLocalDay(DateTime localDay, TimeZoneInfo zone)
    {
        var midnight = DateTime.SpecifyKind(localDay.Date, DateTimeKind.Unspecified);

        while (zone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddMinutes(1);
        }

        if (zone.IsAmbiguousTime(midnight))
        {
            // Earliest offset wins. The offsets come back largest-first for a zone that falls
            // back an hour, and the larger offset is the earlier instant.
            var offsets = zone.GetAmbiguousTimeOffsets(midnight);
            return new DateTimeOffset(midnight, offsets.Max());
        }

        return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight));
    }
}
