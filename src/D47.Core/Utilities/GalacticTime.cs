using System.Globalization;

namespace D47.Core.Utilities;

/// <summary>What time it is, in both worlds at once (Phase 24, "Two clocks, one instant").</summary>
public static class GalacticTime
{
    /// <summary>How far ahead Elite Dangerous runs.</summary>
    public const int YearsAhead = 1286;

    /// <summary>The same instant, in the galaxy's own reckoning.</summary>
    public static DateTimeOffset Galactic(DateTimeOffset instant) => instant.AddYears(YearsAhead);

    /// <summary>The instant as the Commander's own wall clock shows it.</summary>
    public static DateTimeOffset Local(DateTimeOffset instant, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(instant, zone);

    /// <summary>Both clocks, as the panel and the prompt say them.</summary>
    public static Clocks Read(DateTimeOffset instant, TimeZoneInfo zone) =>
        new(Local(instant, zone), Galactic(instant.ToUniversalTime()));
}

/// <summary>One instant, presented twice.</summary>
/// <param name="Local">The Commander's own clock.</param>
/// <param name="Galactic">
/// The same moment in UTC, 1286 years on — which is what the game runs on.
/// </param>
public sealed record Clocks(DateTimeOffset Local, DateTimeOffset Galactic)
{
    /// <summary>"17 August 3312, 21:04" — how the panel and a spoken answer both say the galactic side.</summary>
    public string GalacticDate => Galactic.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>The galactic wall-clock time, to the minute.</summary>
    public string GalacticTimeOfDay => Galactic.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The Commander's own date, in their own regional format.</summary>
    public string LocalDate => Local.ToString("D", CultureInfo.CurrentCulture);

    /// <summary>The Commander's own time, to the minute.</summary>
    public string LocalTimeOfDay => Local.ToString("HH:mm", CultureInfo.CurrentCulture);

    /// <summary>
    /// Both, as one sentence — what the keyword router answers with and what goes into the game-state
    /// block.
    /// </summary>
    public string Say() =>
        $"It is {GalacticTimeOfDay} on {GalacticDate} out here, and {LocalTimeOfDay} on "
        + $"{LocalDate} where you are.";
}
