namespace D47.Core.Conversation;

/// <summary>A stretch of time the ledger can be asked about, as absolute instants.</summary>
public readonly record struct SpendPeriod(string Name, DateTimeOffset From, DateTimeOffset To)
{
    /// <summary>Closed at both ends, deliberately.</summary>
    public bool Holds(DateTimeOffset instant) => instant >= From && instant <= To;
}

/// <summary>The five windows the spend dialog reports, worked out from an instant and a zone.</summary>
public static class SpendPeriods
{
    /// <summary>
    /// Every window, in the order the dialog lists them: <see cref="Immediate"/> then <see
    /// cref="Windows"/>.
    /// </summary>
    public static IReadOnlyList<SpendPeriod> All(DateTimeOffset now, TimeZoneInfo zone) =>
        [.. Immediate(now, zone), .. Windows(now, zone)];

    /// <summary>
    /// The window that reads beside the turn and the session rather than beside the others (#227).
    /// </summary>
    public static IReadOnlyList<SpendPeriod> Immediate(DateTimeOffset now, TimeZoneInfo zone) =>
        [Today(now, zone)];

    /// <summary>
    /// The four spans a Commander compares, each calendar window beside its rolling twin (#227).
    /// </summary>
    public static IReadOnlyList<SpendPeriod> Windows(DateTimeOffset now, TimeZoneInfo zone) =>
    [
        CurrentWeek(now, zone),
        Rolling("Last 7 days", now, 7),
        CurrentMonth(now, zone),
        Rolling("Last 30 days", now, 30),
    ];

    /// <summary>The windows the Details dialog offers to reset (#197).</summary>
    /// <param name="launchedAt">When this process started.</param>
    public static IReadOnlyList<SpendPeriod> Resettable(
        DateTimeOffset now,
        TimeZoneInfo zone,
        DateTimeOffset launchedAt) =>
    [
        new SpendPeriod("This session", launchedAt, now),
        .. All(now, zone),
    ];

    /// <summary>Midnight in the Commander's own zone, to now.</summary>
    public static SpendPeriod Today(DateTimeOffset now, TimeZoneInfo zone) =>
        new("Today", StartOfLocalDay(TimeZoneInfo.ConvertTime(now, zone).Date, zone), now);

    /// <summary>The last <paramref name="days"/> days ending now.</summary>
    public static SpendPeriod Rolling(string name, DateTimeOffset now, int days) =>
        new(name, now - TimeSpan.FromDays(days), now);

    /// <summary>Sunday to Saturday, in the Commander's own zone.</summary>
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

    /// <summary>The instant a local calendar day begins.</summary>
    internal static DateTimeOffset StartOfLocalDay(DateTime localDay, TimeZoneInfo zone)
    {
        var midnight = DateTime.SpecifyKind(localDay.Date, DateTimeKind.Unspecified);

        while (zone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddMinutes(1);
        }

        if (zone.IsAmbiguousTime(midnight))
        {
            // Earliest offset wins.
            var offsets = zone.GetAmbiguousTimeOffsets(midnight);
            return new DateTimeOffset(midnight, offsets.Max());
        }

        return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight));
    }
}
