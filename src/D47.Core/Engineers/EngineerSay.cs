using System.Globalization;

namespace D47.Core.Engineers;

/// <summary>
/// The handful of phrases the Engineers tab and the engineers tools both use (Phase 28).
/// <para>
/// Shared rather than written twice, because <b>the ray points and the voice edits</b>: what the
/// page shows and what d47 says about the same ranking have to be the same sentence, or a
/// Commander comparing them is comparing two answers.
/// </para>
/// </summary>
public static class EngineerSay
{
    /// <summary>
    /// A distance, at the precision anybody can act on. Nobody flies a tenth of a light year, and
    /// the figure is the input to a jump count that has already rounded.
    /// </summary>
    public static string Distance(double? lightYears) =>
        lightYears is not { } light
            ? "distance unknown"
            : light < 10
                ? $"{light.ToString("N1", CultureInfo.InvariantCulture)} ly"
                : $"{light.ToString("N0", CultureInfo.InvariantCulture)} ly";

    /// <summary>
    /// A jump count, hedged. <b>"About"</b>, because the range is the ship's maximum and a real
    /// route bends around the stars that are actually there — a bare "3 jumps" claims a plotted
    /// route where this is a straight line divided by a number.
    /// </summary>
    public static string Jumps(int jumps) =>
        jumps == 1
            ? "about 1 jump"
            : $"about {jumps.ToString(CultureInfo.InvariantCulture)} jumps";

    /// <summary>How far and how many jumps, as a trailing clause, or nothing at all.</summary>
    public static string Trip(double? lightYears, int? jumps) =>
        (lightYears, jumps) switch
        {
            (null, _) => string.Empty,
            ({ } light, null) => $" — {Distance(light)}",
            ({ } light, { } count) => $" — {Distance(light)}, {Jumps(count)}",
        };

    /// <summary>A count and the thing counted, pluralised the boring way.</summary>
    public static string Count(int many, string one, string more) =>
        $"{many.ToString(CultureInfo.InvariantCulture)} {(many == 1 ? one : more)}";
}
