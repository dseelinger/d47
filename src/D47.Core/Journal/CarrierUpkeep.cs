using System.Globalization;

namespace D47.Core.Journal;

/// <summary>The carrier's balance as it stands now, from the last recorded one less the weekly upkeep since.</summary>
/// <param name="Balance">The recorded balance less one week's upkeep for each tick since it was recorded.</param>
/// <param name="Recorded">The balance the journal last recorded.</param>
/// <param name="Weekly">The weekly upkeep, or null before any usable interval.</param>
/// <param name="Ticks">Weekly ticks since the balance was recorded.</param>
public readonly record struct CarrierBalance(
    long Balance,
    long Recorded,
    DateTimeOffset RecordedAt,
    long? Weekly,
    int Ticks)
{
    /// <summary>Whether upkeep has been taken off the recorded figure.</summary>
    public bool Adjusted => Weekly is not null && Ticks > 0;

    /// <summary>Whole weeks of upkeep the balance pays for, or null with no weekly figure.</summary>
    public long? WeeksCovered => Weekly is > 0 ? Math.Max(0, Balance / Weekly.Value) : null;

    /// <summary>What the adjustment took off what: "990,302,661 on 22 Sep, less one week's upkeep of 9,700,000".</summary>
    public string Explained =>
        $"{Recorded:N0} on {RecordedAt.ToString("d MMM", CultureInfo.InvariantCulture)}, less "
        + $"{(Ticks == 1 ? "one week's" : $"{Ticks} weeks'")} upkeep of {Weekly ?? 0:N0}";
}

/// <summary>Elite's weekly tick, when it takes the carrier's upkeep.</summary>
public static class CarrierUpkeep
{
    public const DayOfWeek TickDay = DayOfWeek.Thursday;

    public const int TickHourUtc = 7;

    /// <summary>A tick instant: 1970-01-01 was a Thursday.</summary>
    private static readonly DateTimeOffset Anchor = DateTimeOffset.UnixEpoch.AddHours(TickHourUtc);

    /// <summary>Weekly ticks in the half-open interval (<paramref name="from"/>, <paramref name="to"/>].</summary>
    public static int TicksBetween(DateTimeOffset from, DateTimeOffset to) =>
        to <= from ? 0 : (int)(WeeksSinceAnchor(to) - WeeksSinceAnchor(from));

    private static long WeeksSinceAnchor(DateTimeOffset at)
    {
        var ticks = (at - Anchor).Ticks;
        var week = TimeSpan.FromDays(7).Ticks;

        return ticks >= 0 ? ticks / week : ((ticks + 1) / week) - 1;
    }

    /// <summary>The balance now, or null for a squadron's carrier or one with no recorded balance.</summary>
    public static CarrierBalance? Now(CarrierState carrier, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(carrier);

        if (carrier.IsSquadron || carrier.Balance is not { } recorded || carrier.BalanceSeenAt is not { } at)
        {
            return null;
        }

        var weekly = carrier.WeeklyUpkeep;
        var ticks = weekly is null ? 0 : TicksBetween(at, now);

        return new CarrierBalance(recorded - ((weekly ?? 0) * ticks), recorded, at, weekly, ticks);
    }
}
