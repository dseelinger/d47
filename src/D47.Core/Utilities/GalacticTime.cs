using System.Globalization;

namespace D47.Core.Utilities;

/// <summary>
/// What time it is, in both worlds at once (list.md Phase 24, "Two clocks, one instant").
/// <para>
/// <b>Elite runs 1286 years ahead</b>, so 2026-08-17 is 3312-08-17 and the in-universe date is
/// <em>arithmetic over the same instant</em> rather than a second clock. That is the whole design:
/// one <c>UtcNow</c> presents twice, and the two cannot disagree because there is only one of
/// them to be wrong.
/// </para>
/// <para>
/// <b><see cref="IWallClock"/> already is the mechanism and no invariant needs relaxing.</b> "No
/// Core component reads the clock" means Core does not call <c>DateTimeOffset.Now</c> inline; it
/// takes an injected clock and asks, and <c>SystemWallClock.Instance</c> returns the real system
/// time in the running app. So a Commander sees their actual clock through one constructor
/// parameter, and the replay harness can still run a year of turns in seconds. Dropping the
/// injection would buy one fewer parameter and cost the 692,631-event corpus replay.
/// </para>
/// <para>
/// <b>The instant is UTC and the zone is passed in at the point of asking</b>, which is the rule
/// <c>WallClock</c> already states and which <c>SpendWindows</c> already follows. An instant
/// recorded in local wall-clock is wrong twice a year and cannot be repaired afterwards.
/// </para>
/// </summary>
public static class GalacticTime
{
    /// <summary>
    /// How far ahead Elite Dangerous runs. A fact about the game rather than a preference, so it
    /// is a constant here rather than a setting somebody can put out of step with the fiction.
    /// </summary>
    public const int YearsAhead = 1286;

    /// <summary>The same instant, in the galaxy's own reckoning.</summary>
    public static DateTimeOffset Galactic(DateTimeOffset instant) => instant.AddYears(YearsAhead);

    /// <summary>
    /// The instant as the Commander's own wall clock shows it.
    /// <para>
    /// The zone is a parameter because the alternative is Core deciding what "local" means, which
    /// is the same class of mistake as Core deciding what "now" means.
    /// </para>
    /// </summary>
    public static DateTimeOffset Local(DateTimeOffset instant, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(instant, zone);

    /// <summary>
    /// Both clocks, as the panel and the prompt say them.
    /// <para>
    /// The galactic date is derived from the <em>local</em> presentation rather than from UTC, and
    /// that is deliberate: the in-game date a Commander sees beside their own is the one that
    /// changes over at their midnight, not at Greenwich's. It is still one instant — the zone is
    /// applied once and the years are added to the result.
    /// </para>
    /// </summary>
    public static Clocks Read(DateTimeOffset instant, TimeZoneInfo zone)
    {
        var local = Local(instant, zone);

        return new Clocks(local, Galactic(local));
    }
}

/// <summary>
/// One instant, presented twice.
/// </summary>
/// <param name="Local">The Commander's own clock.</param>
/// <param name="Galactic">The same moment, 1286 years on.</param>
public sealed record Clocks(DateTimeOffset Local, DateTimeOffset Galactic)
{
    /// <summary>
    /// "17 August 3312, 21:04" — how the panel and a spoken answer both say the galactic side.
    /// <para>
    /// Invariant culture, because the galaxy's calendar is not the Commander's regional format and
    /// a date rendered as 08/17 or 17/08 depending on where somebody lives is a date that reads as
    /// two different days.
    /// </para>
    /// </summary>
    public string GalacticDate => Galactic.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>The galactic wall-clock time, to the minute. Seconds are noise on a panel.</summary>
    public string GalacticTimeOfDay => Galactic.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The Commander's own date, in their own regional format.</summary>
    public string LocalDate => Local.ToString("D", CultureInfo.CurrentCulture);

    /// <summary>The Commander's own time, to the minute.</summary>
    public string LocalTimeOfDay => Local.ToString("HH:mm", CultureInfo.CurrentCulture);

    /// <summary>
    /// Both, as one sentence — what the keyword router answers with and what goes into the
    /// game-state block.
    /// <para>
    /// <b>Pre-computed, and the model is never asked to add 1286.</b> That is arithmetic, and a
    /// model doing arithmetic in prose is wrong occasionally and confidently, which is the exact
    /// failure this repository is written against.
    /// </para>
    /// </summary>
    public string Say() =>
        $"It is {GalacticTimeOfDay} on {GalacticDate} out here, and {LocalTimeOfDay} on "
        + $"{LocalDate} where you are.";
}
